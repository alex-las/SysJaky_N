const APP_VERSION = 'pwa-v1.0.0';
const STATIC_CACHE = `sysjaky-static-${APP_VERSION}`;
const PAGE_CACHE = `sysjaky-pages-${APP_VERSION}`;
const RUNTIME_CACHE = `sysjaky-runtime-${APP_VERSION}`;
const OFFLINE_URL = '/Offline';

const PRECACHE_URLS = [
    '/',
    '/Courses/Index',
    '/Courses',
    '/dist/styles.min.css',
    '/dist/scripts.min.js',
    '/js/newsletter.js',
    '/js/pwa.js',
    OFFLINE_URL
];

const SYNC_DB_NAME = 'sysjaky-sync';
const SYNC_STORE = 'requests';
const SYNC_TAG = 'offline-form-sync';

async function openDatabase() {
    return await new Promise((resolve, reject) => {
        const request = indexedDB.open(SYNC_DB_NAME, 1);
        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve(request.result);
        request.onupgradeneeded = (event) => {
            const db = event.target.result;
            if (!db.objectStoreNames.contains(SYNC_STORE)) {
                db.createObjectStore(SYNC_STORE, { keyPath: 'id', autoIncrement: true });
            }
        };
    });
}

async function queueRequest(entry) {
    const db = await openDatabase();
    return await new Promise((resolve, reject) => {
        const tx = db.transaction(SYNC_STORE, 'readwrite');
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
        tx.objectStore(SYNC_STORE).add(entry);
    });
}

async function readQueuedRequests() {
    const db = await openDatabase();
    return await new Promise((resolve, reject) => {
        const tx = db.transaction(SYNC_STORE, 'readonly');
        const store = tx.objectStore(SYNC_STORE);
        const request = store.getAll();
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function removeQueuedRequest(id) {
    const db = await openDatabase();
    return await new Promise((resolve, reject) => {
        const tx = db.transaction(SYNC_STORE, 'readwrite');
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
        tx.objectStore(SYNC_STORE).delete(id);
    });
}

async function serializeRequest(request) {
    const cloned = request.clone();
    let body = null;
    try {
        body = await cloned.text();
    } catch (error) {
        body = null;
    }

    const headers = {};
    for (const [key, value] of cloned.headers.entries()) {
        if (key.toLowerCase() === 'content-length') {
            continue;
        }
        headers[key] = value;
    }

    return {
        url: cloned.url,
        method: cloned.method,
        headers,
        body,
        mode: cloned.mode,
        credentials: cloned.credentials
    };
}

async function replayQueuedRequests() {
    const entries = await readQueuedRequests();
    for (const entry of entries) {
        try {
            await fetch(entry.url, {
                method: entry.method,
                headers: entry.headers,
                body: entry.body,
                mode: entry.mode === 'navigate' ? 'cors' : entry.mode,
                credentials: entry.credentials
            });
            await removeQueuedRequest(entry.id);
        } catch (error) {
            console.warn('Background sync retry failed', error);
        }
    }
}

self.addEventListener('install', (event) => {
    event.waitUntil(
        (async () => {
            const cache = await caches.open(STATIC_CACHE);
            try {
                await cache.addAll(PRECACHE_URLS);
            } catch (error) {
                console.warn('Precaching failed', error);
            }
            await self.skipWaiting();
        })()
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        (async () => {
            const cacheNames = await caches.keys();
            await Promise.all(
                cacheNames
                    .filter((cacheName) => ![STATIC_CACHE, PAGE_CACHE, RUNTIME_CACHE].includes(cacheName))
                    .map((cacheName) => caches.delete(cacheName))
            );
            await self.clients.claim();
        })()
    );
});

async function networkFirst(request) {
    try {
        const networkResponse = await fetch(request);
        const cache = await caches.open(PAGE_CACHE);
        cache.put(request, networkResponse.clone());
        return networkResponse;
    } catch (error) {
        const cache = await caches.open(PAGE_CACHE);
        const cached = await cache.match(request, { ignoreSearch: true });
        if (cached) {
            return cached;
        }
        const offline = await caches.match(OFFLINE_URL);
        if (offline) {
            return offline;
        }
        return Response.error();
    }
}

async function staleWhileRevalidate(request, cacheName, getFallback) {
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);
    const networkPromise = fetch(request)
        .then((networkResponse) => {
            if (networkResponse && networkResponse.ok) {
                cache.put(request, networkResponse.clone());
            }
            return networkResponse;
        })
        .catch(async () => {
            if (cachedResponse) {
                return cachedResponse;
            }
            if (typeof getFallback === 'function') {
                const fallback = await getFallback();
                if (fallback) {
                    return fallback;
                }
            }
            return Response.error();
        });

    return cachedResponse || networkPromise;
}

self.addEventListener('fetch', (event) => {
    const { request } = event;

    if (request.method === 'POST' || request.method === 'PUT') {
        const url = new URL(request.url);
        if (url.origin === self.location.origin) {
            event.respondWith(
                (async () => {
                    try {
                        return await fetch(request.clone());
                    } catch (error) {
                        if ('sync' in self.registration) {
                            const serialized = await serializeRequest(request);
                            await queueRequest(serialized);
                            await self.registration.sync.register(SYNC_TAG);
                        }
                        return new Response(JSON.stringify({
                            success: true,
                            offline: true,
                            message: 'Po obnovení připojení formulář odešleme automaticky.'
                        }), {
                            status: 202,
                            headers: { 'Content-Type': 'application/json' }
                        });
                    }
                })()
            );
            return;
        }
    }

    if (request.mode === 'navigate') {
        event.respondWith(networkFirst(request));
        return;
    }

    if (request.method !== 'GET') {
        return;
    }

    const url = new URL(request.url);

    if (url.origin !== self.location.origin) {
        return;
    }

    if (request.destination === 'style' || url.pathname.endsWith('.css')) {
        event.respondWith(staleWhileRevalidate(request, STATIC_CACHE));
        return;
    }

    if (request.destination === 'script' || url.pathname.endsWith('.js')) {
        event.respondWith(staleWhileRevalidate(request, RUNTIME_CACHE));
        return;
    }

    if (request.destination === 'image') {
        event.respondWith(
            staleWhileRevalidate(request, RUNTIME_CACHE)
        );
        return;
    }

    event.respondWith(
        (async () => {
            const cached = await caches.match(request);
            if (cached) {
                return cached;
            }

            try {
                return await fetch(request);
            } catch (error) {
                const offline = await caches.match(OFFLINE_URL);
                if (offline) {
                    return offline;
                }
                return Response.error();
            }
        })()
    );
});

self.addEventListener('sync', (event) => {
    if (event.tag === SYNC_TAG) {
        event.waitUntil(replayQueuedRequests());
    }
});

self.addEventListener('message', (event) => {
    if (event.data && event.data.type === 'retry-sync') {
        event.waitUntil(replayQueuedRequests());
    }
});

self.addEventListener('push', (event) => {
    if (!event.data) {
        return;
    }

    let payload;
    try {
        payload = event.data.json();
    } catch (error) {
        payload = { title: 'SysJaky', body: event.data.text() };
    }

    const title = payload.title || 'SysJaky';
    const options = {
        body: payload.body,
        icon: '/img/icons/icon.svg',
        badge: '/img/icons/icon.svg',
        data: {
            url: payload.url || '/',
            tag: payload.tag || 'sysjaky-notification'
        },
        actions: payload.actions || [
            { action: 'open', title: 'Otevřít', icon: '/img/icons/icon.svg' }
        ]
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const targetUrl = event.notification?.data?.url || '/';
    event.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clientList) => {
            for (const client of clientList) {
                if ('focus' in client) {
                    if (client.url.includes(targetUrl)) {
                        return client.focus();
                    }
                }
            }
            return self.clients.openWindow(targetUrl);
        })
    );
});


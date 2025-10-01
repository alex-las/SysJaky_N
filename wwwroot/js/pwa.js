(() => {
    const hasServiceWorkerSupport = 'serviceWorker' in navigator;
    if (!hasServiceWorkerSupport) {
        return;
    }

    const hasPushSupport = 'PushManager' in window && 'Notification' in window;
    const appConfig = window.__appConfig ?? {};
    const pushPublicKey = typeof appConfig.pushPublicKey === 'string' ? appConfig.pushPublicKey.trim() : '';
    const LOCAL_STORAGE_DISMISS_KEY = 'sysjaky-pwa-banner-dismissed';

    const pwaState = {
        deferredPrompt: null,
        banner: null,
        registration: null,
        pushStatusElement: null,
        pushEnableButton: null,
        pushDisableButton: null,
        pushSaveButton: null,
        pushCheckboxes: [],
        subscribed: false
    };

    const safeLocalStorage = {
        get(key) {
            try {
                return window.localStorage?.getItem(key) ?? null;
            } catch (error) {
                console.warn('Unable to access localStorage', error);
                return null;
            }
        },
        set(key, value) {
            try {
                window.localStorage?.setItem(key, value);
            } catch (error) {
                console.warn('Unable to persist data to localStorage', error);
            }
        }
    };

    const urlBase64ToUint8Array = (base64String) => {
        const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
        const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);
        for (let i = 0; i < rawData.length; i += 1) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    };

    const setStatusMessage = (message, tone = 'info') => {
        if (!pwaState.pushStatusElement) {
            return;
        }

        const toneClassMap = {
            success: 'text-success',
            error: 'text-danger',
            warning: 'text-warning',
            info: 'text-muted'
        };

        pwaState.pushStatusElement.classList.remove('text-success', 'text-danger', 'text-warning', 'text-muted');
        pwaState.pushStatusElement.classList.add(toneClassMap[tone] ?? toneClassMap.info);
        pwaState.pushStatusElement.textContent = message;
    };

    const updatePushUi = (isSubscribed) => {
        pwaState.subscribed = isSubscribed;
        if (pwaState.pushEnableButton) {
            pwaState.pushEnableButton.classList.toggle('d-none', isSubscribed);
        }
        if (pwaState.pushDisableButton) {
            pwaState.pushDisableButton.classList.toggle('d-none', !isSubscribed);
            pwaState.pushDisableButton.disabled = !isSubscribed;
        }
        if (pwaState.pushSaveButton) {
            pwaState.pushSaveButton.hidden = true;
            pwaState.pushSaveButton.disabled = true;
        }
    };

    const getSelectedTopics = () => pwaState.pushCheckboxes
        .filter((checkbox) => checkbox.checked)
        .map((checkbox) => checkbox.dataset.topic)
        .filter(Boolean);

    const subscribeToPush = async (topics) => {
        if (!hasPushSupport) {
            setStatusMessage('Tento prohlížeč nepodporuje webové notifikace.', 'warning');
            return false;
        }

        if (!pushPublicKey) {
            setStatusMessage('Notifikace nejsou dostupné – chybí veřejný klíč serveru.', 'error');
            return false;
        }

        const permission = Notification.permission === 'granted'
            ? 'granted'
            : await Notification.requestPermission();

        if (permission !== 'granted') {
            setStatusMessage('Pro zasílání upozornění je potřeba udělit oprávnění.', 'warning');
            return false;
        }

        try {
            const registration = pwaState.registration ?? await navigator.serviceWorker.ready;
            pwaState.registration = registration;
            let subscription = await registration.pushManager.getSubscription();
            if (!subscription) {
                const applicationServerKey = urlBase64ToUint8Array(pushPublicKey);
                subscription = await registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey
                });
            }

            const payload = subscription.toJSON();
            payload.topics = topics;

            const response = await fetch('/push/subscribe', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                throw new Error('Server odmítl přihlášení k notifikacím.');
            }

            updatePushUi(true);
            setStatusMessage('Upozornění byla úspěšně zapnuta.', 'success');
            if (pwaState.pushSaveButton) {
                pwaState.pushSaveButton.disabled = true;
            }
            return true;
        } catch (error) {
            console.warn('Push subscription failed', error);
            setStatusMessage('Nepodařilo se aktivovat upozornění. Zkuste to prosím znovu.', 'error');
            return false;
        }
    };

    const unsubscribeFromPush = async () => {
        try {
            const registration = pwaState.registration ?? await navigator.serviceWorker.ready;
            if (!registration) {
                return false;
            }

            const subscription = await registration.pushManager.getSubscription();
            if (!subscription) {
                updatePushUi(false);
                return false;
            }

            try {
                await fetch('/push/unsubscribe', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ endpoint: subscription.endpoint })
                });
            } catch (error) {
                console.warn('Unable to inform server about push unsubscription', error);
            }

            await subscription.unsubscribe();
            updatePushUi(false);
            setStatusMessage('Upozornění byla vypnuta.', 'info');
            return true;
        } catch (error) {
            console.warn('Push unsubscription failed', error);
            setStatusMessage('Nepodařilo se odhlásit upozornění.', 'error');
            return false;
        }
    };

    const ensureEngagementBanner = () => {
        if (pwaState.banner) {
            return pwaState.banner;
        }

        const banner = document.createElement('div');
        banner.className = 'pwa-engagement-banner';
        banner.setAttribute('role', 'dialog');
        banner.setAttribute('aria-live', 'polite');
        banner.innerHTML = `
            <button type="button" class="pwa-banner-close" aria-label="Zavřít nabídku">&times;</button>
            <div class="pwa-engagement-banner__title">Přidejte si SysJaky jako aplikaci</div>
            <p class="mb-2">Instalujte si naši PWA, mějte kurzy k dispozici offline a nechte si posílat upozornění na důležité novinky.</p>
            <div class="pwa-notification-preferences" role="group" aria-label="Kategorie notifikací">
                <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="pwa-notify-new" data-topic="new-courses" checked>
                    <label class="form-check-label" for="pwa-notify-new">Nové kurzy</label>
                </div>
                <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="pwa-notify-reminders" data-topic="course-reminders" checked>
                    <label class="form-check-label" for="pwa-notify-reminders">Připomenutí před začátkem kurzu</label>
                </div>
                <div class="form-check">
                    <input class="form-check-input" type="checkbox" id="pwa-notify-special" data-topic="special-offers" checked>
                    <label class="form-check-label" for="pwa-notify-special">Speciální nabídky</label>
                </div>
            </div>
            <div class="pwa-engagement-banner__actions">
                <button type="button" class="btn btn-primary" data-action="install">Přidat na plochu</button>
                <button type="button" class="btn btn-outline-primary" data-action="enable-push">Zapnout upozornění</button>
                <button type="button" class="btn btn-outline-secondary d-none" data-action="disable-push">Vypnout upozornění</button>
                <button type="button" class="btn btn-outline-primary" data-action="save-preferences" hidden>Uložit preference</button>
            </div>
            <div class="small text-muted mt-2" data-role="push-status"></div>
        `;

        document.body.appendChild(banner);

        const closeButton = banner.querySelector('.pwa-banner-close');
        closeButton?.addEventListener('click', () => {
            banner.classList.remove('is-visible');
            safeLocalStorage.set(LOCAL_STORAGE_DISMISS_KEY, '1');
        });

        const installButton = banner.querySelector('[data-action="install"]');
        const enableButton = banner.querySelector('[data-action="enable-push"]');
        const disableButton = banner.querySelector('[data-action="disable-push"]');
        const saveButton = banner.querySelector('[data-action="save-preferences"]');
        const statusElement = banner.querySelector('[data-role="push-status"]');
        const checkboxes = Array.from(banner.querySelectorAll('[data-topic]'));

        pwaState.banner = banner;
        pwaState.pushStatusElement = statusElement;
        pwaState.pushEnableButton = enableButton;
        pwaState.pushDisableButton = disableButton;
        pwaState.pushSaveButton = saveButton;
        pwaState.pushCheckboxes = checkboxes;

        if (!hasPushSupport || !pushPublicKey) {
            enableButton?.setAttribute('disabled', 'true');
            setStatusMessage('Upozornění nejsou v tomto prohlížeči dostupná.', 'warning');
        }

        installButton?.addEventListener('click', async () => {
            if (!pwaState.deferredPrompt) {
                setStatusMessage('Instalační nabídka není aktuálně dostupná.', 'info');
                return;
            }

            try {
                installButton.disabled = true;
                pwaState.deferredPrompt.prompt();
                const choice = await pwaState.deferredPrompt.userChoice;
                if (choice.outcome === 'accepted') {
                    setStatusMessage('Aplikace byla přidána na domovskou obrazovku.', 'success');
                } else {
                    setStatusMessage('Instalaci lze spustit později z této nabídky.', 'info');
                }
            } finally {
                pwaState.deferredPrompt = null;
                installButton.disabled = false;
            }
        });

        enableButton?.addEventListener('click', async () => {
            enableButton.disabled = true;
            setStatusMessage('Nastavuji upozornění…', 'info');
            const topics = getSelectedTopics();
            await subscribeToPush(topics);
            enableButton.disabled = false;
        });

        disableButton?.addEventListener('click', async () => {
            disableButton.disabled = true;
            await unsubscribeFromPush();
            disableButton.disabled = false;
        });

        saveButton?.addEventListener('click', async () => {
            saveButton.disabled = true;
            const topics = getSelectedTopics();
            await subscribeToPush(topics);
            saveButton.disabled = false;
            saveButton.hidden = true;
        });

        checkboxes.forEach((checkbox) => {
            checkbox.addEventListener('change', () => {
                if (pwaState.subscribed && pwaState.pushSaveButton) {
                    pwaState.pushSaveButton.hidden = false;
                    pwaState.pushSaveButton.disabled = false;
                }
            });
        });

        return banner;
    };

    const showBanner = () => {
        if (safeLocalStorage.get(LOCAL_STORAGE_DISMISS_KEY) === '1') {
            return;
        }
        const banner = ensureEngagementBanner();
        banner.classList.add('is-visible');
    };

    const registerServiceWorker = async () => {
        try {
            const registration = await navigator.serviceWorker.register('/service-worker.js');
            pwaState.registration = registration;
            await navigator.serviceWorker.ready;
            const subscription = await registration.pushManager.getSubscription();
            updatePushUi(Boolean(subscription));
        } catch (error) {
            console.warn('Service worker registration failed', error);
            setStatusMessage('Nepodařilo se aktivovat offline režim.', 'error');
        }
    };

    document.addEventListener('DOMContentLoaded', () => {
        ensureEngagementBanner();
        registerServiceWorker();

        window.addEventListener('beforeinstallprompt', (event) => {
            event.preventDefault();
            pwaState.deferredPrompt = event;
            showBanner();
        });

        window.addEventListener('appinstalled', () => {
            safeLocalStorage.set(LOCAL_STORAGE_DISMISS_KEY, '1');
            if (pwaState.banner) {
                pwaState.banner.classList.remove('is-visible');
            }
        });

        window.addEventListener('online', () => {
            setStatusMessage('Připojení k internetu je opět aktivní.', 'success');
            if (navigator.serviceWorker?.controller) {
                navigator.serviceWorker.controller.postMessage({ type: 'retry-sync' });
            } else {
                navigator.serviceWorker.ready
                    .then((registration) => registration.active?.postMessage({ type: 'retry-sync' }))
                    .catch(() => { /* ignored */ });
            }
        });

        window.addEventListener('offline', () => {
            setStatusMessage('Jste offline. Formuláře odešleme, jakmile budete online.', 'warning');
        });

        if (safeLocalStorage.get(LOCAL_STORAGE_DISMISS_KEY) !== '1') {
            setTimeout(() => {
                if (!document.visibilityState || document.visibilityState === 'visible') {
                    showBanner();
                }
            }, 4000);
        }
    });
})();

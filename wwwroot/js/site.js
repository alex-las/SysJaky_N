// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(function () {
    const hoverMedia = window.matchMedia ? window.matchMedia('(hover: hover)') : null;

    function initPopovers(scope) {
        if (!window.bootstrap || typeof window.bootstrap.Popover !== 'function') {
            return;
        }

        scope.querySelectorAll('[data-bs-toggle="popover"]').forEach((element) => {
            if (!element.__courseCardPopover) {
                element.__courseCardPopover = new window.bootstrap.Popover(element);
            }
        });
    }

    function updateWishlistButtonState(button, active) {
        button.classList.toggle('is-active', active);
        button.setAttribute('aria-pressed', String(active));

        const addLabel = button.getAttribute('data-wishlist-add-label') ?? '';
        const removeLabel = button.getAttribute('data-wishlist-remove-label') ?? '';
        const label = active ? removeLabel : addLabel;
        if (label) {
            button.setAttribute('aria-label', label);
            const hidden = button.querySelector('.visually-hidden');
            if (hidden) {
                hidden.textContent = label;
            }
        }
    }

    async function toggleWishlist(button) {
        const courseId = Number(button.dataset.courseId);
        if (!Number.isFinite(courseId)) {
            return;
        }

        if (button.dataset.wishlistPending === 'true') {
            return;
        }

        const isActive = button.classList.contains('is-active');
        const endpoint = isActive ? `/api/wishlist/${courseId}` : '/api/wishlist';
        const options = {
            method: isActive ? 'DELETE' : 'POST',
            headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
        };

        if (!isActive) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify({ courseId });
        }

        button.dataset.wishlistPending = 'true';

        try {
            const response = await fetch(endpoint, options);
            if (response.status === 401) {
                const returnUrl = encodeURIComponent(window.location.pathname + window.location.search + window.location.hash);
                window.location.href = `/Account/Login?returnUrl=${returnUrl}`;
                return;
            }

            if (!response.ok) {
                throw new Error(`Wishlist request failed with status ${response.status}`);
            }

            const data = await response.json().catch(() => ({ isWishlisted: !isActive }));
            const active = typeof data?.isWishlisted === 'boolean' ? data.isWishlisted : !isActive;
            updateWishlistButtonState(button, active);
        } catch (error) {
            console.error('Wishlist update failed', error);
        } finally {
            delete button.dataset.wishlistPending;
        }
    }

    function initWishlistButtons(scope) {
        scope.querySelectorAll('[data-wishlist-button]').forEach((button) => {
            if (button.dataset.wishlistReady === 'true') {
                return;
            }
            button.dataset.wishlistReady = 'true';
            button.addEventListener('click', (event) => {
                event.preventDefault();
                toggleWishlist(button);
            });
        });
    }

    function initPreviewTooltips(scope) {
        if (!hoverMedia || !hoverMedia.matches || !window.bootstrap || typeof window.bootstrap.Tooltip !== 'function') {
            return;
        }

        scope.querySelectorAll('[data-course-preview]').forEach((button) => {
            if (button.dataset.previewReady === 'true') {
                return;
            }
            const content = button.getAttribute('data-course-preview');
            if (!content) {
                return;
            }
            button.dataset.previewReady = 'true';
            button.__courseCardTooltip = new window.bootstrap.Tooltip(button, {
                title: content,
                placement: 'top',
                trigger: 'hover focus',
                customClass: 'course-card__preview-tooltip'
            });
        });
    }

    function initLazyImages(scope) {
        scope.querySelectorAll('.course-card__image').forEach((img) => {
            const cleanup = () => img.classList.remove('is-loading');
            if (img.dataset.lazyReady === 'true') {
                return;
            }
            img.dataset.lazyReady = 'true';
            if (img.complete) {
                cleanup();
                return;
            }
            img.addEventListener('load', cleanup, { once: true });
            img.addEventListener('error', cleanup, { once: true });
        });
    }

    function initCourseCards(scope) {
        if (!scope) {
            return;
        }

        initLazyImages(scope);
        initWishlistButtons(scope);
        initPreviewTooltips(scope);
    }

    window.SysJaky = window.SysJaky || {};
    window.SysJaky.courseCard = window.SysJaky.courseCard || { initScope: initCourseCards };

    document.addEventListener('DOMContentLoaded', () => {
        initPopovers(document);
        initCourseCards(document);
    });
})();

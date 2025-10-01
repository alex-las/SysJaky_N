// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', () => {
    const bootstrap = window.bootstrap;

    const getFocusableElements = (root) => Array.from(
        root.querySelectorAll(
            'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
        )
    ).filter((el) => !el.hasAttribute('inert'));

    const traps = new WeakMap();

    const enableFocusTrap = (element) => {
        if (traps.has(element)) {
            return;
        }

        const handler = (event) => {
            if (event.key !== 'Tab') {
                return;
            }

            const focusable = getFocusableElements(element);
            if (!focusable.length) {
                return;
            }

            const first = focusable[0];
            const last = focusable[focusable.length - 1];

            if (event.shiftKey) {
                if (document.activeElement === first) {
                    last.focus();
                    event.preventDefault();
                }
            } else if (document.activeElement === last) {
                first.focus();
                event.preventDefault();
            }
        };

        element.addEventListener('keydown', handler);
        traps.set(element, handler);
    };

    const disableFocusTrap = (element) => {
        const handler = traps.get(element);
        if (!handler) {
            return;
        }

        element.removeEventListener('keydown', handler);
        traps.delete(element);
    };

    const updateAriaExpanded = (trigger, isExpanded) => {
        if (trigger) {
            trigger.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
        }
    };

    const politeRegion = document.getElementById('accessibility-live-region');
    const assertiveRegion = document.getElementById('accessibility-assertive-region');

    window.accessibilityAnnounce = (message, options = {}) => {
        const settings = { assertive: false, ...options };
        const region = settings.assertive ? assertiveRegion : politeRegion;
        if (!region) {
            return;
        }

        region.textContent = '';
        window.requestAnimationFrame(() => {
            region.textContent = message;
        });
    };

    if (bootstrap && typeof bootstrap.Popover === 'function') {
        document.querySelectorAll('[data-bs-toggle="popover"]').forEach((element) => {
            new bootstrap.Popover(element);
        });
    }

    if (bootstrap && typeof bootstrap.Modal === 'function') {
        document.querySelectorAll('.modal').forEach((modalEl) => {
            modalEl.addEventListener('shown.bs.modal', () => {
                enableFocusTrap(modalEl);
                const focusable = getFocusableElements(modalEl);
                const initialFocus = focusable.find((el) => el.dataset.focusFirst === 'true') || focusable[0];
                if (initialFocus) {
                    initialFocus.focus({ preventScroll: true });
                }
                updateAriaExpanded(document.querySelector(`[data-bs-target="#${modalEl.id}"]`), true);
            });
            modalEl.addEventListener('hidden.bs.modal', () => {
                disableFocusTrap(modalEl);
                updateAriaExpanded(document.querySelector(`[data-bs-target="#${modalEl.id}"]`), false);
            });
        });
    }

    if (bootstrap && typeof bootstrap.Offcanvas === 'function') {
        document.querySelectorAll('.offcanvas').forEach((offcanvasEl) => {
            offcanvasEl.addEventListener('shown.bs.offcanvas', () => {
                enableFocusTrap(offcanvasEl);
                const focusable = getFocusableElements(offcanvasEl);
                if (focusable.length) {
                    focusable[0].focus({ preventScroll: true });
                }
                updateAriaExpanded(document.querySelector(`[href="#${offcanvasEl.id}"]`), true);
            });
            offcanvasEl.addEventListener('hidden.bs.offcanvas', () => {
                disableFocusTrap(offcanvasEl);
                updateAriaExpanded(document.querySelector(`[href="#${offcanvasEl.id}"]`), false);
            });
        });
    }

    if (bootstrap && typeof bootstrap.Dropdown === 'function') {
        document.querySelectorAll('[data-bs-toggle="dropdown"]').forEach((toggle) => {
            toggle.addEventListener('show.bs.dropdown', () => updateAriaExpanded(toggle, true));
            toggle.addEventListener('hide.bs.dropdown', () => updateAriaExpanded(toggle, false));
        });
    }

    document.querySelectorAll('[data-bs-toggle="collapse"]').forEach((toggle) => {
        const targetSelector = toggle.getAttribute('data-bs-target');
        if (!targetSelector) {
            return;
        }

        const target = document.querySelector(targetSelector);
        if (!target) {
            return;
        }

        target.addEventListener('shown.bs.collapse', () => updateAriaExpanded(toggle, true));
        target.addEventListener('hidden.bs.collapse', () => updateAriaExpanded(toggle, false));
    });
});

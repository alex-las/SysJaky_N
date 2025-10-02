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

    const wishlistStorageKey = 'courseWishlist';

    const readWishlistState = () => {
        try {
            const raw = window.localStorage?.getItem(wishlistStorageKey);
            if (!raw) {
                return new Set();
            }
            const parsed = JSON.parse(raw);
            if (Array.isArray(parsed)) {
                return new Set(parsed.map((value) => value.toString()));
            }
        } catch (error) {
            console.warn('Unable to read wishlist state', error);
        }
        return new Set();
    };

    const wishlistState = readWishlistState();

    const persistWishlist = () => {
        try {
            window.localStorage?.setItem(wishlistStorageKey, JSON.stringify(Array.from(wishlistState)));
        } catch (error) {
            console.warn('Unable to persist wishlist state', error);
        }
    };

    const syncWishlistButton = (button) => {
        const courseId = button?.dataset?.courseId;
        if (!courseId) {
            return;
        }

        const isActive = wishlistState.has(courseId.toString());
        const addLabel = button.dataset.wishlistLabelAdd ?? '';
        const removeLabel = button.dataset.wishlistLabelRemove ?? addLabel;

        button.classList.toggle('is-active', isActive);
        button.setAttribute('aria-pressed', isActive ? 'true' : 'false');
        button.setAttribute('aria-label', isActive ? removeLabel : addLabel);
    };

    const animateWishlist = (button) => {
        if (!button) {
            return;
        }

        button.classList.remove('is-burst');
        void button.offsetWidth; // trigger reflow
        button.classList.add('is-burst');
    };

    document.addEventListener('click', (event) => {
        const button = event.target.closest('[data-wishlist-button]');
        if (!button) {
            return;
        }

        event.preventDefault();

        const courseId = button.dataset.courseId;
        if (!courseId) {
            return;
        }

        if (wishlistState.has(courseId.toString())) {
            wishlistState.delete(courseId.toString());
        } else {
            wishlistState.add(courseId.toString());
        }

        persistWishlist();
        syncWishlistButton(button);
        animateWishlist(button);
    });

    const wishlistApi = {
        syncAll(root = document) {
            root.querySelectorAll('[data-wishlist-button]').forEach(syncWishlistButton);
        }
    };

    wishlistApi.syncAll();
    window.courseCardWishlist = wishlistApi;

    const previewApi = (() => {
        const bootstrapLib = window.bootstrap;
        if (!bootstrapLib || typeof bootstrapLib.Tooltip !== 'function') {
            return { register: () => undefined };
        }

        const mediaQuery = window.matchMedia('(hover: hover) and (pointer: fine)');
        const tooltips = new Map();

        const register = (root = document) => {
            if (!mediaQuery.matches) {
                return;
            }

            root.querySelectorAll('[data-course-preview]').forEach((element) => {
                if (tooltips.has(element)) {
                    return;
                }

                const text = element.getAttribute('data-course-preview');
                if (!text) {
                    return;
                }

                const tooltip = new bootstrapLib.Tooltip(element, {
                    title: text,
                    trigger: 'hover focus',
                    placement: 'top',
                    container: 'body',
                    customClass: 'course-preview-tooltip'
                });

                tooltips.set(element, tooltip);
            });
        };

        const disposeAll = () => {
            tooltips.forEach((tooltip, element) => {
                tooltip.dispose();
                tooltips.delete(element);
            });
        };

        if (typeof mediaQuery.addEventListener === 'function') {
            mediaQuery.addEventListener('change', (event) => {
                if (event.matches) {
                    register();
                } else {
                    disposeAll();
                }
            });
        }

        register();

        return { register };
    })();

    window.courseCardPreview = previewApi;
    previewApi.register();

    const initCertificationTimeline = () => {
        const section = document.querySelector('.certification-timeline-section');
        if (!section) {
            return;
        }

        const steps = Array.from(section.querySelectorAll('.certification-step'));
        const progressItems = Array.from(section.querySelectorAll('.timeline-progress-item'));
        const progressButtons = Array.from(section.querySelectorAll('.timeline-progress-button'));
        const stepToggles = steps.map((step) => step.querySelector('.certification-step-toggle'));
        const detailPanels = steps.map((step) => step.querySelector('.certification-step-detail'));
        const revealElements = Array.from(section.querySelectorAll('[data-scroll-reveal]'));

        if (!steps.length || !progressItems.length) {
            return;
        }

        let activeIndex = 0;
        const prefersReducedMotion = typeof window.matchMedia === 'function'
            ? window.matchMedia('(prefers-reduced-motion: reduce)').matches
            : false;

        const activateStep = (index, options = {}) => {
            const { userInitiated = false } = options;
            if (index < 0 || index >= steps.length) {
                return;
            }

            activeIndex = index;

            steps.forEach((step, stepIndex) => {
                const toggle = stepToggles[stepIndex];
                const panel = detailPanels[stepIndex];
                const isCurrent = stepIndex === index;
                const isComplete = stepIndex < index;

                step.classList.toggle('is-current', isCurrent);
                step.classList.toggle('is-complete', isComplete);
                step.classList.toggle('is-open', isCurrent);

                if (toggle && panel) {
                    toggle.setAttribute('aria-expanded', isCurrent ? 'true' : 'false');
                    panel.hidden = !isCurrent;
                }

                if (isCurrent) {
                    step.classList.add('is-visible');
                }
            });

            progressItems.forEach((item, itemIndex) => {
                item.classList.toggle('is-active', itemIndex === index);
                item.classList.toggle('is-complete', itemIndex < index);
            });

            if (userInitiated) {
                const targetStep = steps[index];
                if (targetStep) {
                    targetStep.scrollIntoView({
                        behavior: prefersReducedMotion ? 'auto' : 'smooth',
                        block: 'nearest',
                        inline: 'center'
                    });
                }
            }
        };

        progressButtons.forEach((button) => {
            button.addEventListener('click', () => {
                const value = button.dataset.stepIndex;
                const index = Number.parseInt(value ?? '', 10);
                if (!Number.isNaN(index)) {
                    activateStep(index, { userInitiated: true });
                }
            });
        });

        stepToggles.forEach((toggle, index) => {
            if (!toggle || !detailPanels[index]) {
                return;
            }

            toggle.addEventListener('click', () => {
                activateStep(index, { userInitiated: true });
            });
        });

        const supportsIntersectionObserver = typeof window !== 'undefined'
            && typeof window.IntersectionObserver === 'function';

        if (supportsIntersectionObserver && revealElements.length) {
            const revealObserver = new IntersectionObserver(
                (entries, observer) => {
                    entries.forEach((entry) => {
                        if (entry.isIntersecting) {
                            entry.target.classList.add('is-revealed');
                            observer.unobserve(entry.target);
                        }
                    });
                },
                {
                    threshold: 0.2,
                    rootMargin: '0px 0px -10% 0px'
                }
            );

            revealElements.forEach((element) => {
                revealObserver.observe(element);
            });
        } else if (!supportsIntersectionObserver) {
            revealElements.forEach((element) => {
                element.classList.add('is-revealed');
            });
        }

        if (supportsIntersectionObserver) {
            const progressObserver = new IntersectionObserver(
                (entries) => {
                    const visibleEntry = entries
                        .filter((entry) => entry.isIntersecting)
                        .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];

                    if (!visibleEntry) {
                        return;
                    }

                    const index = steps.indexOf(visibleEntry.target);
                    if (index !== -1 && index !== activeIndex) {
                        activateStep(index);
                    }
                },
                {
                    threshold: [0.25, 0.5, 0.75],
                    rootMargin: '-12% 0px -12% 0px'
                }
            );

            steps.forEach((step) => {
                progressObserver.observe(step);
            });
        }

        if (supportsIntersectionObserver) {
            const visibilityObserver = new IntersectionObserver(
                (entries) => {
                    entries.forEach((entry) => {
                        if (entry.isIntersecting) {
                            entry.target.classList.add('is-visible');
                        }
                    });
                },
                { threshold: 0.35 }
            );

            steps.forEach((step) => {
                visibilityObserver.observe(step);
            });
        } else {
            steps.forEach((step) => {
                step.classList.add('is-visible');
            });
        }

        activateStep(activeIndex);
    };

    initCertificationTimeline();
});

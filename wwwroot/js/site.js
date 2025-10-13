// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', () => {
    const bootstrap = window.bootstrap;

    const patchJqueryValidator = () => {
        const $ = window.jQuery;
        if (!$ || !$.validator || !$.validator.prototype) {
            return false;
        }

        const prototype = $.validator.prototype;
        if (prototype.elementValuePatched) {
            return true;
        }

        const originalElementValue = prototype.elementValue;
        prototype.elementValue = function patchedElementValue(element) {
            if (!element) {
                return "";
            }

            return originalElementValue.call(this, element);
        };

        prototype.elementValuePatched = true;
        return true;
    };

    if (!patchJqueryValidator()) {
        window.addEventListener('load', patchJqueryValidator, { once: true });
    }

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

    const toArray = (value) => {
        if (!value) {
            return [];
        }

        if (Array.isArray(value)) {
            return value;
        }

        if (value instanceof Element) {
            return [value];
        }

        return Array.from(value);
    };

    const updateAriaExpanded = (targets, isExpanded) => {
        toArray(targets).forEach((trigger) => {
            if (trigger instanceof Element) {
                trigger.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
            }
        });
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
        const getModalTriggers = (modalId) =>
            document.querySelectorAll(`[data-bs-target='#${modalId}'], [href='#${modalId}']`);

        document.querySelectorAll('[data-bs-toggle="modal"]').forEach((trigger) => {
            const selector = trigger.getAttribute('data-bs-target') || trigger.getAttribute('href');
            if (!selector || !selector.startsWith('#')) {
                return;
            }

            const modalEl = document.querySelector(selector);
            if (!modalEl) {
                return;
            }

            if (!trigger.hasAttribute('aria-haspopup')) {
                trigger.setAttribute('aria-haspopup', 'dialog');
            }

            if (!trigger.hasAttribute('aria-controls')) {
                trigger.setAttribute('aria-controls', modalEl.id);
            }

            if (!trigger.hasAttribute('aria-expanded')) {
                trigger.setAttribute('aria-expanded', 'false');
            }

            trigger.addEventListener('click', (event) => {
                if (trigger.tagName === 'A') {
                    event.preventDefault();
                }

                const instance = bootstrap.Modal.getOrCreateInstance(modalEl);
                instance.show();
            });
        });

        document.querySelectorAll('.modal').forEach((modalEl) => {
            modalEl.addEventListener('shown.bs.modal', () => {
                enableFocusTrap(modalEl);
                const focusable = getFocusableElements(modalEl);
                const initialFocus = focusable.find((el) => el.dataset.focusFirst === 'true') || focusable[0];
                if (initialFocus) {
                    initialFocus.focus({ preventScroll: true });
                }
                updateAriaExpanded(getModalTriggers(modalEl.id), true);
            });
            modalEl.addEventListener('hidden.bs.modal', () => {
                disableFocusTrap(modalEl);
                updateAriaExpanded(getModalTriggers(modalEl.id), false);
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
                updateAriaExpanded(document.querySelectorAll(`[href="#${offcanvasEl.id}"]`), true);
            });
            offcanvasEl.addEventListener('hidden.bs.offcanvas', () => {
                disableFocusTrap(offcanvasEl);
                updateAriaExpanded(document.querySelectorAll(`[href="#${offcanvasEl.id}"]`), false);
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

    const mainNav = document.getElementById('mainNav');
    if (mainNav) {
        const rootStyle = document.documentElement.style;
        const updateNavOffset = () => {
            const navHeight = Math.ceil(mainNav.getBoundingClientRect().height);
            if (navHeight > 0) {
                rootStyle.setProperty('--nav-offset', `${navHeight}px`);
            }
        };

        const scheduleNavUpdate = () => {
            window.requestAnimationFrame(updateNavOffset);
        };

        updateNavOffset();

        if ('ResizeObserver' in window) {
            const navResizeObserver = new ResizeObserver(scheduleNavUpdate);
            navResizeObserver.observe(mainNav);
        }

        window.addEventListener('resize', scheduleNavUpdate, { passive: true });
        window.addEventListener('orientationchange', scheduleNavUpdate);
        window.addEventListener('load', updateNavOffset);

        const navCollapse = mainNav.querySelector('.navbar-collapse');
        if (navCollapse) {
            navCollapse.addEventListener('shown.bs.collapse', updateNavOffset);
            navCollapse.addEventListener('hidden.bs.collapse', updateNavOffset);
        }

    }

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

    const newsletterForm = document.querySelector('[data-newsletter-form]');
    if (newsletterForm) {
        const statusEl = newsletterForm.querySelector('[data-newsletter-status]');
        const submitButton = newsletterForm.querySelector('[data-newsletter-submit]');
        const successMessage = newsletterForm.dataset.successMessage ?? '';
        const errorMessage = newsletterForm.dataset.errorMessage ?? '';
        const loadingText = newsletterForm.dataset.loadingText ?? '';
        const defaultButtonText = submitButton ? submitButton.textContent : '';

        const resetStatus = () => {
            if (!statusEl) {
                return;
            }

            statusEl.textContent = '';
            statusEl.classList.remove('text-success', 'text-danger');
            statusEl.classList.add('text-muted');
        };

        const applyStatus = (message, type) => {
            if (!statusEl) {
                return;
            }

            statusEl.textContent = message;
            statusEl.classList.remove('text-muted', 'text-success', 'text-danger');

            if (type === 'success') {
                statusEl.classList.add('text-success');
            } else if (type === 'error') {
                statusEl.classList.add('text-danger');
            }
        };

        const extractFirstError = (errors) => {
            if (!errors || typeof errors !== 'object') {
                return '';
            }

            const values = Object.values(errors);
            for (const value of values) {
                if (!value) {
                    continue;
                }

                if (Array.isArray(value) && value.length) {
                    return value[0];
                }

                if (typeof value === 'string' && value) {
                    return value;
                }
            }

            return '';
        };

        newsletterForm.addEventListener('submit', async (event) => {
            event.preventDefault();

            if (!newsletterForm.checkValidity()) {
                newsletterForm.reportValidity?.();
                return;
            }

            resetStatus();

            newsletterForm.setAttribute('aria-busy', 'true');

            if (submitButton) {
                submitButton.disabled = true;
                if (loadingText) {
                    submitButton.dataset.originalText = defaultButtonText ?? '';
                    submitButton.textContent = loadingText;
                }
            }

            try {
                const formData = new FormData(newsletterForm);
                formData.set('Input.Consent', formData.has('Input.Consent') ? 'true' : 'false');

                const payload = new URLSearchParams();
                formData.forEach((value, key) => {
                    if (value instanceof File) {
                        return;
                    }
                    payload.append(key, value == null ? '' : value.toString());
                });

                const response = await fetch(newsletterForm.action || '/Api/Newsletter', {
                    method: 'POST',
                    headers: { Accept: 'application/json' },
                    body: payload
                });

                let data = null;
                const contentType = response.headers.get('content-type') ?? '';
                if (contentType.includes('application/json')) {
                    data = await response.json();
                }

                if (response.ok && data && data.success) {
                    applyStatus(data.message || successMessage, 'success');
                    newsletterForm.reset();
                } else {
                    const message = (data && (data.message || extractFirstError(data.errors))) || errorMessage;
                    applyStatus(message, 'error');
                }
            } catch (error) {
                console.warn('Unable to submit newsletter form', error);
                applyStatus(errorMessage, 'error');
            } finally {
                newsletterForm.setAttribute('aria-busy', 'false');

                if (submitButton) {
                    submitButton.disabled = false;
                    const original = submitButton.dataset.originalText ?? defaultButtonText;
                    if (original) {
                        submitButton.textContent = original;
                    }
                    delete submitButton.dataset.originalText;
                }
            }
        });
    }

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

        const revealRootMargin = ['0px', '0px', '-10%', '0px'].join(' ');
        const progressRootMargin = ['-12%', '0px', '-12%', '0px'].join(' ');

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
                    rootMargin: revealRootMargin
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
                    rootMargin: progressRootMargin
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
// Scroll efekt na navbar
(() => {
    const initializeNavScrollEffect = () => {
        const mainNav = document.getElementById('mainNav');
        if (!mainNav) {
            return;
        }

        const toggleScrolledClass = () => {
            mainNav.classList.toggle('scrolled', window.scrollY > 50);
        };

        toggleScrolledClass();
        window.addEventListener('scroll', toggleScrolledClass, { passive: true });
        window.addEventListener('load', toggleScrolledClass);
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeNavScrollEffect, { once: true });
    } else {
        initializeNavScrollEffect();
    }
})();

(() => {
    const modalSelector = '[data-admin-modal="root"]';
    const modalElement = document.querySelector(modalSelector);
    if (!modalElement) {
        return;
    }

    const backdropElement = document.querySelector('[data-admin-modal="backdrop"]');
    const headerElement = modalElement.querySelector('[data-admin-modal="header"]');
    const bodyElement = modalElement.querySelector('[data-admin-modal="body"]');
    const actionsElement = modalElement.querySelector('[data-admin-modal="actions"]');
    let currentUrl = null;
    let isLoading = false;

    const focusableSelectors = [
        'a[href]',
        'button:not([disabled])',
        'textarea:not([disabled])',
        'input:not([type="hidden"]):not([disabled])',
        'select:not([disabled])',
        '[tabindex]:not([tabindex="-1"])'
    ];

    function dispatchEvent(name, detail) {
        document.dispatchEvent(new CustomEvent(name, { detail }));
    }

    function setLoadingState() {
        headerElement.innerHTML = '';
        actionsElement.innerHTML = '';
        bodyElement.innerHTML = '<div class="py-5 text-center"><div class="spinner-border text-primary" role="status" aria-live="polite"></div></div>';
    }

    function applyValidation(form) {
        if (window.jQuery && window.jQuery.validator && window.jQuery.validator.unobtrusive) {
            const $form = window.jQuery(form);
            $form.removeData('validator');
            $form.removeData('unobtrusiveValidation');
            window.jQuery.validator.unobtrusive.parse(form);
        }
    }

    function focusFirstElement() {
        const focusTarget = modalElement.querySelector('[data-admin-modal-focus]')
            || modalElement.querySelector(focusableSelectors.join(','));
        if (focusTarget) {
            focusTarget.focus();
        }
    }

    function showModal() {
        if (modalElement.classList.contains('show')) {
            return;
        }

        modalElement.setAttribute('aria-hidden', 'false');
        modalElement.style.display = 'block';
        document.body.classList.add('modal-open');
        document.body.style.overflow = 'hidden';

        requestAnimationFrame(() => {
            modalElement.classList.add('show');
            if (backdropElement) {
                backdropElement.style.display = 'block';
                backdropElement.classList.add('show');
            }
            focusFirstElement();
        });
    }

    function hideModal() {
        if (!modalElement.classList.contains('show')) {
            return;
        }

        modalElement.classList.remove('show');
        modalElement.setAttribute('aria-hidden', 'true');
        if (backdropElement) {
            backdropElement.classList.remove('show');
        }

        const transitionDuration = 200;
        window.setTimeout(() => {
            modalElement.style.display = 'none';
            if (backdropElement) {
                backdropElement.style.display = 'none';
            }
            document.body.classList.remove('modal-open');
            document.body.style.removeProperty('overflow');
            headerElement.innerHTML = '';
            bodyElement.innerHTML = '';
            actionsElement.innerHTML = '';
        }, transitionDuration);
    }

    function populateModal(html) {
        const container = document.createElement('div');
        container.innerHTML = html;

        const headerSlot = container.querySelector('[data-admin-modal-slot="header"]');
        const bodySlot = container.querySelector('[data-admin-modal-slot="body"]');
        const actionsSlot = container.querySelector('[data-admin-modal-slot="actions"]');

        headerElement.innerHTML = headerSlot ? headerSlot.innerHTML : '';
        bodyElement.innerHTML = bodySlot ? bodySlot.innerHTML : html;
        actionsElement.innerHTML = actionsSlot ? actionsSlot.innerHTML : '';

        const dialog = modalElement.querySelector('.modal-dialog');
        dialog.classList.remove('modal-sm', 'modal-lg', 'modal-xl');
        const size = container.querySelector('[data-admin-modal-size]')?.getAttribute('data-admin-modal-size');
        if (size) {
            dialog.classList.add(`modal-${size}`);
        } else {
            dialog.classList.add('modal-lg');
        }

        const form = bodyElement.querySelector('form');
        if (form) {
            form.setAttribute('data-admin-modal-form', 'true');
            applyValidation(form);
        }

        dispatchEvent('adminModal:contentLoaded', { modal: modalElement, body: bodyElement });
        focusFirstElement();
    }

    async function handleSubmit(event) {
        const form = event.target;
        if (!form.matches('[data-admin-modal-form]')) {
            return;
        }

        event.preventDefault();
        if (isLoading) {
            return;
        }

        const formData = new FormData(form);
        const action = form.getAttribute('action') || currentUrl || window.location.href;
        const method = (form.getAttribute('method') || 'post').toUpperCase();

        try {
            isLoading = true;
            const response = await fetch(action, {
                method,
                body: formData,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            const contentType = response.headers.get('content-type') || '';
            if (contentType.includes('application/json')) {
                const data = await response.json();
                if (data && data.success) {
                    hideModal();
                    dispatchEvent('adminModal:success', { url: action, response: data });
                    if (data.redirectUrl) {
                        window.location.href = data.redirectUrl;
                    } else if (data.reload !== false) {
                        window.location.reload();
                    }
                } else if (data && data.html) {
                    populateModal(data.html);
                }
            } else {
                const html = await response.text();
                if (!response.ok) {
                    dispatchEvent('adminModal:error', { url: action, status: response.status });
                }
                populateModal(html);
            }
        } catch (error) {
            console.error('Admin modal form submission failed', error);
            dispatchEvent('adminModal:error', { url: action, error });
        } finally {
            isLoading = false;
        }
    }

    async function openModal(url) {
        if (!url || isLoading) {
            return;
        }
        currentUrl = url;

        try {
            isLoading = true;
            setLoadingState();
            showModal();
            const response = await fetch(url, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });
            const html = await response.text();
            if (!response.ok) {
                dispatchEvent('adminModal:error', { url, status: response.status });
            }
            populateModal(html);
        } catch (error) {
            console.error('Failed to load admin modal content', error);
            dispatchEvent('adminModal:error', { url, error });
            hideModal();
        } finally {
            isLoading = false;
        }
    }

    document.addEventListener('click', (event) => {
        const trigger = event.target.closest('[data-admin-modal-target]');
        if (!trigger) {
            return;
        }

        const url = trigger.getAttribute('data-url');
        const target = trigger.getAttribute('data-admin-modal-target');
        if (!url || !target) {
            return;
        }

        const targetElement = document.querySelector(target);
        if (targetElement !== modalElement) {
            return;
        }

        event.preventDefault();
        openModal(url);
    });

    modalElement.addEventListener('click', (event) => {
        if (event.target === modalElement) {
            hideModal();
        }

        const dismissTrigger = event.target.closest('[data-admin-modal-dismiss]');
        if (dismissTrigger) {
            event.preventDefault();
            hideModal();
        }
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') {
            hideModal();
        }
    });

    bodyElement.addEventListener('submit', handleSubmit);
})();

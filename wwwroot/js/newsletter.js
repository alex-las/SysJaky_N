(() => {
    const form = document.getElementById('newsletter-form');
    if (!form) {
        return;
    }

    const emailInput = document.getElementById('newsletter-email');
    const consentInput = document.getElementById('newsletter-consent');
    const messageElement = form.querySelector('.newsletter-message');
    const submitButton = form.querySelector('button[type="submit"]');
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    const animateMessage = (element) => {
        if (!element || typeof element.animate !== 'function') {
            return;
        }

        element.animate(
            [
                { opacity: 0, transform: 'translateY(4px)' },
                { opacity: 1, transform: 'translateY(0)' }
            ],
            { duration: 250, easing: 'ease-out' }
        );
    };

    const setMessage = (type, text) => {
        if (!messageElement) {
            return;
        }

        messageElement.textContent = text;
        messageElement.classList.remove('text-success', 'text-danger');

        if (type === 'success') {
            messageElement.classList.add('text-success');
        } else if (type === 'error') {
            messageElement.classList.add('text-danger');
        }

        animateMessage(messageElement);
    };

    form.addEventListener('submit', async (event) => {
        event.preventDefault();

        if (!emailInput || !consentInput || !submitButton) {
            return;
        }

        const email = emailInput.value.trim();
        const consent = consentInput.checked;
        const errors = [];

        if (!emailRegex.test(email)) {
            errors.push('Zadejte platný e-mail.');
        }

        if (!consent) {
            errors.push('Pro přihlášení je nutný souhlas se zpracováním.');
        }

        if (errors.length > 0) {
            setMessage('error', errors[0]);
            return;
        }

        submitButton.disabled = true;
        submitButton.dataset.originalText = submitButton.dataset.originalText || submitButton.textContent;
        submitButton.textContent = 'Odesílám…';

        const payload = new URLSearchParams();
        payload.append('Email', email);
        payload.append('Consent', consent ? 'true' : 'false');

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: payload
            });

            const data = await response.json().catch(() => null);

            if (!response.ok) {
                let errorMessage = 'Něco se pokazilo. Zkuste to prosím znovu.';

                if (data?.errors) {
                    const flatErrors = Object.values(data.errors).flat();
                    if (flatErrors.length > 0) {
                        errorMessage = String(flatErrors[0]);
                    }
                } else if (typeof data?.message === 'string') {
                    errorMessage = data.message;
                }

                setMessage('error', errorMessage);
                return;
            }

            const successMessage = typeof data?.message === 'string'
                ? data.message
                : 'Děkujeme! Odeslali jsme potvrzovací e-mail.';

            if (data?.success) {
                form.reset();
                setMessage('success', successMessage);
            } else {
                setMessage('error', successMessage);
            }
        } catch (error) {
            setMessage('error', 'Nepodařilo se odeslat formulář. Zkontrolujte připojení a zkuste znovu.');
        } finally {
            submitButton.disabled = false;
            if (submitButton.dataset.originalText) {
                submitButton.textContent = submitButton.dataset.originalText;
            }
        }
    });
})();

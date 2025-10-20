(function () {
    const storageKey = 'sysjaky-chatbot-history';

    const selectors = {
        root: '[data-chatbot]',
        toggle: '[data-chatbot-toggle]',
        panel: '#chatbot-panel',
        close: '[data-chatbot-close]',
        messages: '[data-chatbot-messages]',
        form: '[data-chatbot-form]',
        input: '#chatbot-input',
        quickReply: '[data-quick-reply]',
        quickRepliesContainer: '[data-chatbot-quick-replies]',
        escalateButton: '[data-chatbot-escalate]',
        escalateForm: '[data-chatbot-escalate-form]',
        escalateCancel: '[data-chatbot-cancel-escalation]'
    };

    const state = {
        history: [],
        typingIndicator: null,
        initialized: false
    };

    const formatter = new Intl.NumberFormat('cs-CZ', {
        style: 'currency',
        currency: 'CZK',
        maximumFractionDigits: 0
    });

    const dateFormatter = new Intl.DateTimeFormat('cs-CZ', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });

    function loadHistory() {
        try {
            const raw = window.localStorage.getItem(storageKey);
            if (!raw) {
                return [];
            }
            const parsed = JSON.parse(raw);
            if (!Array.isArray(parsed)) {
                return [];
            }
            return parsed.filter(message => typeof message?.role === 'string' && typeof message?.content === 'string');
        } catch (error) {
            console.warn('Nepodařilo se načíst historii chatu.', error);
            return [];
        }
    }

    function saveHistory() {
        try {
            window.localStorage.setItem(storageKey, JSON.stringify(state.history));
        } catch (error) {
            console.warn('Nepodařilo se uložit historii chatu.', error);
        }
    }

    function createMessageElement(message) {
        const wrapper = document.createElement('div');
        wrapper.className = `chatbot-message chatbot-message-${message.role}`;
        wrapper.setAttribute('data-message-role', message.role);

        if (message.type === 'courses' && Array.isArray(message.data)) {
            const heading = document.createElement('p');
            heading.className = 'chatbot-message-text';
            heading.textContent = message.content;
            wrapper.appendChild(heading);

            const list = document.createElement('div');
            list.className = 'chatbot-course-list';

            message.data.forEach(course => {
                const item = document.createElement('article');
                item.className = 'chatbot-course-item';

                const title = document.createElement('h3');
                title.className = 'chatbot-course-title';
                title.textContent = course.title;
                item.appendChild(title);

                if (course.nextTermStart) {
                    const date = document.createElement('p');
                    date.className = 'chatbot-course-meta';
                    const parsedDate = new Date(course.nextTermStart);
                    date.textContent = `Nejbližší termín: ${dateFormatter.format(parsedDate)}`;
                    item.appendChild(date);
                }

                if (typeof course.price === 'number') {
                    const price = document.createElement('p');
                    price.className = 'chatbot-course-meta';
                    price.textContent = `Cena: ${formatter.format(course.price)}`;
                    item.appendChild(price);
                }

                if (course.detailUrl) {
                    const link = document.createElement('a');
                    link.className = 'chatbot-course-link';
                    link.href = course.detailUrl;
                    link.textContent = 'Zobrazit detail kurzu';
                    link.target = '_blank';
                    link.rel = 'noopener noreferrer';
                    item.appendChild(link);
                }

                list.appendChild(item);
            });

            wrapper.appendChild(list);
        } else {
            const text = document.createElement('p');
            text.className = 'chatbot-message-text';
            text.textContent = message.content;
            wrapper.appendChild(text);
        }

        return wrapper;
    }

    function renderMessages(messagesContainer) {
        messagesContainer.innerHTML = '';
        state.history.forEach(message => {
            messagesContainer.appendChild(createMessageElement(message));
        });
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    function addMessage(message, { persist = true } = {}) {
        message.timestamp = message.timestamp ?? new Date().toISOString();
        state.history.push(message);
        if (persist) {
            saveHistory();
        }
    }

    function appendMessageToDom(message, messagesContainer) {
        messagesContainer.appendChild(createMessageElement(message));
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    function showTyping(messagesContainer) {
        const typing = document.createElement('div');
        typing.className = 'chatbot-message chatbot-message-assistant chatbot-typing';
        typing.innerHTML = '<span></span><span></span><span></span>';
        messagesContainer.appendChild(typing);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
        state.typingIndicator = typing;
    }

    function hideTyping() {
        if (state.typingIndicator) {
            state.typingIndicator.remove();
            state.typingIndicator = null;
        }
    }

    async function sendMessage({ text, messagesContainer, input, form }) {
        if (!text.trim()) {
            return;
        }

        const userMessage = {
            role: 'user',
            content: text.trim(),
            type: 'text'
        };
        addMessage(userMessage);
        appendMessageToDom(userMessage, messagesContainer);

        form.reset();
        input.focus();

        showTyping(messagesContainer);

        try {
            const response = await fetch('/Api/Chatbot', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    message: userMessage.content,
                    history: state.history.map(({ role, content, type }) => ({ role, content, type }))
                })
            });

            hideTyping();

            if (!response.ok) {
                throw new Error(`Chyba serveru: ${response.status}`);
            }

            const payload = await response.json();

            const assistantMessage = {
                role: 'assistant',
                content: payload.reply ?? 'Omlouvám se, teď nerozumím. Zkuste to prosím znovu.',
                type: payload.courses && payload.courses.length ? 'courses' : 'text',
                data: Array.isArray(payload.courses) ? payload.courses : undefined
            };

            addMessage(assistantMessage);
            appendMessageToDom(assistantMessage, messagesContainer);

            if (payload.followUp) {
                const followUpMessage = {
                    role: 'assistant',
                    content: payload.followUp,
                    type: 'text'
                };
                addMessage(followUpMessage);
                appendMessageToDom(followUpMessage, messagesContainer);
            }
        } catch (error) {
            console.error(error);
            hideTyping();
            const failureMessage = {
                role: 'assistant',
                content: 'Omlouvám se, ale spojení se nezdařilo. Zkuste to prosím později.',
                type: 'text'
            };
            addMessage(failureMessage);
            appendMessageToDom(failureMessage, messagesContainer);
        }
    }

    async function sendEscalation({ email, messagesContainer }) {
        try {
            showTyping(messagesContainer);
            const response = await fetch('/Api/Chatbot?handler=Escalate', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    email,
                    history: state.history.map(({ role, content, type }) => ({ role, content, type }))
                })
            });

            hideTyping();

            if (!response.ok) {
                throw new Error(`Chyba serveru: ${response.status}`);
            }

            const payload = await response.json();
            const assistantMessage = {
                role: 'assistant',
                content: payload.reply ?? 'Děkujeme, kolega se vám ozve co nejdříve.',
                type: 'text'
            };
            addMessage(assistantMessage);
            appendMessageToDom(assistantMessage, messagesContainer);
        } catch (error) {
            console.error(error);
            hideTyping();
            const failureMessage = {
                role: 'assistant',
                content: 'Momentálně se nedaří odeslat požadavek. Napište nám prosím na info@sysjaky.cz.',
                type: 'text'
            };
            addMessage(failureMessage);
            appendMessageToDom(failureMessage, messagesContainer);
        }
    }

    function handleToggle(panel, toggle, messagesContainer) {
        const isHidden = panel.hasAttribute('hidden');
        if (isHidden) {
            panel.removeAttribute('hidden');
            panel.setAttribute('aria-modal', 'true');
            toggle.setAttribute('aria-expanded', 'true');
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
            setTimeout(() => {
                const input = panel.querySelector(selectors.input);
                input?.focus();
            }, 100);
        } else {
            panel.setAttribute('hidden', '');
            panel.setAttribute('aria-modal', 'false');
            toggle.setAttribute('aria-expanded', 'false');
        }
    }

    function showEscalationForm(form) {
        form.removeAttribute('hidden');
        const emailInput = form.querySelector('input[type="email"]');
        emailInput?.focus();
    }

    function hideEscalationForm(form) {
        form.setAttribute('hidden', '');
        form.reset();
    }

    function initialize() {
        if (state.initialized) {
            return;
        }

        const root = document.querySelector(selectors.root);
        if (!root) {
            return;
        }

        const toggle = root.querySelector(selectors.toggle);
        const panel = root.querySelector(selectors.panel);
        const close = root.querySelector(selectors.close);
        const messagesContainer = root.querySelector(selectors.messages);
        const form = root.querySelector(selectors.form);
        const input = root.querySelector(selectors.input);
        const quickRepliesContainer = root.querySelector(selectors.quickRepliesContainer);
        const escalateButton = root.querySelector(selectors.escalateButton);
        const escalateForm = root.querySelector(selectors.escalateForm);
        const escalateCancel = root.querySelector(selectors.escalateCancel);

        if (!toggle || !panel || !messagesContainer || !form || !input) {
            return;
        }

        state.history = loadHistory();
        if (state.history.length === 0) {
            const welcomeMessage = {
                role: 'assistant',
                content: 'Dobrý den! Jsem virtuální poradce. Zeptejte se na cokoliv ohledně našich kurzů.',
                type: 'text'
            };
            addMessage(welcomeMessage);
        }

        renderMessages(messagesContainer);

        toggle.addEventListener('click', () => handleToggle(panel, toggle, messagesContainer));
        close?.addEventListener('click', () => handleToggle(panel, toggle, messagesContainer));

        form.addEventListener('submit', event => {
            event.preventDefault();
            const value = input.value;
            if (!value.trim()) {
                input.focus();
                return;
            }
            sendMessage({ text: value, messagesContainer, input, form });
        });

        quickRepliesContainer?.addEventListener('click', event => {
            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }
            const value = target.getAttribute('data-quick-reply');
            if (!value) {
                return;
            }
            input.value = value;
            input.focus();
            sendMessage({ text: value, messagesContainer, input, form });
        });

        escalateButton?.addEventListener('click', () => {
            if (!escalateForm) {
                return;
            }
            showEscalationForm(escalateForm);
        });

        escalateCancel?.addEventListener('click', () => {
            if (!escalateForm) {
                return;
            }
            hideEscalationForm(escalateForm);
        });

        escalateForm?.addEventListener('submit', event => {
            event.preventDefault();
            const emailInput = escalateForm.querySelector('input[type="email"]');
            if (!(emailInput instanceof HTMLInputElement)) {
                return;
            }
            if (!emailInput.value || !emailInput.value.includes('@')) {
                emailInput.focus();
                return;
            }
            hideEscalationForm(escalateForm);
            sendEscalation({ email: emailInput.value, messagesContainer });
        });

        document.addEventListener('keydown', event => {
            if (event.key === 'Escape' && !panel.hasAttribute('hidden')) {
                handleToggle(panel, toggle, messagesContainer);
            }
        });

        state.initialized = true;
    }

    function bootstrap() {
        const root = document.querySelector(selectors.root);

        window.SysJakyChatbot = window.SysJakyChatbot || {};
        window.SysJakyChatbot.initialize = initialize;

        if (!root) {
            return;
        }

        const autoInitAttribute = root.getAttribute('data-chatbot-autoinit');
        const shouldAutoInit = autoInitAttribute === null || autoInitAttribute === '' || autoInitAttribute === 'true';

        if (shouldAutoInit) {
            initialize();
            return;
        }

        const toggle = root.querySelector(selectors.toggle);
        const panel = root.querySelector(selectors.panel);
        const messagesContainer = root.querySelector(selectors.messages);

        if (!toggle || !panel || !messagesContainer) {
            return;
        }

        const deferredInitHandler = () => {
            initialize();

            if (!state.initialized) {
                return;
            }

            handleToggle(panel, toggle, messagesContainer);
        };

        toggle.addEventListener('click', deferredInitHandler, { once: true });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bootstrap);
    } else {
        bootstrap();
    }
})();

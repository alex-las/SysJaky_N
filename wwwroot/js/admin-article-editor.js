(function () {
    const selectors = {
        textarea: '[data-editor-textarea]',
        container: '[data-editor-container]',
        preview: '[data-editor-preview]',
        toggle: '[data-editor-preview-toggle]'
    };

    function initializeEditor(textarea) {
        const container = document.querySelector(selectors.container);
        if (!container || typeof Quill === 'undefined') {
            return;
        }

        const form = textarea.closest('form');
        const preview = document.querySelector(selectors.preview);
        const toggleButton = document.querySelector(selectors.toggle);

        const quill = new Quill(container, {
            theme: 'snow',
            placeholder: textarea.getAttribute('placeholder') || '',
            modules: {
                toolbar: [
                    [{ header: [1, 2, 3, false] }],
                    ['bold', 'italic', 'underline', 'strike'],
                    [{ color: [] }, { background: [] }],
                    [{ list: 'ordered' }, { list: 'bullet' }],
                    [{ align: [] }],
                    ['link', 'blockquote', 'code-block'],
                    ['clean']
                ]
            }
        });

        if (textarea.value) {
            quill.clipboard.dangerouslyPasteHTML(textarea.value);
        }

        textarea.classList.add('d-none');

        const syncContent = () => {
            const html = container.querySelector('.ql-editor').innerHTML;
            textarea.value = html === '<p><br></p>' ? '' : html;
            if (preview) {
                preview.innerHTML = textarea.value;
            }
        };

        quill.on('text-change', syncContent);

        if (form) {
            form.addEventListener('submit', syncContent);
        }

        if (toggleButton && preview) {
            toggleButton.addEventListener('click', () => {
                const isHidden = preview.hasAttribute('hidden');
                if (isHidden) {
                    preview.removeAttribute('hidden');
                    toggleButton.setAttribute('aria-expanded', 'true');
                    toggleButton.textContent = toggleButton.dataset.labelHide;
                } else {
                    preview.setAttribute('hidden', 'hidden');
                    toggleButton.setAttribute('aria-expanded', 'false');
                    toggleButton.textContent = toggleButton.dataset.labelShow;
                }
            });
        }

        syncContent();
    }

    document.addEventListener('DOMContentLoaded', () => {
        const textarea = document.querySelector(selectors.textarea);
        if (textarea) {
            initializeEditor(textarea);
        }
    });
})();

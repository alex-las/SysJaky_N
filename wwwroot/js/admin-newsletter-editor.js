(function () {
    const selectors = {
        root: '[data-editor-root]',
        container: '[data-editor-container]',
        textarea: '[data-editor-textarea]',
        preview: '[data-editor-preview]',
        toggle: '[data-editor-preview-toggle]'
    };

    function initializeEditor(root) {
        if (!root || typeof Quill === 'undefined') {
            return;
        }

        if (root.dataset.editorInitialized === 'true') {
            return;
        }

        const container = root.querySelector(selectors.container);
        const textarea = root.querySelector(selectors.textarea);

        if (!container || !textarea) {
            return;
        }

        const form = textarea.closest('form');
        const preview = root.querySelector(selectors.preview);
        const toggle = root.querySelector(selectors.toggle);

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

        if (textarea.value && textarea.value.trim() !== '') {
            quill.clipboard.dangerouslyPasteHTML(textarea.value);
        }

        const syncContent = () => {
            const editorHtml = container.querySelector('.ql-editor')?.innerHTML ?? '';
            textarea.value = editorHtml === '<p><br></p>' ? '' : editorHtml;
            if (preview) {
                preview.innerHTML = textarea.value;
            }
        };

        quill.on('text-change', syncContent);

        if (form) {
            form.addEventListener('submit', syncContent);
        }

        if (toggle && preview) {
            toggle.addEventListener('click', () => {
                const hidden = preview.hasAttribute('hidden');
                if (hidden) {
                    preview.removeAttribute('hidden');
                    toggle.setAttribute('aria-expanded', 'true');
                    toggle.textContent = toggle.dataset.labelHide || toggle.textContent;
                    syncContent();
                } else {
                    preview.setAttribute('hidden', 'hidden');
                    toggle.setAttribute('aria-expanded', 'false');
                    toggle.textContent = toggle.dataset.labelShow || toggle.textContent;
                }
            });
        }

        textarea.classList.add('d-none');
        root.dataset.editorInitialized = 'true';
        syncContent();
    }

    function initializeEditors(context = document) {
        const roots = Array.from(context.querySelectorAll(selectors.root));
        roots.forEach(initializeEditor);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => initializeEditors(document));
    } else {
        initializeEditors(document);
    }
})();

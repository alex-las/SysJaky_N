(function () {
    const DEFAULT_TOAST_DURATION = 5000;

    const createId = () => `${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;

    document.addEventListener('alpine:init', () => {
        Alpine.store('toast', {
            items: [],
            show(message, type = 'info', duration = DEFAULT_TOAST_DURATION) {
                const id = createId();
                this.items.push({ id, message, type });

                if (duration > 0) {
                    window.setTimeout(() => {
                        this.remove(id);
                    }, duration);
                }
            },
            remove(id) {
                this.items = this.items.filter((item) => item.id !== id);
            }
        });

        Alpine.data('lightbox', () => ({
            isOpen: false,
            title: '',
            description: '',
            imageUrl: '',
            open(payload = {}) {
                this.title = payload.title || '';
                this.description = payload.description || '';
                this.imageUrl = payload.imageUrl || '';
                this.isOpen = true;
                this.focusFirst();
            },
            close() {
                this.isOpen = false;
                this.title = '';
                this.description = '';
                this.imageUrl = '';
            },
            focusFirst() {
                requestAnimationFrame(() => {
                    const dialog = this.$refs.dialog;
                    if (!dialog) {
                        return;
                    }

                    const focusable = dialog.querySelector('[data-initial-focus]');
                    if (focusable instanceof HTMLElement) {
                        focusable.focus({ preventScroll: true });
                    }
                });
            }
        }));
    });
})();

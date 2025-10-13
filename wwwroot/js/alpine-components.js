(function () {
    const DEFAULT_TOAST_DURATION = 5000;

    document.addEventListener('alpine:init', () => {
        Alpine.store('toast', {
            isOpen: false,
            message: '',
            type: 'info',
            timeoutId: null,
            show(message, options = {}) {
                const { type = 'info', duration = DEFAULT_TOAST_DURATION } = options;

                this.clearTimeout();
                this.message = message;
                this.type = type;
                this.isOpen = true;

                if (duration > 0) {
                    this.timeoutId = window.setTimeout(() => {
                        this.hide();
                    }, duration);
                }
            },
            hide() {
                this.clearTimeout();
                this.isOpen = false;
                this.message = '';
                this.type = 'info';
            },
            clearTimeout() {
                if (this.timeoutId) {
                    window.clearTimeout(this.timeoutId);
                    this.timeoutId = null;
                }
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

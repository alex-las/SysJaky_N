(function () {
    const DEFAULT_TOAST_DURATION = 5000;

    const normalizeOptions = (options) => ({
        type: 'info',
        duration: DEFAULT_TOAST_DURATION,
        ...options
    });

    document.addEventListener('alpine:init', () => {
        Alpine.store('toast', {
            isOpen: false,
            message: '',
            type: 'info',
            timeoutHandle: null,
            show(message, options = {}) {
                const settings = normalizeOptions(options);

                this.message = message;
                this.type = settings.type;
                this.isOpen = true;

                if (this.timeoutHandle) {
                    window.clearTimeout(this.timeoutHandle);
                }

                if (settings.duration > 0) {
                    this.timeoutHandle = window.setTimeout(() => {
                        this.hide();
                    }, settings.duration);
                }
            },
            hide() {
                this.isOpen = false;
                if (this.timeoutHandle) {
                    window.clearTimeout(this.timeoutHandle);
                    this.timeoutHandle = null;
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

const DEFAULT_TIMEOUT = 5000;
let toastId = 0;

document.addEventListener('alpine:init', () => {
    if (!window.Alpine) {
        return;
    }

    Alpine.store('toast', {
        items: [],
        add(message, options = {}) {
            const { type = 'info', title = '', timeout = DEFAULT_TIMEOUT } = options;
            const id = ++toastId;

            this.items.push({ id, message, type, title, timeout });

            if (timeout && Number.isFinite(timeout)) {
                window.setTimeout(() => this.remove(id), timeout);
            }

            return id;
        },
        remove(id) {
            const index = this.items.findIndex(toast => toast.id === id);
            if (index !== -1) {
                this.items.splice(index, 1);
            }
        }
    });
});

export function showToast(message, options) {
    if (!window.Alpine) {
        console.warn('Alpine.js is required to use the toast store.');
        return null;
    }

    return Alpine.store('toast').add(message, options);
}

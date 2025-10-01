// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', () => {
    if (window.bootstrap && typeof window.bootstrap.Popover === 'function') {
        document.querySelectorAll('[data-bs-toggle="popover"]').forEach((element) => {
            new window.bootstrap.Popover(element);
        });
    }

    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/service-worker.js').catch((error) => {
            console.warn('Service worker registration failed', error);
        });
    }
});

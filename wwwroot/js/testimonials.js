(function () {
    "use strict";

    class TestimonialsCarousel {
        constructor(root) {
            this.root = root;
            this.track = root.querySelector('[data-track]');
            this.items = Array.from(root.querySelectorAll('[data-item]'));
            this.prevControl = root.querySelector('[data-prev]');
            this.nextControl = root.querySelector('[data-next]');
            this.pagination = root.querySelector('[data-pagination]');
            this.viewport = root.querySelector('[data-viewport]');
            this.total = this.items.length;
            this.index = 0;
            this.intervalMs = Number.parseInt(root.getAttribute('data-autoplay-interval'), 10) || 7000;
            this.autoplayId = null;
            this.isHovered = false;
            this.touchStartX = null;
            this.touchDeltaX = 0;
            this.prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            this.resizeTimer = null;

            if (this.total === 0 || !this.track) {
                return;
            }

            this.bindEvents();
            this.updateDimensions();
            this.update();
            if (!this.prefersReducedMotion && this.total > 1) {
                this.startAutoplay();
            }
        }

        bindEvents() {
            if (this.prevControl) {
                this.prevControl.addEventListener('click', (event) => {
                    event.preventDefault();
                    this.prevSlide();
                });
            }
            if (this.nextControl) {
                this.nextControl.addEventListener('click', (event) => {
                    event.preventDefault();
                    this.nextSlide();
                });
            }

            this.root.addEventListener('mouseenter', () => {
                this.isHovered = true;
                this.stopAutoplay();
            });

            this.root.addEventListener('mouseleave', () => {
                this.isHovered = false;
                if (!this.prefersReducedMotion) {
                    this.startAutoplay();
                }
            });

            this.root.addEventListener('focusin', () => this.stopAutoplay());
            this.root.addEventListener('focusout', () => {
                if (!this.prefersReducedMotion && !this.isHovered && !this.root.contains(document.activeElement)) {
                    this.startAutoplay();
                }
            });

            this.root.addEventListener('keydown', (event) => this.handleKeydown(event));

            if (this.viewport) {
                this.viewport.addEventListener('touchstart', (event) => this.onTouchStart(event), { passive: true });
                this.viewport.addEventListener('touchmove', (event) => this.onTouchMove(event), { passive: true });
                this.viewport.addEventListener('touchend', (event) => this.onTouchEnd(event));
            }

            window.addEventListener('resize', () => this.onResize());
            document.addEventListener('visibilitychange', () => this.onVisibilityChange());
        }

        onResize() {
            window.clearTimeout(this.resizeTimer);
            this.resizeTimer = window.setTimeout(() => {
                this.updateDimensions();
                this.update();
            }, 150);
        }

        onVisibilityChange() {
            if (document.hidden) {
                this.stopAutoplay();
            } else if (!this.prefersReducedMotion && !this.isHovered) {
                this.startAutoplay();
            }
        }

        updateDimensions() {
            const width = (this.viewport || this.root).offsetWidth;
            if (!width) {
                return;
            }

            this.items.forEach((item) => {
                item.style.width = `${width}px`;
            });
        }

        update() {
            const width = (this.viewport || this.root).offsetWidth;
            this.track.style.transform = `translateX(-${width * this.index}px)`;

            this.items.forEach((item, idx) => {
                item.setAttribute('aria-hidden', idx === this.index ? 'false' : 'true');
            });

            if (this.pagination) {
                this.pagination.textContent = `${this.index + 1} / ${this.total}`;
            }
        }

        startAutoplay() {
            if (this.total <= 1) {
                return;
            }
            this.stopAutoplay();
            this.autoplayId = window.setInterval(() => {
                this.nextSlide();
            }, this.intervalMs);
        }

        stopAutoplay() {
            if (this.autoplayId) {
                window.clearInterval(this.autoplayId);
                this.autoplayId = null;
            }
        }

        nextSlide() {
            if (this.total === 0) {
                return;
            }
            this.index = (this.index + 1) % this.total;
            this.update();
        }

        prevSlide() {
            if (this.total === 0) {
                return;
            }
            this.index = (this.index - 1 + this.total) % this.total;
            this.update();
        }

        handleKeydown(event) {
            if (event.key === 'ArrowRight') {
                event.preventDefault();
                this.nextSlide();
            } else if (event.key === 'ArrowLeft') {
                event.preventDefault();
                this.prevSlide();
            }
        }

        onTouchStart(event) {
            if (event.touches.length !== 1) {
                return;
            }
            this.touchStartX = event.touches[0].clientX;
            this.touchDeltaX = 0;
            this.stopAutoplay();
        }

        onTouchMove(event) {
            if (this.touchStartX === null) {
                return;
            }
            this.touchDeltaX = event.touches[0].clientX - this.touchStartX;
        }

        onTouchEnd() {
            if (this.touchStartX === null) {
                return;
            }

            const threshold = 40;
            if (Math.abs(this.touchDeltaX) > threshold) {
                if (this.touchDeltaX < 0) {
                    this.nextSlide();
                } else {
                    this.prevSlide();
                }
            }

            this.touchStartX = null;
            this.touchDeltaX = 0;

            if (!this.prefersReducedMotion && !this.isHovered) {
                this.startAutoplay();
            }
        }
    }

    const init = () => {
        const carousels = document.querySelectorAll('.js-testimonials-carousel');
        carousels.forEach((root) => new TestimonialsCarousel(root));
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

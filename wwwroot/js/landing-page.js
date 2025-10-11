(() => {
  const docEl = document.documentElement;
  docEl.classList.add('js-enabled');

  const reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const revealTargets = document.querySelectorAll('[data-reveal]');

  if (!reduceMotion && 'IntersectionObserver' in window) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('is-visible');
          observer.unobserve(entry.target);
        }
      });
    }, {
      threshold: 0.12,
      rootMargin: '0px 0px -10%'
    });

    revealTargets.forEach((element) => {
      const delay = parseInt(element.getAttribute('data-reveal-delay') ?? '', 10);
      if (!Number.isNaN(delay)) {
        element.style.setProperty('--delay', `${delay}ms`);
      }
      observer.observe(element);
    });
  } else {
    revealTargets.forEach((element) => element.classList.add('is-visible'));
  }

  if (reduceMotion) {
    return;
  }

  const heroVisual = document.querySelector('.hero [data-float]');
  if (!heroVisual) {
    return;
  }

  const parallaxLayers = heroVisual.querySelectorAll('[data-parallax-layer]');
  const pointerCoarse = window.matchMedia && window.matchMedia('(pointer: coarse)').matches;

  const updateParallax = (xRatio, yRatio) => {
    if (!parallaxLayers.length) {
      return;
    }

    parallaxLayers.forEach((layer, index) => {
      const depth = (index + 1) / parallaxLayers.length;
      const translateX = xRatio * 24 * depth;
      const translateY = yRatio * 16 * depth;
      const baseRotation = layer.classList.contains('hero__gallery-item--secondary') ? 2 : layer.classList.contains('hero__gallery-item--primary') ? -2 : 1;
      layer.style.transform = `translate3d(${translateX}px, ${translateY}px, 0) rotate(${baseRotation}deg)`;
    });
  };

  if (!pointerCoarse && parallaxLayers.length) {
    heroVisual.addEventListener('mousemove', (event) => {
      const bounds = heroVisual.getBoundingClientRect();
      const xRatio = ((event.clientX - bounds.left) / bounds.width) - 0.5;
      const yRatio = ((event.clientY - bounds.top) / bounds.height) - 0.5;
      updateParallax(xRatio, yRatio);
    });

    heroVisual.addEventListener('mouseleave', () => {
      updateParallax(0, 0);
    });
  }

  let latestScrollY = window.scrollY;
  let ticking = false;

  const applyFloat = () => {
    const offset = latestScrollY * 0.04;
    heroVisual.style.transform = `translate3d(0, ${offset}px, 0)`;
    ticking = false;
  };

  const requestTick = () => {
    if (!ticking) {
      window.requestAnimationFrame(applyFloat);
      ticking = true;
    }
  };

  window.addEventListener('scroll', () => {
    latestScrollY = window.scrollY;
    requestTick();
  }, { passive: true });
})();

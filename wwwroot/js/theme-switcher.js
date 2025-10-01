const STORAGE_KEY = 'theme-preference';
const THEMES = {
    LIGHT: 'light',
    DARK: 'dark'
};

const DARK_META_COLOR = '#0f172a';
const LIGHT_META_COLOR = '#0b7bb3';

function getStoredPreference() {
    try {
        return localStorage.getItem(STORAGE_KEY);
    } catch (error) {
        return null;
    }
}

function storePreference(theme) {
    try {
        localStorage.setItem(STORAGE_KEY, theme);
    } catch (error) {
        // Ignore storage errors (e.g., private browsing modes).
    }
}

function getSystemPreference() {
    if (typeof window.matchMedia !== 'function') {
        return THEMES.LIGHT;
    }

    return window.matchMedia('(prefers-color-scheme: dark)').matches ? THEMES.DARK : THEMES.LIGHT;
}

function updateMetaThemeColor(theme) {
    const metaThemeColor = document.querySelector('meta[name="theme-color"]');

    if (!metaThemeColor) {
        return;
    }

    metaThemeColor.setAttribute('content', theme === THEMES.DARK ? DARK_META_COLOR : LIGHT_META_COLOR);
}

function toggleIcons(button, theme) {
    if (!button) {
        return;
    }

    button.setAttribute('aria-pressed', theme === THEMES.DARK ? 'true' : 'false');

    const icons = button.querySelectorAll('[data-theme-icon]');
    icons.forEach((icon) => {
        const isDarkIcon = icon.getAttribute('data-theme-icon') === THEMES.DARK;
        icon.classList.toggle('d-none', theme === THEMES.DARK ? !isDarkIcon : isDarkIcon);
    });
}

function reflectTheme(theme) {
    const root = document.documentElement;
    root.setAttribute('data-theme', theme);
    updateMetaThemeColor(theme);
    toggleIcons(document.querySelector('[data-theme-toggle]'), theme);
}

export function initThemeSwitcher() {
    let storedPreference = getStoredPreference();
    const mediaQueryList = typeof window.matchMedia === 'function'
        ? window.matchMedia('(prefers-color-scheme: dark)')
        : null;
    const initialTheme = storedPreference ?? getSystemPreference();

    reflectTheme(initialTheme);

    const toggleButton = document.querySelector('[data-theme-toggle]');

    if (toggleButton) {
        toggleButton.addEventListener('click', () => {
            const currentTheme = document.documentElement.getAttribute('data-theme') === THEMES.DARK ? THEMES.DARK : THEMES.LIGHT;
            const nextTheme = currentTheme === THEMES.DARK ? THEMES.LIGHT : THEMES.DARK;

            reflectTheme(nextTheme);
            storePreference(nextTheme);
            storedPreference = nextTheme;
        });
    }

    if (mediaQueryList) {
        const systemPreferenceListener = (event) => {
            if (storedPreference) {
                return;
            }

            reflectTheme(event.matches ? THEMES.DARK : THEMES.LIGHT);
        };

        if (typeof mediaQueryList.addEventListener === 'function') {
            mediaQueryList.addEventListener('change', systemPreferenceListener);
        } else if (typeof mediaQueryList.addListener === 'function') {
            mediaQueryList.addListener(systemPreferenceListener);
        }
    }
}

reflectTheme(getStoredPreference() ?? getSystemPreference());

initThemeSwitcher();

export default initThemeSwitcher;

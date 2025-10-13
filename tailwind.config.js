/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Pages/**/*.{cshtml,razor}",
    "./Areas/**/*.{cshtml,razor}",
    "./ViewComponents/**/*.{cshtml,razor}",
    "./TagHelpers/**/*.{cshtml,razor}",
    "./wwwroot/js/**/*.js"
  ],
  theme: {
    extend: {
      colors: {
        primary: {
          50: "#eef3ff",
          100: "#dce6ff",
          200: "#c0d1ff",
          300: "#9bb6ff",
          400: "#7396ff",
          500: "#4b78ff",
          600: "#255bf4",
          650: "#1f4fe0",
          700: "#1b42c4",
          800: "#16349d",
          900: "#0f246d"
        },
        accent: {
          50: "#fff9e6",
          100: "#fef2c7",
          200: "#fde59a",
          300: "#fbd667",
          400: "#f9c43c",
          500: "#f0ad1b",
          600: "#c98a0f"
        },
        neutral: {
          50: "#f8fafc",
          100: "#f1f5f9",
          150: "#e8edf7",
          200: "#e2e8f0",
          300: "#cbd5e1",
          400: "#94a3b8",
          500: "#64748b",
          600: "#475569",
          700: "#334155",
          800: "#1e293b",
          900: "#0f172a"
        },
        ink: "#0f172a",
        "ink-soft": "rgba(15, 23, 42, 0.78)"
      },
      fontFamily: {
        sans: ["Inter", "Segoe UI", "system-ui", "-apple-system", "BlinkMacSystemFont", "sans-serif"],
        serif: ["Merriweather", "Fraunces", "serif"],
        display: ["Fraunces", "Merriweather", "serif"],
        mono: [
          "JetBrains Mono",
          "SFMono-Regular",
          "Menlo",
          "Monaco",
          "Consolas",
          "Liberation Mono",
          "Courier New",
          "monospace"
        ]
      },
      borderRadius: {
        xs: "0.4rem",
        sm: "0.65rem",
        md: "0.9rem",
        lg: "1.25rem",
        xl: "1.65rem",
        "2xl": "2.1rem",
        "3xl": "2.75rem",
        pill: "999px"
      },
      boxShadow: {
        xs: "0 1px 2px rgba(15, 23, 42, 0.10)",
        sm: "0 4px 12px rgba(15, 23, 42, 0.12)",
        md: "0 12px 32px rgba(15, 23, 42, 0.16)",
        lg: "0 24px 48px rgba(15, 23, 42, 0.20)",
        xl: "0 36px 72px rgba(15, 23, 42, 0.24)",
        soft: "0 18px 50px rgba(37, 91, 244, 0.18)",
        glow: "0 0 0 6px rgba(37, 91, 244, 0.22)"
      }
    }
  },
  plugins: []
};

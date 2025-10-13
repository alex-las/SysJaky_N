/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./Pages/**/*.{cshtml,js}",
    "./Views/**/*.{cshtml,js}",
    "./Areas/**/*.{cshtml,js}",
    "./Components/**/*.{cshtml,js}",
    "./ViewComponents/**/*.{cshtml,js}",
    "./EmailTemplates/**/*.cshtml",
    "./wwwroot/js/**/*.js"
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          50: "#f4f8ff",
          100: "#e3edff",
          200: "#c4d8ff",
          300: "#99bbff",
          400: "#6a96ff",
          500: "#3d6dff",
          600: "#274deb",
          700: "#1c39c2",
          800: "#182f97",
          900: "#172b78"
        },
        accent: {
          50: "#fff7f5",
          100: "#ffe7e0",
          200: "#ffc7b8",
          300: "#ff9f80",
          400: "#ff6f47",
          500: "#ff4a1f",
          600: "#f23411",
          700: "#c3230d",
          800: "#931f12",
          900: "#781d13"
        },
        neutral: {
          50: "#f9fafb",
          100: "#f2f4f7",
          200: "#e5e7eb",
          300: "#d2d6dc",
          400: "#9aa1b1",
          500: "#6b7280",
          600: "#4b5563",
          700: "#374151",
          800: "#1f2933",
          900: "#121826"
        }
      },
      fontFamily: {
        sans: ["'Inter'", "'Segoe UI'", "system-ui", "sans-serif"],
        display: ["'Manrope'", "'Poppins'", "'Segoe UI'", "sans-serif"],
        mono: ["'Fira Code'", "'SFMono-Regular'", "monospace"]
      },
      borderRadius: {
        none: "0",
        sm: "0.25rem",
        DEFAULT: "0.75rem",
        lg: "1rem",
        xl: "1.5rem",
        '2xl': "2rem",
        pill: "9999px"
      },
      boxShadow: {
        elevated: "0 20px 50px -25px rgba(15, 23, 42, 0.45)",
        soft: "0 10px 30px -15px rgba(15, 23, 42, 0.25)"
      }
    }
  },
  plugins: []
};

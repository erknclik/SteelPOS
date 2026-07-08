/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        brand: {
          50: "#eef4ff",
          100: "#dbe6fe",
          500: "#4f6ef7",
          600: "#3b4fe0",
          700: "#3140b4",
          900: "#232a66",
        },
      },
    },
  },
  plugins: [],
};

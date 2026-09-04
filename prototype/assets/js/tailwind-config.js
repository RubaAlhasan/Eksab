// Eksabli — shared Tailwind CDN config.
// Load order in every page's <head>:
//   1. <script src="assets/js/tailwind-config.js"></script>
//   2. <script src="https://cdn.tailwindcss.com"></script>
//   3. <script>tailwind.config = window.EksabliTailwindConfig;</script>
//
// Kept in sync with angular/src/styles/_tokens.scss, which is the source of truth for the real
// app's palette — same violet ramp, same warm neutral ramp, same categorical chart slots.
//
// Note on `slate`: the prototype's pages use `slate-*` for every neutral (383 uses of `slate-400`
// alone), so rather than rewrite ~67 HTML files the whole ramp is REDEFINED here as a warm gray.
// Each step is lightness-matched to the Tailwind slate it replaces (max drift 1.9% relative
// luminance), so this is a pure hue shift — `bg-slate-50`, `dark:bg-slate-950` etc. keep their
// existing role and simply stop fighting the violet accent the way a blue-gray does.
window.EksabliTailwindConfig = {
  darkMode: 'class',
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      },
      colors: {
        // Warm neutral ramp replacing Tailwind's stock (cool) `slate` — see header note.
        slate: {
          50: '#FAF9F7', 100: '#F5F3EF', 200: '#EBE7E1', 300: '#D8D3CA',
          400: '#A7A196', 500: '#756F66', 600: '#5B564D', 700: '#433E37',
          800: '#2C2823', 900: '#1B1815', 950: '#0D0B09',
        },
        // Categorical chart series — validated for CVD separation in both modes. Deliberately
        // excludes the brand violet: `primary` means "actionable", and a chart bar is not a button.
        chart: {
          1: '#2A78D6', 2: '#EB6834', 3: '#1BAF7A',
          4: '#EDA100', 5: '#E87BA4', 6: '#008300',
        },
        primary: {
          50: '#F4F3FF', 100: '#EBE9FE', 200: '#D9D6FD', 300: '#BEB8FB',
          400: '#9D8FF8', 500: '#7C6AF0', 600: '#6248E3', 700: '#4F37C4',
          800: '#422F9E', 900: '#392A7D', 950: '#241A52',
        },
        success: { 50: '#ECFDF5', 100: '#D1FAE5', 200: '#A7F3D0', 500: '#10B981', 600: '#059669', 700: '#047857' },
        warning: { 50: '#FFFBEB', 100: '#FEF3C7', 200: '#FDE68A', 500: '#F59E0B', 600: '#D97706', 700: '#B45309' },
        danger:  { 50: '#FEF2F2', 100: '#FEE2E2', 200: '#FECACA', 500: '#EF4444', 600: '#DC2626', 700: '#B91C1C' },
        info:    { 50: '#F0F9FF', 100: '#E0F2FE', 200: '#BAE6FD', 500: '#0EA5E9', 600: '#0284C7', 700: '#0369A1' },
      },
      borderRadius: { '2xl': '1rem', '3xl': '1.5rem', '4xl': '2rem' },
      boxShadow: {
        soft: '0 1px 2px 0 rgba(27,24,21,0.05), 0 8px 24px -8px rgba(27,24,21,0.12)',
        'soft-lg': '0 2px 4px 0 rgba(27,24,21,0.05), 0 20px 48px -12px rgba(27,24,21,0.20)',
        'soft-dark': '0 1px 2px 0 rgba(0,0,0,0.3), 0 8px 24px -8px rgba(0,0,0,0.5)',
        'soft-dark-lg': '0 2px 4px 0 rgba(0,0,0,0.3), 0 20px 48px -12px rgba(0,0,0,0.65)',
      },
      keyframes: {
        fadeUp: { '0%': { opacity: 0, transform: 'translateY(8px)' }, '100%': { opacity: 1, transform: 'translateY(0)' } },
        scaleIn: { '0%': { opacity: 0, transform: 'scale(.96)' }, '100%': { opacity: 1, transform: 'scale(1)' } },
        slideInRight: { '0%': { opacity: 0, transform: 'translateX(16px)' }, '100%': { opacity: 1, transform: 'translateX(0)' } },
        slideUpSheet: { '0%': { transform: 'translateY(100%)' }, '100%': { transform: 'translateY(0)' } },
      },
      animation: {
        'fade-up': 'fadeUp .35s ease-out both',
        'scale-in': 'scaleIn .2s ease-out both',
        'slide-in-right': 'slideInRight .3s ease-out both',
        'slide-up-sheet': 'slideUpSheet .25s ease-out both',
      },
    },
  },
};

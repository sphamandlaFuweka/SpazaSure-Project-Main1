/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        primary: {
          DEFAULT: '#1B4332',
          50: '#E8F5E9',
          100: '#C8E6C9',
          200: '#A5D6A7',
          300: '#81C784',
          400: '#4CAF50',
          500: '#2E7D52',
          600: '#1B4332',
          700: '#143326',
          800: '#0D2218',
          900: '#071109',
        },
        accent: {
          DEFAULT: '#F59E0B',
          50: '#FFFBEB',
          100: '#FEF3C7',
          400: '#FBBF24',
          500: '#F59E0B',
          600: '#D97706',
          700: '#B45309',
        },
        surface: '#F4F7F5',
        card: '#FFFFFF',
      },
      fontFamily: {
        sans: ['Poppins', 'Inter', 'system-ui', 'sans-serif'],
      },
      boxShadow: {
        card: '0 1px 3px 0 rgba(0,0,0,0.06), 0 1px 2px -1px rgba(0,0,0,0.04)',
        'card-hover': '0 8px 24px 0 rgba(0,0,0,0.10), 0 2px 8px -1px rgba(0,0,0,0.06)',
        'card-lg': '0 12px 32px 0 rgba(0,0,0,0.10)',
        'card-xl': '0 20px 48px 0 rgba(0,0,0,0.12)',
        sidebar: '4px 0 24px 0 rgba(0,0,0,0.12)',
        glow: '0 0 20px rgba(27,67,50,0.25)',
        'glow-accent': '0 0 20px rgba(245,158,11,0.30)',
        'inner-sm': 'inset 0 1px 3px rgba(0,0,0,0.06)',
      },
      backgroundImage: {
        'gradient-primary': 'linear-gradient(135deg, #1B4332 0%, #2E7D52 50%, #4CAF50 100%)',
        'gradient-card': 'linear-gradient(135deg, #1B4332 0%, #2E7D52 100%)',
        'gradient-gold': 'linear-gradient(135deg, #B45309 0%, #F59E0B 100%)',
        'gradient-surface': 'linear-gradient(180deg, #F4F7F5 0%, #FFFFFF 100%)',
        'gradient-mesh': 'radial-gradient(at 40% 20%, hsla(140,60%,20%,0.08) 0px, transparent 50%), radial-gradient(at 80% 0%, hsla(45,90%,55%,0.06) 0px, transparent 50%)',
        'gradient-shimmer': 'linear-gradient(90deg, transparent 0%, rgba(255,255,255,0.4) 50%, transparent 100%)',
      },
      animation: {
        'fade-in':        'fadeIn 0.4s cubic-bezier(0.16,1,0.3,1)',
        'slide-up':       'slideUp 0.4s cubic-bezier(0.16,1,0.3,1)',
        'slide-in':       'slideIn 0.3s cubic-bezier(0.16,1,0.3,1)',
        'pulse-soft':     'pulseSoft 3s ease-in-out infinite',
        'shimmer':        'shimmer 2s infinite',
        'bounce-sm':      'bounceSm 3s ease-in-out infinite',
        'scale-in':       'scaleIn 0.3s cubic-bezier(0.34,1.56,0.64,1)',
        'count-up':       'fadeIn 0.6s ease-out',
        // Staggered entry animations
        'stagger-1':      'staggerIn 0.5s cubic-bezier(0.16,1,0.3,1) 0.05s both',
        'stagger-2':      'staggerIn 0.5s cubic-bezier(0.16,1,0.3,1) 0.1s both',
        'stagger-3':      'staggerIn 0.5s cubic-bezier(0.16,1,0.3,1) 0.15s both',
        'stagger-4':      'staggerIn 0.5s cubic-bezier(0.16,1,0.3,1) 0.2s both',
        'stagger-5':      'staggerIn 0.5s cubic-bezier(0.16,1,0.3,1) 0.25s both',
        'stagger-6':      'staggerIn 0.5s cubic-bezier(0.16,1,0.3,1) 0.3s both',
        // Float animation
        'float':          'float 6s ease-in-out infinite',
        'float-slow':     'float 8s ease-in-out infinite',
        // Subtle breathing
        'breathe':        'breathe 4s ease-in-out infinite',
        // Number counter
        'number-pop':     'numberPop 0.6s cubic-bezier(0.34,1.56,0.64,1)',
        // Login-specific
        'orb-drift-1':    'orbDrift1 12s ease-in-out infinite',
        'orb-drift-2':    'orbDrift2 16s ease-in-out infinite',
        'orb-drift-3':    'orbDrift3 20s ease-in-out infinite',
        'particle-float': 'particleFloat 6s ease-in-out infinite',
        'gradient-shift': 'gradientShift 8s ease infinite',
        'glow-pulse':     'glowPulse 2.5s ease-in-out infinite',
        'card-glow':      'cardGlow 3s ease-in-out infinite',
        'btn-shimmer':    'btnShimmer 2.5s ease-in-out infinite',
        'spin-slow':      'spin 8s linear infinite',
        'counter-up':     'counterUp 0.6s cubic-bezier(0.16,1,0.3,1) forwards',
        'slide-left':     'slideLeft 0.7s cubic-bezier(0.16,1,0.3,1) forwards',
        'slide-right':    'slideRight 0.7s cubic-bezier(0.16,1,0.3,1) forwards',
        'slide-down':     'slideDown 0.7s cubic-bezier(0.16,1,0.3,1) forwards',
        'pop-in':         'popIn 0.5s cubic-bezier(0.34,1.56,0.64,1) forwards',
        'badge-pulse':    'badgePulse 2s ease-in-out infinite',
      },
      keyframes: {
        fadeIn:        { from: { opacity: '0' },                                          to: { opacity: '1' } },
        slideUp:       { from: { opacity: '0', transform: 'translateY(16px)' },           to: { opacity: '1', transform: 'translateY(0)' } },
        slideIn:       { from: { opacity: '0', transform: 'translateX(-8px)' },           to: { opacity: '1', transform: 'translateX(0)' } },
        pulseSoft:     { '0%,100%': { opacity: '1' },                                     '50%': { opacity: '0.5' } },
        shimmer:       { '0%': { transform: 'translateX(-100%)' },                        '100%': { transform: 'translateX(100%)' } },
        bounceSm:      { '0%,100%': { transform: 'translateY(0)' },                       '50%': { transform: 'translateY(-6px)' } },
        scaleIn:       { from: { opacity: '0', transform: 'scale(0.92)' },                to: { opacity: '1', transform: 'scale(1)' } },
        // Staggered entry — elements fly up with subtle scale
        staggerIn: {
          from: { opacity: '0', transform: 'translateY(20px) scale(0.96)' },
          to:   { opacity: '1', transform: 'translateY(0) scale(1)' },
        },
        // Floating — gentle up/down drift
        float: {
          '0%,100%': { transform: 'translateY(0px)' },
          '50%':     { transform: 'translateY(-8px)' },
        },
        // Subtle breathing scale
        breathe: {
          '0%,100%': { transform: 'scale(1)' },
          '50%':     { transform: 'scale(1.02)' },
        },
        // Number pop — for KPI values
        numberPop: {
          '0%':   { opacity: '0', transform: 'scale(0.5) translateY(10px)' },
          '60%':  { transform: 'scale(1.1) translateY(-2px)' },
          '100%': { opacity: '1', transform: 'scale(1) translateY(0)' },
        },
        // Orbs drift around independently
        orbDrift1: {
          '0%,100%': { transform: 'translate(0px, 0px) scale(1)' },
          '33%':     { transform: 'translate(40px, -30px) scale(1.1)' },
          '66%':     { transform: 'translate(-20px, 20px) scale(0.95)' },
        },
        orbDrift2: {
          '0%,100%': { transform: 'translate(0px, 0px) scale(1)' },
          '40%':     { transform: 'translate(-50px, 30px) scale(1.15)' },
          '70%':     { transform: 'translate(30px, -40px) scale(0.9)' },
        },
        orbDrift3: {
          '0%,100%': { transform: 'translate(0px, 0px) scale(1)' },
          '50%':     { transform: 'translate(25px, 35px) scale(1.2)' },
        },
        // Particles float up and fade
        particleFloat: {
          '0%':   { transform: 'translateY(0px) translateX(0px)', opacity: '0' },
          '10%':  { opacity: '1' },
          '90%':  { opacity: '0.6' },
          '100%': { transform: 'translateY(-80px) translateX(15px)', opacity: '0' },
        },
        // Background gradient morphs
        gradientShift: {
          '0%,100%': { backgroundPosition: '0% 50%' },
          '50%':     { backgroundPosition: '100% 50%' },
        },
        // Green glow pulse on dot
        glowPulse: {
          '0%,100%': { boxShadow: '0 0 4px 1px rgba(76,175,80,0.6)' },
          '50%':     { boxShadow: '0 0 12px 4px rgba(76,175,80,0.9)' },
        },
        // Card border glow
        cardGlow: {
          '0%,100%': { boxShadow: '0 8px 40px rgba(0,0,0,0.10), 0 0 0 1px rgba(76,175,80,0)' },
          '50%':     { boxShadow: '0 8px 40px rgba(0,0,0,0.14), 0 0 0 1px rgba(76,175,80,0.15)' },
        },
        // Button shimmer sweep
        btnShimmer: {
          '0%':   { backgroundPosition: '-200% center' },
          '100%': { backgroundPosition: '200% center' },
        },
        // Entrance animations with spring
        counterUp:  { from: { opacity: '0', transform: 'translateY(16px) scale(0.9)' }, to: { opacity: '1', transform: 'translateY(0) scale(1)' } },
        slideLeft:  { from: { opacity: '0', transform: 'translateX(-28px)' },           to: { opacity: '1', transform: 'translateX(0)' } },
        slideRight: { from: { opacity: '0', transform: 'translateX(28px)' },            to: { opacity: '1', transform: 'translateX(0)' } },
        slideDown:  { from: { opacity: '0', transform: 'translateY(-20px)' },           to: { opacity: '1', transform: 'translateY(0)' } },
        popIn:      { from: { opacity: '0', transform: 'scale(0.8)' },                  to: { opacity: '1', transform: 'scale(1)' } },
        badgePulse: {
          '0%,100%': { transform: 'scale(1)',    boxShadow: '0 0 0 0 rgba(76,175,80,0.4)' },
          '50%':     { transform: 'scale(1.02)', boxShadow: '0 0 0 6px rgba(76,175,80,0)' },
        },
      },
      borderRadius: {
        xl: '0.875rem',
        '2xl': '1.25rem',
      },
    },
  },
  plugins: [],
};

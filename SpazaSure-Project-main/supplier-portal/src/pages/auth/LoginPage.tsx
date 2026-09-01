import { useState, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import toast from 'react-hot-toast';
import { Eye, EyeOff, ArrowRight, Shield, Package, TrendingUp, ShieldCheck, Users, Star } from 'lucide-react';
import { useAuthStore } from '../../store/authStore';
import { Spinner } from '../../components/ui';
import { authApi, profileApi } from '../../services/api';
import clsx from 'clsx';

/* ─── Schema ─── */
const schema = z.object({
  email: z.string().email('Invalid email address'),
  password: z.string().min(6, 'Password must be at least 6 characters'),
});
type FormData = z.infer<typeof schema>;

/* ─── Feature cards data ─── */
const features = [
  { icon: Package, title: 'Product Catalog', desc: 'Manage inventory with bulk pricing & smart restock alerts' },
  { icon: TrendingUp, title: 'Live Analytics', desc: 'Real-time revenue, orders & growth insights' },
  { icon: ShieldCheck, title: 'Compliance Hub', desc: 'CIPC, POPIA & document verification' },
  { icon: Users, title: 'Shop Network', desc: 'Connect with 1,000+ verified spaza shops' },
];

/* ─── Floating particles config ─── */
const PARTICLES = [
  { top: '10%', left: '12%', size: 3, delay: '0s', dur: '7s' },
  { top: '25%', left: '80%', size: 2, delay: '1.2s', dur: '9s' },
  { top: '55%', left: '18%', size: 4, delay: '2.4s', dur: '6s' },
  { top: '75%', left: '85%', size: 2, delay: '0.8s', dur: '11s' },
  { top: '40%', left: '60%', size: 3, delay: '3s', dur: '8s' },
  { top: '85%', left: '35%', size: 2, delay: '1.5s', dur: '10s' },
  { top: '15%', left: '50%', size: 3, delay: '4s', dur: '7.5s' },
  { top: '65%', left: '70%', size: 2, delay: '2s', dur: '9.5s' },
];

/* ─── Main Component ─── */
export default function LoginPage() {
  const [showPw, setShowPw] = useState(false);
  const [loading, setLoading] = useState(false);
  const [activeRole, setActiveRole] = useState<'supplier' | 'admin'>('supplier');
  const [ready, setReady] = useState(false);
  const { setUser } = useAuthStore();
  const navigate = useNavigate();

  useEffect(() => { const t = setTimeout(() => setReady(true), 50); return () => clearTimeout(t); }, []);

  const { register, handleSubmit, setValue, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const switchRole = (role: 'supplier' | 'admin') => {
    setActiveRole(role);
    setValue('email', '');
    setValue('password', '');
  };

  const onSubmit = async (data: FormData) => {
    setLoading(true);
    try {
      const res = await authApi.login(data.email, data.password, activeRole);
      if (!res.success) { toast.error(res.message || 'Login failed'); return; }
      const { accessToken, refreshToken, expiresAt, role, userId } = res.data;
      const userRole: 'supplier' | 'admin' = role === 'admin' ? 'admin' : 'supplier';
      if (userRole !== activeRole) {
        toast.error(
          activeRole === 'admin'
            ? 'This account is not an admin. Please use the Supplier Login tab.'
            : 'This account is an admin. Please use the Admin Login tab.'
        );
        return;
      }
      setUser({
        id: String(userId),
        email: data.email,
        companyName: userRole === 'admin' ? 'SpazaSure Admin' : '',
        role: userRole,
        tier: userRole === 'admin' ? 'gold' : 'basic',
        isVerified: userRole === 'admin',
        token: accessToken,
        refreshToken,
        tokenExpiresAt: typeof expiresAt === 'string' ? expiresAt : new Date(expiresAt).toISOString(),
      });

      // AuthResponse only carries token + role + userId — it doesn't include
      // companyName/tier/logoUrl, so the header/sidebar would otherwise show
      // a blank name and a hardcoded "Basic" badge forever. Hydrate the real
      // values from the profile endpoint now that we have a token to call it
      // with. Login still succeeds even if this fetch fails.
      if (userRole === 'supplier') {
        try {
          const profile = await profileApi.get();
          setUser({
            id: String(userId),
            email: profile.email || data.email,
            companyName: profile.companyName,
            role: userRole,
            tier: profile.tier,
            isVerified: profile.isVerified,
            logoUrl: profile.logoUrl,
            token: accessToken,
            refreshToken,
            tokenExpiresAt: typeof expiresAt === 'string' ? expiresAt : new Date(expiresAt).toISOString(),
          });
        } catch {
          // Non-fatal — dashboard will just show a blank company name/tier
          // until the Profile page is visited and refreshes the store.
        }
      }

      toast.success('Welcome back!');
      setTimeout(() => navigate(userRole === 'admin' ? '/admin/dashboard' : '/dashboard'), 50);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg || 'Invalid credentials. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  /* Stagger animation helper */
  const stagger = (delay: number) =>
    ready
      ? { opacity: 1, transform: 'translateY(0)', transition: `all 0.7s cubic-bezier(0.16,1,0.3,1) ${delay}ms` }
      : { opacity: 0, transform: 'translateY(24px)' };

  return (
    <div className="login-page h-screen w-screen flex overflow-hidden">

      {/* ═══════════════════════════════════════════════════════
          LEFT PANEL — Dark luxury brand showcase (55% desktop)
          ═══════════════════════════════════════════════════════ */}
      <div className="hidden lg:flex lg:w-[55%] relative flex-col bg-gradient-to-br from-[#0A1A0F] via-[#1B4332] to-[#0D2E1A] overflow-hidden">

        {/* Gradient orb decorations */}
        <div className="absolute top-[-80px] left-[-60px] w-[400px] h-[400px] rounded-full bg-[#4CAF50]/8 blur-[100px] login-orb-1" />
        <div className="absolute bottom-[-60px] right-[-40px] w-[350px] h-[350px] rounded-full bg-[#2E7D52]/12 blur-[90px] login-orb-2" />
        <div className="absolute top-[45%] left-[50%] w-[250px] h-[250px] rounded-full bg-[#F59E0B]/5 blur-[80px] login-orb-3" />

        {/* Subtle grid overlay */}
        <div className="absolute inset-0 opacity-[0.03]" style={{
          backgroundImage: 'linear-gradient(rgba(255,255,255,0.5) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.5) 1px, transparent 1px)',
          backgroundSize: '48px 48px',
        }} />

        {/* Floating particles */}
        {PARTICLES.map((p, i) => (
          <div
            key={i}
            className="absolute rounded-full bg-[#4CAF50]/25 login-particle pointer-events-none"
            style={{
              width: p.size,
              height: p.size,
              top: p.top,
              left: p.left,
              animationDelay: p.delay,
              animationDuration: p.dur,
            }}
          />
        ))}

        {/* Content container */}
        <div className="relative z-10 flex flex-col justify-between h-full px-12 py-10">

          {/* Logo header */}
          <div style={stagger(0)} className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl overflow-hidden ring-2 ring-white/20 shadow-lg">
              <img src="/spazasure_logo.jpg" alt="SpazaSure" className="w-full h-full object-cover" />
            </div>
            <div>
              <h1 className="text-lg font-black text-white tracking-tight">SpazaSure</h1>
              <p className="text-[9px] font-bold tracking-[0.25em] uppercase text-[#4CAF50]/80">Supplier Portal</p>
            </div>
          </div>

          {/* ═══ 3D ANIMATED LOGO HERO ═══ */}
          <div style={stagger(100)} className="flex flex-col items-center justify-center py-4">
            <div className="login-3d-logo-wrapper relative">
              {/* Outer glow rings */}
              <div className="login-3d-ring login-3d-ring-1" />
              <div className="login-3d-ring login-3d-ring-2" />
              <div className="login-3d-ring login-3d-ring-3" />

              {/* Orbiting dots */}
              <div className="login-3d-orbit">
                <div className="login-3d-orbit-dot login-3d-orbit-dot-1" />
                <div className="login-3d-orbit-dot login-3d-orbit-dot-2" />
                <div className="login-3d-orbit-dot login-3d-orbit-dot-3" />
                <div className="login-3d-orbit-dot login-3d-orbit-dot-4" />
              </div>

              {/* Main logo — 3D perspective */}
              <div className="login-3d-logo">
                <div className="login-3d-logo-inner">
                  <img src="/spazasure_logo.jpg" alt="SpazaSure" className="w-full h-full object-cover rounded-3xl" />
                </div>
              </div>

              {/* Reflection */}
              <div className="login-3d-reflection" />
            </div>

            {/* Brand text below logo */}
            <div className="mt-5 text-center">
              <h2 className="text-2xl font-black text-white tracking-tight login-3d-text-shimmer">
                SPAZASURE
              </h2>
              <p className="text-[10px] font-bold tracking-[0.4em] uppercase text-[#4CAF50] mt-1 login-3d-subtitle">
                SUPPLIER PORTAL
              </p>
            </div>
          </div>

          {/* Headline */}
          <div style={stagger(200)}>
            <h2 className="text-[2.2rem] font-black text-white leading-[1.08] tracking-tight mb-3">
              South Africa's #1<br />
              <span className="text-transparent bg-clip-text" style={{
                backgroundImage: 'linear-gradient(135deg, #4CAF50, #81C784, #F59E0B)',
              }}>
                B2B Spaza Platform
              </span>
            </h2>
            <p className="text-[#A7C4B0] text-sm leading-relaxed max-w-sm">
              Connect with thousands of verified spaza shops. Manage orders, track payments, and grow revenue — all in one platform.
            </p>
          </div>

          {/* Feature cards — 2x2 grid */}
          <div className="grid grid-cols-2 gap-3">
            {features.map(({ icon: Icon, title, desc }, i) => (
              <div
                key={title}
                style={stagger(250 + i * 80)}
                className="group bg-white/[0.05] hover:bg-white/[0.09] border border-white/[0.10] hover:border-[#4CAF50]/30 rounded-2xl p-4 transition-all duration-300 hover:-translate-y-0.5 hover:shadow-[0_8px_32px_rgba(76,175,80,0.12)] backdrop-blur-sm"
              >
                <div className="w-9 h-9 rounded-xl bg-[#4CAF50]/15 flex items-center justify-center mb-2.5 group-hover:bg-[#4CAF50]/25 group-hover:scale-110 transition-all duration-300">
                  <Icon size={16} className="text-[#4CAF50]" />
                </div>
                <p className="text-white text-xs font-bold mb-0.5">{title}</p>
                <p className="text-[#7A9E8A] text-[11px] leading-snug">{desc}</p>
              </div>
            ))}
          </div>

          {/* Stats bar */}
          <div style={stagger(600)} className="bg-white/[0.04] border border-white/[0.08] rounded-2xl px-5 py-3.5 backdrop-blur-sm">
            <div className="flex items-center justify-between text-center">
              {[
                { val: '1,000+', label: 'Shops' },
                { val: 'R5M+', label: 'GMV' },
                { val: '100+', label: 'Suppliers' },
                { val: '4.9★', label: 'Rating' },
              ].map(({ val, label }, i) => (
                <div key={label} className="flex-1">
                  <p className="text-white font-black text-sm">{val}</p>
                  <p className="text-[#7A9E8A] text-[10px] font-medium mt-0.5">{label}</p>
                  {i < 3 && <div className="absolute" />}
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════
          RIGHT PANEL — Login form (45% desktop, full mobile)
          ═══════════════════════════════════════════════════════ */}
      <div className="flex-1 flex items-center justify-center bg-[#F8FAF9] relative overflow-y-auto">

        {/* Subtle background decoration */}
        <div className="absolute top-0 right-0 w-[300px] h-[300px] rounded-full bg-[#4CAF50]/[0.03] blur-[80px]" />
        <div className="absolute bottom-0 left-0 w-[250px] h-[250px] rounded-full bg-[#1B4332]/[0.04] blur-[60px]" />

        <div className="w-full max-w-[400px] px-6 py-10 lg:py-0">

          {/* Mobile logo (hidden on desktop) */}
          <div style={stagger(0)} className="lg:hidden flex flex-col items-center mb-8">
            <div className="w-16 h-16 rounded-2xl overflow-hidden ring-2 ring-[#4CAF50]/20 shadow-xl mb-3">
              <img src="/spazasure_logo.jpg" alt="SpazaSure" className="w-full h-full object-cover" />
            </div>
            <h1 className="text-xl font-black text-gray-900 tracking-tight">SpazaSure</h1>
            <p className="text-[10px] font-bold tracking-[0.2em] uppercase text-[#4CAF50]">Supplier Portal</p>
          </div>

          {/* Role toggle pills */}
          <div style={stagger(80)} className="flex bg-gray-100 rounded-xl p-1 mb-6">
            {(['supplier', 'admin'] as const).map((role) => (
              <button
                key={role}
                type="button"
                onClick={() => switchRole(role)}
                className={clsx(
                  'flex-1 py-2.5 text-sm font-semibold rounded-lg transition-all duration-300',
                  activeRole === role
                    ? 'bg-gradient-to-r from-[#1B4332] to-[#2E7D52] text-white shadow-lg shadow-[#1B4332]/20'
                    : 'text-gray-500 hover:text-gray-700'
                )}
              >
                {role === 'supplier' ? '🏪 Supplier' : '🛡️ Admin'}
              </button>
            ))}
          </div>

          {/* Form card */}
          <div
            style={stagger(160)}
            className="bg-white rounded-3xl shadow-2xl shadow-gray-200/60 p-8 login-card-shadow"
          >
            {/* Title */}
            <div className="mb-6">
              <h2 className="text-2xl font-black text-gray-900 tracking-tight">
                {activeRole === 'admin' ? 'Welcome back, Admin' : 'Welcome back'}
              </h2>
              <p className="text-gray-500 text-sm mt-1">
                {activeRole === 'admin' ? 'Sign in to your admin dashboard' : 'Sign in to your supplier account'}
              </p>
            </div>

            <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
              {/* Email */}
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1.5">Email address</label>
                <input
                  type="email"
                  {...register('email')}
                  placeholder="you@company.co.za"
                  className={clsx(
                    'w-full px-4 py-3 rounded-xl border text-sm transition-all duration-200 outline-none',
                    'bg-gray-50 focus:bg-white',
                    errors.email
                      ? 'border-red-300 focus:border-red-400 focus:ring-2 focus:ring-red-100'
                      : 'border-gray-200 focus:border-[#4CAF50] focus:ring-2 focus:ring-[#4CAF50]/10'
                  )}
                />
                {errors.email && (
                  <p className="text-red-500 text-xs mt-1 font-medium">{errors.email.message}</p>
                )}
              </div>

              {/* Password */}
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1.5">Password</label>
                <div className="relative">
                  <input
                    type={showPw ? 'text' : 'password'}
                    {...register('password')}
                    placeholder="••••••••"
                    className={clsx(
                      'w-full px-4 py-3 pr-11 rounded-xl border text-sm transition-all duration-200 outline-none',
                      'bg-gray-50 focus:bg-white',
                      errors.password
                        ? 'border-red-300 focus:border-red-400 focus:ring-2 focus:ring-red-100'
                        : 'border-gray-200 focus:border-[#4CAF50] focus:ring-2 focus:ring-[#4CAF50]/10'
                    )}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPw(!showPw)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 transition-colors"
                    tabIndex={-1}
                  >
                    {showPw ? <EyeOff size={18} /> : <Eye size={18} />}
                  </button>
                </div>
                {errors.password && (
                  <p className="text-red-500 text-xs mt-1 font-medium">{errors.password.message}</p>
                )}
              </div>

              {/* Forgot password */}
              <div className="flex justify-end">
                <Link
                  to="/forgot-password"
                  className="text-xs font-semibold text-[#2E7D52] hover:text-[#1B4332] transition-colors"
                >
                  Forgot password?
                </Link>
              </div>

              {/* Submit button */}
              <button
                type="submit"
                disabled={loading}
                className="login-btn-shimmer relative w-full py-3.5 rounded-xl font-bold text-white text-sm overflow-hidden transition-all duration-300 hover:shadow-lg hover:shadow-[#1B4332]/25 hover:-translate-y-0.5 disabled:opacity-60 disabled:cursor-not-allowed disabled:hover:translate-y-0"
                style={{
                  background: 'linear-gradient(135deg, #1B4332, #2E7D52, #1B4332)',
                  backgroundSize: '200% 200%',
                }}
              >
                <span className="relative z-10 flex items-center justify-center gap-2">
                  {loading ? (
                    <Spinner size="sm" />
                  ) : (
                    <>
                      Sign in
                      <ArrowRight size={16} className="group-hover:translate-x-0.5 transition-transform" />
                    </>
                  )}
                </span>
              </button>
            </form>

            {/* Register link (supplier only) */}
            {activeRole === 'supplier' && (
              <p className="text-center text-sm text-gray-500 mt-5">
                Don't have an account?{' '}
                <Link to="/register" className="font-semibold text-[#2E7D52] hover:text-[#1B4332] transition-colors">
                  Create one
                </Link>
              </p>
            )}
          </div>

          {/* Trust badges */}
          <div style={stagger(280)} className="flex items-center justify-center gap-4 mt-6 text-[11px] text-gray-400 font-medium">
            <span className="flex items-center gap-1">🔒 SSL Encrypted</span>
            <span className="w-1 h-1 rounded-full bg-gray-300" />
            <span className="flex items-center gap-1">🇿🇦 POPIA Compliant</span>
            <span className="w-1 h-1 rounded-full bg-gray-300" />
            <span className="flex items-center gap-1">✓ CIPC Verified</span>
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════
          Inline Styles for animations
          ═══════════════════════════════════════════════════════ */}
      <style>{`
        /* Orb drift animations */
        .login-orb-1 { animation: orbDrift1 12s ease-in-out infinite; }
        .login-orb-2 { animation: orbDrift2 14s ease-in-out infinite; }
        .login-orb-3 { animation: orbDrift3 10s ease-in-out infinite; }
        @keyframes orbDrift1 {
          0%, 100% { transform: translate(0, 0); }
          33% { transform: translate(30px, 20px); }
          66% { transform: translate(-20px, 10px); }
        }
        @keyframes orbDrift2 {
          0%, 100% { transform: translate(0, 0); }
          33% { transform: translate(-25px, -15px); }
          66% { transform: translate(15px, -25px); }
        }
        @keyframes orbDrift3 {
          0%, 100% { transform: translate(0, 0) scale(1); }
          50% { transform: translate(20px, -20px) scale(1.1); }
        }

        /* Floating particles */
        .login-particle { animation: particleFloat 7s ease-in-out infinite; }
        @keyframes particleFloat {
          0%, 100% { transform: translateY(0) scale(1); opacity: 0.3; }
          50% { transform: translateY(-20px) scale(1.5); opacity: 0.7; }
        }

        /* ═══ 3D LOGO ANIMATIONS ═══ */
        .login-3d-logo-wrapper {
          width: 160px;
          height: 160px;
          display: flex;
          align-items: center;
          justify-content: center;
          perspective: 800px;
        }

        /* Main logo with 3D rotation */
        .login-3d-logo {
          width: 100px;
          height: 100px;
          position: relative;
          z-index: 10;
          transform-style: preserve-3d;
          animation: logo3DFloat 6s ease-in-out infinite, logo3DRotate 12s ease-in-out infinite;
        }
        .login-3d-logo-inner {
          width: 100%;
          height: 100%;
          border-radius: 24px;
          overflow: hidden;
          box-shadow:
            0 20px 60px rgba(0, 0, 0, 0.5),
            0 0 40px rgba(76, 175, 80, 0.3),
            inset 0 -4px 12px rgba(0, 0, 0, 0.2);
          border: 3px solid rgba(255, 255, 255, 0.15);
          transform-style: preserve-3d;
          backface-visibility: hidden;
        }

        @keyframes logo3DFloat {
          0%, 100% { transform: translateY(0) translateZ(0); }
          25% { transform: translateY(-8px) translateZ(10px); }
          50% { transform: translateY(-4px) translateZ(20px); }
          75% { transform: translateY(-10px) translateZ(5px); }
        }
        @keyframes logo3DRotate {
          0%, 100% { transform: rotateY(0deg) rotateX(0deg); }
          25% { transform: rotateY(8deg) rotateX(-4deg); }
          50% { transform: rotateY(-5deg) rotateX(6deg); }
          75% { transform: rotateY(6deg) rotateX(-3deg); }
        }

        /* Glow rings */
        .login-3d-ring {
          position: absolute;
          border-radius: 50%;
          border: 1.5px solid rgba(76, 175, 80, 0.2);
          top: 50%;
          left: 50%;
          transform: translate(-50%, -50%);
          animation: ringPulse 4s ease-in-out infinite;
        }
        .login-3d-ring-1 {
          width: 120px;
          height: 120px;
          border-color: rgba(76, 175, 80, 0.25);
          animation-delay: 0s;
        }
        .login-3d-ring-2 {
          width: 140px;
          height: 140px;
          border-color: rgba(76, 175, 80, 0.15);
          animation-delay: 0.8s;
        }
        .login-3d-ring-3 {
          width: 160px;
          height: 160px;
          border-color: rgba(76, 175, 80, 0.08);
          animation-delay: 1.6s;
        }
        @keyframes ringPulse {
          0%, 100% { transform: translate(-50%, -50%) scale(1); opacity: 0.6; }
          50% { transform: translate(-50%, -50%) scale(1.08); opacity: 1; }
        }

        /* Orbiting dots */
        .login-3d-orbit {
          position: absolute;
          width: 140px;
          height: 140px;
          top: 50%;
          left: 50%;
          transform: translate(-50%, -50%);
          animation: orbitSpin 8s linear infinite;
        }
        .login-3d-orbit-dot {
          position: absolute;
          width: 6px;
          height: 6px;
          border-radius: 50%;
          background: #4CAF50;
          box-shadow: 0 0 8px rgba(76, 175, 80, 0.8), 0 0 20px rgba(76, 175, 80, 0.4);
        }
        .login-3d-orbit-dot-1 { top: 0; left: 50%; transform: translateX(-50%); background: #4CAF50; }
        .login-3d-orbit-dot-2 { bottom: 0; left: 50%; transform: translateX(-50%); background: #81C784; }
        .login-3d-orbit-dot-3 { top: 50%; left: 0; transform: translateY(-50%); background: #F59E0B; box-shadow: 0 0 8px rgba(245, 158, 11, 0.8); }
        .login-3d-orbit-dot-4 { top: 50%; right: 0; transform: translateY(-50%); background: #2E7D52; }
        @keyframes orbitSpin {
          from { transform: translate(-50%, -50%) rotate(0deg); }
          to { transform: translate(-50%, -50%) rotate(360deg); }
        }

        /* Reflection below logo */
        .login-3d-reflection {
          position: absolute;
          bottom: -20px;
          left: 50%;
          transform: translateX(-50%);
          width: 80px;
          height: 20px;
          background: radial-gradient(ellipse, rgba(76, 175, 80, 0.3) 0%, transparent 70%);
          filter: blur(4px);
          animation: reflectionPulse 3s ease-in-out infinite;
        }
        @keyframes reflectionPulse {
          0%, 100% { opacity: 0.5; transform: translateX(-50%) scaleX(1); }
          50% { opacity: 0.8; transform: translateX(-50%) scaleX(1.2); }
        }

        /* Brand text shimmer */
        .login-3d-text-shimmer {
          background: linear-gradient(90deg, #ffffff 0%, #4CAF50 30%, #81C784 50%, #ffffff 70%, #4CAF50 100%);
          background-size: 300% 100%;
          -webkit-background-clip: text;
          -webkit-text-fill-color: transparent;
          animation: textShimmer3D 4s ease-in-out infinite;
        }
        @keyframes textShimmer3D {
          0% { background-position: 100% 0; }
          100% { background-position: -100% 0; }
        }

        /* Subtitle wave */
        .login-3d-subtitle {
          animation: subtitleGlow 3s ease-in-out infinite;
        }
        @keyframes subtitleGlow {
          0%, 100% { opacity: 0.7; letter-spacing: 0.4em; }
          50% { opacity: 1; letter-spacing: 0.5em; }
        }

        /* Card shadow animation */
        .login-card-shadow {
          animation: cardShadow 4s ease-in-out infinite;
        }
        @keyframes cardShadow {
          0%, 100% { box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.08), 0 0 0 1px rgba(0, 0, 0, 0.02); }
          50% { box-shadow: 0 30px 60px -15px rgba(0, 0, 0, 0.12), 0 0 0 1px rgba(0, 0, 0, 0.03); }
        }

        /* Button shimmer */
        .login-btn-shimmer::before {
          content: '';
          position: absolute;
          top: 0;
          left: -100%;
          width: 100%;
          height: 100%;
          background: linear-gradient(90deg, transparent, rgba(255,255,255,0.15), transparent);
          animation: shimmerSweep 3s ease-in-out infinite;
        }
        @keyframes shimmerSweep {
          0% { left: -100%; }
          50%, 100% { left: 100%; }
        }
      `}</style>
    </div>
  );
}

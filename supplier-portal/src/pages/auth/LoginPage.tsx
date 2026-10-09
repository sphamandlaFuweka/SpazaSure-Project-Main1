import { useState, useEffect } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import toast from 'react-hot-toast';
import { Eye, EyeOff, ArrowRight, Package, TrendingUp, ShieldCheck, Users, Store, Lock, BadgeCheck } from 'lucide-react';
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
    <div className="login-page min-h-[100dvh] w-full flex items-center justify-center p-3 lg:p-8 bg-[#EEF2FF]">
      <div className="w-full max-w-6xl bg-white rounded-[2rem] shadow-2xl shadow-primary/10 flex flex-col lg:flex-row overflow-hidden lg:min-h-[640px]">

      {/* LEFT PANEL: headline, feature chips and studio shot */}
      <div className="hidden lg:flex lg:w-[48%] m-3 rounded-[1.75rem] relative flex-col overflow-hidden px-10 pt-12 bg-gradient-to-br from-primary-700 via-primary-600 to-primary-500">
        <div className="absolute -top-24 -right-24 w-72 h-72 rounded-full bg-white/10 blur-2xl" />
        <div className="absolute bottom-10 -left-20 w-64 h-64 rounded-full bg-accent/20 blur-3xl" />

        <div style={stagger(0)} className="relative z-10">
          <h2 className="text-[2.6rem] font-black text-white leading-[1.1] tracking-tight">
            Simplify your spaza supply with our{' '}
            <span className="relative inline-block">
              dashboard
              <span className="absolute left-0 -bottom-1 w-full h-1 rounded-full bg-accent" />
            </span>
            .
          </h2>
          <p className="text-white/80 text-sm leading-relaxed mt-5 max-w-sm">
            Connect with thousands of verified spaza shops. Manage products, orders and payments in one place.
          </p>
          <div className="flex flex-wrap gap-2 mt-5">
            {features.map(({ icon: Icon, title }) => (
              <span key={title} className="flex items-center gap-1.5 bg-white/15 border border-white/20 text-white text-xs font-semibold px-3 py-1.5 rounded-full">
                <Icon size={13} /> {title}
              </span>
            ))}
          </div>
        </div>

        <div style={stagger(150)} className="relative z-10 flex-1 flex items-end justify-center mt-6">
          <img
            src="/studioshot_square_plain.png"
            alt="Shop owner juggling groceries"
            className="max-h-[420px] w-auto object-contain object-bottom"
            onError={(e) => { e.currentTarget.style.display = 'none'; }}
          />
        </div>
      </div>


      {/* ═══════════════════════════════════════════════════════
          RIGHT PANEL — Login form (45% desktop, full mobile)
          ═══════════════════════════════════════════════════════ */}
      <div className="flex-1 flex items-start lg:items-center justify-center bg-white relative py-8 lg:py-0">

        <div className="w-full max-w-[420px] px-6 py-4 sm:py-6 lg:py-0">

          {/* Logo */}
          <div style={stagger(0)} className="flex flex-col items-center mb-6">
            <div className="w-14 h-14 rounded-2xl overflow-hidden ring-2 ring-primary/20 shadow-lg mb-3">
              <img src="/spazasure_logo.jpg" alt="SpazaSure" className="w-full h-full object-cover" />
            </div>
            <h1 className="text-xl font-black text-gray-900 tracking-tight">SpazaSure</h1>
            <p className="text-[10px] font-bold tracking-[0.2em] uppercase text-primary-400">Supplier Portal</p>
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
                    ? 'bg-gradient-to-r from-[#25449A] to-[#3F5DB0] text-white shadow-lg shadow-[#25449A]/20'
                    : 'text-gray-500 hover:text-gray-700'
                )}
              >
                <span className="inline-flex items-center justify-center gap-1.5">{role === 'supplier' ? <Store size={15} /> : <ShieldCheck size={15} />}{role === 'supplier' ? 'Supplier' : 'Admin'}</span>
              </button>
            ))}
          </div>

          {/* Form card */}
          <div
            style={stagger(160)}
            className="p-2 sm:p-4"
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
                      : 'border-gray-200 focus:border-[#607CC8] focus:ring-2 focus:ring-[#607CC8]/10'
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
                        : 'border-gray-200 focus:border-[#607CC8] focus:ring-2 focus:ring-[#607CC8]/10'
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
                  className="text-xs font-semibold text-[#3F5DB0] hover:text-[#25449A] transition-colors"
                >
                  Forgot password?
                </Link>
              </div>

              {/* Submit button */}
              <button
                type="submit"
                disabled={loading}
                className="login-btn-shimmer relative w-full py-3.5 rounded-xl font-bold text-white text-sm overflow-hidden transition-all duration-300 hover:shadow-lg hover:shadow-[#25449A]/25 hover:-translate-y-0.5 disabled:opacity-60 disabled:cursor-not-allowed disabled:hover:translate-y-0"
                style={{
                  background: 'linear-gradient(135deg, #25449A, #3F5DB0, #25449A)',
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
                <Link to="/register" className="font-semibold text-[#3F5DB0] hover:text-[#25449A] transition-colors">
                  Create one
                </Link>
              </p>
            )}
          </div>

          {/* Trust badges */}
          <div style={stagger(280)} className="flex items-center justify-center gap-4 mt-6 text-[11px] text-gray-400 font-medium">
            <span className="flex items-center gap-1"><Lock size={12} /> SSL Encrypted</span>
            <span className="w-1 h-1 rounded-full bg-gray-300" />
            <span className="flex items-center gap-1"><ShieldCheck size={12} /> POPIA Compliant</span>
            <span className="w-1 h-1 rounded-full bg-gray-300" />
            <span className="flex items-center gap-1"><BadgeCheck size={12} /> CIPC Verified</span>
          </div>
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
          background: #607CC8;
          box-shadow: 0 0 8px rgba(76, 175, 80, 0.8), 0 0 20px rgba(76, 175, 80, 0.4);
        }
        .login-3d-orbit-dot-1 { top: 0; left: 50%; transform: translateX(-50%); background: #607CC8; }
        .login-3d-orbit-dot-2 { bottom: 0; left: 50%; transform: translateX(-50%); background: #81C784; }
        .login-3d-orbit-dot-3 { top: 50%; left: 0; transform: translateY(-50%); background: #F59E0B; box-shadow: 0 0 8px rgba(245, 158, 11, 0.8); }
        .login-3d-orbit-dot-4 { top: 50%; right: 0; transform: translateY(-50%); background: #3F5DB0; }
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
          background: linear-gradient(90deg, #ffffff 0%, #607CC8 30%, #81C784 50%, #ffffff 70%, #607CC8 100%);
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

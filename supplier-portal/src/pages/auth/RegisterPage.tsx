import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import toast from 'react-hot-toast';
import { Eye, EyeOff, CheckCircle, ArrowRight, Building2, User, Lock } from 'lucide-react';
import { Spinner } from '../../components/ui';
import { authApi } from '../../services/api';
import { useAuthStore } from '../../store/authStore';

const schema = z.object({
  companyName: z.string().min(2, 'Required'),
  registrationNumber: z.string().min(5, 'Required'),
  contactName: z.string().min(2, 'Required'),
  email: z.string().email('Invalid email'),
  phone: z.string().min(10, 'Invalid phone number'),
  address: z.string().min(5, 'Required'),
  password: z.string().min(8, 'Minimum 8 characters'),
  confirmPassword: z.string(),
}).refine((d) => d.password === d.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword'],
});
type FormData = z.infer<typeof schema>;

const steps = [
  { label: 'Company', icon: Building2 },
  { label: 'Contact', icon: User },
  { label: 'Security', icon: Lock },
];

export default function RegisterPage() {
  const [loading, setLoading] = useState(false);
  const [showPw, setShowPw] = useState(false);
  const [step, setStep] = useState(0);
  const navigate = useNavigate();
  const { setUser } = useAuthStore();

  const { register, handleSubmit, trigger, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
  });

  const stepFields: (keyof FormData)[][] = [
    ['companyName', 'registrationNumber', 'address'],
    ['contactName', 'email', 'phone'],
    ['password', 'confirmPassword'],
  ];

  const nextStep = async () => {
    const valid = await trigger(stepFields[step]);
    if (valid) setStep((s) => Math.min(s + 1, 2));
  };

  const onSubmit = async (data: FormData) => {
    setLoading(true);
    try {
      const res = await authApi.register({
        role: 'supplier',
        email: data.email,
        phone: data.phone,
        password: data.password,
        companyName: data.companyName,
        contactPerson: data.contactName,
        address: data.address,
      });

      if (!res.success) {
        toast.error(res.message || 'Registration failed');
        return;
      }

      const { accessToken, refreshToken, expiresAt, role, userId } = res.data;
      setUser({
        id: userId,
        email: data.email,
        companyName: data.companyName,
        role: 'supplier',
        tier: 'basic',
        isVerified: false,
        token: accessToken,
        refreshToken,
        tokenExpiresAt: expiresAt,
      });

      toast.success('Account created! Complete your profile to get verified.');
      navigate('/dashboard');
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg || 'Registration failed. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex bg-[#0A1A0F] overflow-hidden">
      {/* Left Panel */}
      <div className="hidden lg:flex lg:w-[42%] relative flex-col overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-br from-[#0A1A0F] via-[#1B4332] to-[#0D2E1A]" />
        <div className="absolute top-[-100px] left-[-60px] w-[400px] h-[400px] rounded-full bg-[#4CAF50]/10 blur-[80px]" />
        <div className="absolute bottom-[-80px] right-[-40px] w-[350px] h-[350px] rounded-full bg-[#F59E0B]/8 blur-[70px]" />
        <div className="absolute inset-0 opacity-[0.04]" style={{ backgroundImage: `linear-gradient(rgba(255,255,255,0.5) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.5) 1px, transparent 1px)`, backgroundSize: '40px 40px' }} />

        <div className="relative z-10 flex flex-col h-full p-12">
          <div className="flex items-center gap-3.5 mb-12">
            <div className="w-12 h-12 rounded-2xl overflow-hidden ring-2 ring-white/20 shadow-lg flex-shrink-0">
              <img src="/spazasure_logo.jpg" alt="SpazaSure" className="w-full h-full object-cover" />
            </div>
            <div>
              <h1 className="text-xl font-black text-white tracking-tight leading-none">SpazaSure</h1>
              <p className="text-[10px] font-bold tracking-[0.2em] uppercase text-[#4CAF50] mt-0.5">Supplier Portal</p>
            </div>
          </div>

          <div className="inline-flex items-center gap-2 bg-[#4CAF50]/15 border border-[#4CAF50]/25 rounded-full px-3.5 py-1.5 mb-5 w-fit">
            <span className="w-1.5 h-1.5 rounded-full bg-[#4CAF50] animate-pulse" />
            <span className="text-[11px] font-bold text-[#4CAF50] tracking-wide uppercase">Join 100+ Verified Suppliers</span>
          </div>

          <h2 className="text-4xl font-black text-white leading-[1.1] tracking-tight mb-4">
            Join South Africa's<br />
            <span className="text-transparent bg-clip-text bg-gradient-to-r from-[#4CAF50] to-[#81C784]">fastest growing</span><br />
            spaza marketplace
          </h2>
          <p className="text-[#A7C4B0] text-sm leading-relaxed mb-8 max-w-sm">
            Register as a verified supplier and start reaching thousands of spaza shops across South Africa.
          </p>

          <div className="space-y-3 mb-8">
            {['Free to register — no upfront cost', 'Approval within 2 business days', 'Start selling immediately after approval', 'Dedicated onboarding support'].map((item) => (
              <div key={item} className="flex items-center gap-3">
                <div className="w-5 h-5 rounded-full bg-[#4CAF50]/20 flex items-center justify-center flex-shrink-0">
                  <CheckCircle size={11} className="text-[#4CAF50]" />
                </div>
                <p className="text-[#C8DDD0] text-sm">{item}</p>
              </div>
            ))}
          </div>

          <div className="mt-auto bg-white/[0.04] border border-white/[0.07] rounded-2xl p-5">
            <p className="text-[#7A9E8A] text-xs font-bold uppercase tracking-wide mb-3">Required documents after registration</p>
            <div className="space-y-2">
              {['CIPC Certificate', 'Tax Clearance Certificate', 'BEE Certificate'].map((doc) => (
                <div key={doc} className="flex items-center gap-2.5">
                  <div className="w-1.5 h-1.5 rounded-full bg-[#4CAF50]" />
                  <p className="text-white text-xs font-medium">{doc}</p>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Right Form */}
      <div className="flex-1 flex items-center justify-center p-6 lg:p-10 bg-[#F4F7F5] overflow-y-auto">
        <div className="w-full max-w-lg animate-slide-up">
          <div className="lg:hidden flex items-center gap-3 mb-8">
            <div className="w-10 h-10 rounded-xl overflow-hidden ring-2 ring-[#1B4332]/20 shadow-md flex-shrink-0">
              <img src="/spazasure_logo.jpg" alt="SpazaSure" className="w-full h-full object-cover" />
            </div>
            <div>
              <h1 className="text-lg font-black text-[#1B4332] leading-none">SpazaSure</h1>
              <p className="text-[10px] font-bold tracking-widest uppercase text-[#4CAF50]">Supplier Portal</p>
            </div>
          </div>

          <div className="bg-white rounded-3xl shadow-[0_8px_40px_rgba(0,0,0,0.10)] border border-gray-100/80 p-8">
            <div className="mb-7">
              <h2 className="text-2xl font-black text-gray-900 tracking-tight">Create Supplier Account</h2>
              <p className="text-gray-400 text-sm mt-1 font-medium">Your account will be active immediately. Verification follows.</p>
            </div>

            {/* Step Indicator */}
            <div className="flex items-center gap-2 mb-8">
              {steps.map(({ label, icon: Icon }, i) => (
                <div key={label} className="flex items-center gap-2 flex-1">
                  <div className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold transition-all ${
                    i === step ? 'bg-primary text-white' :
                    i < step ? 'bg-emerald-100 text-emerald-700' :
                    'bg-gray-100 text-gray-400'
                  }`}>
                    {i < step ? <CheckCircle size={12} /> : <Icon size={12} />}
                    {label}
                  </div>
                  {i < 2 && <div className={`flex-1 h-0.5 rounded-full ${i < step ? 'bg-emerald-300' : 'bg-gray-200'}`} />}
                </div>
              ))}
            </div>

            <form onSubmit={handleSubmit(onSubmit)}>
              {step === 0 && (
                <div className="space-y-4 animate-slide-up">
                  <div>
                    <label className="label">Company Name</label>
                    <input {...register('companyName')} className={`input ${errors.companyName ? 'input-error' : ''}`} placeholder="Fresh Foods SA (Pty) Ltd" />
                    {errors.companyName && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.companyName.message}</p>}
                  </div>
                  <div>
                    <label className="label">CIPC Registration Number</label>
                    <input {...register('registrationNumber')} className={`input ${errors.registrationNumber ? 'input-error' : ''}`} placeholder="2020/123456/07" />
                    {errors.registrationNumber && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.registrationNumber.message}</p>}
                  </div>
                  <div>
                    <label className="label">Business Address</label>
                    <input {...register('address')} className={`input ${errors.address ? 'input-error' : ''}`} placeholder="12 Industrial Road, Johannesburg, 2001" />
                    {errors.address && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.address.message}</p>}
                  </div>
                </div>
              )}

              {step === 1 && (
                <div className="space-y-4 animate-slide-up">
                  <div>
                    <label className="label">Contact Person</label>
                    <input {...register('contactName')} className={`input ${errors.contactName ? 'input-error' : ''}`} placeholder="Full name" />
                    {errors.contactName && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.contactName.message}</p>}
                  </div>
                  <div>
                    <label className="label">Business Email</label>
                    <input {...register('email')} type="email" className={`input ${errors.email ? 'input-error' : ''}`} placeholder="you@company.co.za" />
                    {errors.email && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.email.message}</p>}
                  </div>
                  <div>
                    <label className="label">Phone Number</label>
                    <input {...register('phone')} className={`input ${errors.phone ? 'input-error' : ''}`} placeholder="+27 82 000 0000" />
                    {errors.phone && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.phone.message}</p>}
                  </div>
                </div>
              )}

              {step === 2 && (
                <div className="space-y-4 animate-slide-up">
                  <div>
                    <label className="label">Password</label>
                    <div className="relative">
                      <input {...register('password')} type={showPw ? 'text' : 'password'} className={`input pr-11 ${errors.password ? 'input-error' : ''}`} placeholder="Min 8 characters" />
                      <button type="button" onClick={() => setShowPw(!showPw)} className="absolute right-3.5 top-2.5 text-gray-400 hover:text-gray-600">
                        {showPw ? <EyeOff size={16} /> : <Eye size={16} />}
                      </button>
                    </div>
                    {errors.password && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.password.message}</p>}
                  </div>
                  <div>
                    <label className="label">Confirm Password</label>
                    <input {...register('confirmPassword')} type="password" className={`input ${errors.confirmPassword ? 'input-error' : ''}`} placeholder="••••••••" />
                    {errors.confirmPassword && <p className="text-red-500 text-xs mt-1.5 font-medium">{errors.confirmPassword.message}</p>}
                  </div>
                  <p className="text-xs text-gray-400 bg-gray-50 rounded-xl p-3 border border-gray-100">
                    By submitting, you agree to SpazaSure's Terms of Service and Privacy Policy. Your data is protected under POPIA.
                  </p>
                </div>
              )}

              <div className="flex gap-3 mt-7">
                {step > 0 && (
                  <button type="button" onClick={() => setStep((s) => s - 1)} className="btn-secondary flex-1">
                    Back
                  </button>
                )}
                {step < 2 ? (
                  <button type="button" onClick={nextStep} className="btn-primary flex-1 flex items-center justify-center gap-2">
                    Continue <ArrowRight size={15} />
                  </button>
                ) : (
                  <button type="submit" disabled={loading} className="btn-primary flex-1 flex items-center justify-center gap-2">
                    {loading ? <Spinner size="sm" /> : <CheckCircle size={15} />}
                    {loading ? 'Creating account...' : 'Create Account'}
                  </button>
                )}
              </div>
            </form>

            <p className="text-center text-sm text-gray-500 mt-6">
              Already have an account?{' '}
              <Link to="/login" className="text-primary font-bold hover:underline">Sign in</Link>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

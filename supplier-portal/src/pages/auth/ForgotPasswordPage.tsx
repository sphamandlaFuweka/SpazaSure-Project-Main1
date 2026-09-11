import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import toast from 'react-hot-toast';
import { ArrowLeft, Mail, KeyRound, Eye, EyeOff, CheckCircle } from 'lucide-react';
import { Spinner } from '../../components/ui';
import { authApi } from '../../services/api';
import { useNavigate } from 'react-router-dom';

// Step 1 — request reset link
const requestSchema = z.object({
  email: z.string().email('Invalid email address'),
});

// Step 2 — enter token + new password
const resetSchema = z.object({
  email: z.string().email('Invalid email address'),
  token: z.string().min(10, 'Invalid reset token'),
  newPassword: z.string().min(8, 'Minimum 8 characters'),
  confirmPassword: z.string(),
}).refine((d) => d.newPassword === d.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword'],
});

type RequestForm = z.infer<typeof requestSchema>;
type ResetForm = z.infer<typeof resetSchema>;

export default function ForgotPasswordPage() {
  const [searchParams] = useSearchParams();
  // If URL has ?token=xxx&email=xxx, jump straight to reset step
  const [step, setStep] = useState<'request' | 'reset'>(
    searchParams.get('token') ? 'reset' : 'request'
  );
  const [loading, setLoading] = useState(false);
  const [sentEmail, setSentEmail] = useState('');
  const [showPw, setShowPw] = useState(false);
  const navigate = useNavigate();

  const requestForm = useForm<RequestForm>({ resolver: zodResolver(requestSchema) });
  const resetForm = useForm<ResetForm>({
    resolver: zodResolver(resetSchema),
    defaultValues: {
      email: searchParams.get('email') ?? '',
      token: searchParams.get('token') ?? '',
    },
  });

  const onRequestSubmit = async (data: RequestForm) => {
    setLoading(true);
    try {
      await authApi.forgotPassword(data.email);
      setSentEmail(data.email);
      setStep('reset');
      toast.success('If that email exists, a reset token has been sent.');
    } catch {
      toast.error('Failed to send reset link. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const onResetSubmit = async (data: ResetForm) => {
    setLoading(true);
    try {
      const res = await authApi.resetPassword(data.email, data.token, data.newPassword);
      if (!res.success) {
        toast.error(res.message || 'Invalid or expired token.');
        return;
      }
      toast.success('Password reset! Please sign in with your new password.');
      navigate('/login');
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg || 'Invalid or expired token.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-[#F4F7F5] flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <div className="inline-flex items-center gap-3 mb-2">
            <div className="w-11 h-11 rounded-2xl overflow-hidden ring-2 ring-[#1B4332]/20 shadow-md flex-shrink-0">
              <img src="/spazasure_logo.jpg" alt="SpazaSure" className="w-full h-full object-cover" />
            </div>
            <div className="text-left">
              <h1 className="text-xl font-black text-[#1B4332] leading-none">SpazaSure</h1>
              <p className="text-[10px] font-bold tracking-widest uppercase text-[#4CAF50]">Supplier Portal</p>
            </div>
          </div>
        </div>

        <div className="bg-white rounded-3xl shadow-[0_8px_40px_rgba(0,0,0,0.10)] border border-gray-100/80 p-8">
          {/* Step 1 — Request reset */}
          {step === 'request' && (
            <>
              <div className="w-12 h-12 bg-[#1B4332]/10 rounded-2xl flex items-center justify-center mb-5">
                <Mail size={22} className="text-[#1B4332]" />
              </div>
              <h2 className="text-xl font-black text-gray-900 mb-1">Reset your password</h2>
              <p className="text-sm text-gray-500 mb-6">
                Enter your email and we'll send you a reset token.
              </p>

              <form onSubmit={requestForm.handleSubmit(onRequestSubmit)} className="space-y-4">
                <div>
                  <label className="label">Email address</label>
                  <input
                    {...requestForm.register('email')}
                    type="email"
                    className={`input ${requestForm.formState.errors.email ? 'input-error' : ''}`}
                    placeholder="you@company.co.za"
                  />
                  {requestForm.formState.errors.email && (
                    <p className="text-red-500 text-xs mt-1.5 font-medium">
                      {requestForm.formState.errors.email.message}
                    </p>
                  )}
                </div>

                <button
                  type="submit"
                  disabled={loading}
                  className="btn-primary w-full flex items-center justify-center gap-2"
                >
                  {loading ? <Spinner size="sm" /> : <Mail size={15} />}
                  {loading ? 'Sending...' : 'Send Reset Token'}
                </button>
              </form>

              <div className="mt-4 text-center">
                <button
                  onClick={() => setStep('reset')}
                  className="text-sm text-primary font-semibold hover:underline"
                >
                  Already have a token? Enter it here
                </button>
              </div>
            </>
          )}

          {/* Step 2 — Enter token + new password */}
          {step === 'reset' && (
            <>
              <div className="w-12 h-12 bg-[#1B4332]/10 rounded-2xl flex items-center justify-center mb-5">
                <KeyRound size={22} className="text-[#1B4332]" />
              </div>
              <h2 className="text-xl font-black text-gray-900 mb-1">Enter new password</h2>
              <p className="text-sm text-gray-500 mb-6">
                {sentEmail
                  ? `We sent a reset token to ${sentEmail}. Paste it below.`
                  : 'Enter your email, the reset token, and your new password.'}
              </p>

              <form onSubmit={resetForm.handleSubmit(onResetSubmit)} className="space-y-4">
                <div>
                  <label className="label">Email address</label>
                  <input
                    {...resetForm.register('email')}
                    type="email"
                    className={`input ${resetForm.formState.errors.email ? 'input-error' : ''}`}
                    placeholder="you@company.co.za"
                    defaultValue={sentEmail}
                  />
                  {resetForm.formState.errors.email && (
                    <p className="text-red-500 text-xs mt-1.5 font-medium">
                      {resetForm.formState.errors.email.message}
                    </p>
                  )}
                </div>

                <div>
                  <label className="label">Reset Token</label>
                  <input
                    {...resetForm.register('token')}
                    className={`input font-mono text-sm ${resetForm.formState.errors.token ? 'input-error' : ''}`}
                    placeholder="Paste your reset token here"
                  />
                  {resetForm.formState.errors.token && (
                    <p className="text-red-500 text-xs mt-1.5 font-medium">
                      {resetForm.formState.errors.token.message}
                    </p>
                  )}
                </div>

                <div>
                  <label className="label">New Password</label>
                  <div className="relative">
                    <input
                      {...resetForm.register('newPassword')}
                      type={showPw ? 'text' : 'password'}
                      className={`input pr-11 ${resetForm.formState.errors.newPassword ? 'input-error' : ''}`}
                      placeholder="Min 8 characters"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPw(!showPw)}
                      className="absolute right-3.5 top-2.5 text-gray-400 hover:text-gray-600"
                    >
                      {showPw ? <EyeOff size={16} /> : <Eye size={16} />}
                    </button>
                  </div>
                  {resetForm.formState.errors.newPassword && (
                    <p className="text-red-500 text-xs mt-1.5 font-medium">
                      {resetForm.formState.errors.newPassword.message}
                    </p>
                  )}
                </div>

                <div>
                  <label className="label">Confirm New Password</label>
                  <input
                    {...resetForm.register('confirmPassword')}
                    type="password"
                    className={`input ${resetForm.formState.errors.confirmPassword ? 'input-error' : ''}`}
                    placeholder="••••••••"
                  />
                  {resetForm.formState.errors.confirmPassword && (
                    <p className="text-red-500 text-xs mt-1.5 font-medium">
                      {resetForm.formState.errors.confirmPassword.message}
                    </p>
                  )}
                </div>

                <button
                  type="submit"
                  disabled={loading}
                  className="btn-primary w-full flex items-center justify-center gap-2"
                >
                  {loading ? <Spinner size="sm" /> : <CheckCircle size={15} />}
                  {loading ? 'Resetting...' : 'Reset Password'}
                </button>
              </form>

              <button
                onClick={() => setStep('request')}
                className="flex items-center justify-center gap-1.5 text-sm text-gray-500 hover:text-primary mt-5 w-full transition-colors"
              >
                <ArrowLeft size={14} /> Request a new token
              </button>
            </>
          )}
        </div>

        <Link
          to="/login"
          className="flex items-center justify-center gap-1.5 text-sm text-gray-400 hover:text-[#1B4332] mt-5 transition-colors font-semibold"
        >
          <ArrowLeft size={14} /> Back to Sign In
        </Link>
      </div>
    </div>
  );
}

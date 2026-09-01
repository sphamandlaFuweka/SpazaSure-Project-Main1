import { Construction } from 'lucide-react';

interface Props { title: string; description?: string; }

export default function AdminComingSoon({ title, description }: Props) {
  return (
    <div className="p-6 animate-in">
      <h1 className="page-title mb-1">{title}</h1>
      <p className="page-subtitle mb-10">{description ?? 'This section is under construction'}</p>
      <div className="card p-16 flex flex-col items-center justify-center text-center">
        <div className="w-16 h-16 bg-slate-100 rounded-2xl flex items-center justify-center mb-4">
          <Construction size={28} className="text-slate-400" />
        </div>
        <p className="font-bold text-gray-700 text-lg">Coming Soon</p>
        <p className="text-gray-400 text-sm mt-2 max-w-xs">This admin module is being built. Check back in the next phase.</p>
      </div>
    </div>
  );
}

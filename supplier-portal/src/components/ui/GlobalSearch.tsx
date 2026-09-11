import { useState, useRef, useEffect, useCallback } from 'react';
import { Search, Package, ShoppingCart, BarChart2, User, X, ArrowRight, Clock } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { productsApi, ordersApi } from '../../services/api';

interface SearchResult {
  id: string;
  type: 'product' | 'order' | 'page' | 'recent';
  label: string;
  sub?: string;
  to: string;
  icon: React.ReactNode;
}

const pages: SearchResult[] = [
  { id: 'pg1', type: 'page', label: 'Dashboard',     sub: 'Overview & KPIs',          to: '/dashboard',     icon: <BarChart2 size={14} /> },
  { id: 'pg2', type: 'page', label: 'Products',      sub: 'Manage your catalog',      to: '/products',      icon: <Package size={14} /> },
  { id: 'pg3', type: 'page', label: 'Orders',        sub: 'Track & manage orders',    to: '/orders',        icon: <ShoppingCart size={14} /> },
  { id: 'pg4', type: 'page', label: 'Analytics',     sub: 'Revenue & insights',       to: '/analytics',     icon: <BarChart2 size={14} /> },
  { id: 'pg5', type: 'page', label: 'Profile',       sub: 'Account & compliance',     to: '/profile',       icon: <User size={14} /> },
];

const RECENT_KEY = 'spaza_recent_searches';

function getRecent(): string[] {
  try { return JSON.parse(localStorage.getItem(RECENT_KEY) ?? '[]'); } catch { return []; }
}
function saveRecent(q: string) {
  const prev = getRecent().filter((r) => r !== q).slice(0, 4);
  localStorage.setItem(RECENT_KEY, JSON.stringify([q, ...prev]));
}

export default function GlobalSearch() {
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [results, setResults] = useState<SearchResult[]>([]);
  const [active, setActive] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout>>();

  const search = useCallback((q: string) => {
    if (!q.trim()) { setResults([]); setLoading(false); return; }
    setLoading(true);
    clearTimeout(timerRef.current);
    timerRef.current = setTimeout(async () => {
      const ql = q.toLowerCase();

      // Search pages locally (always fast)
      const pageResults: SearchResult[] = pages.filter((p) => p.label.toLowerCase().includes(ql));

      // Search products and orders from real API
      let productResults: SearchResult[] = [];
      let orderResults: SearchResult[] = [];

      try {
        const [prodRes, ordRes] = await Promise.allSettled([
          productsApi.list({ search: q, pageSize: 3 }),
          ordersApi.list({ pageSize: 50 }),
        ]);

        if (prodRes.status === 'fulfilled') {
          productResults = (prodRes.value.data ?? []).slice(0, 3).map((p: any) => ({
            id: p.id, type: 'product' as const, label: p.name,
            sub: `SKU: ${p.sku} · R${p.price}`, to: '/products',
            icon: <Package size={14} />,
          }));
        }

        if (ordRes.status === 'fulfilled') {
          const rawData = ordRes.value?.data as any;
          const allOrders = rawData?.items ?? (Array.isArray(rawData) ? rawData : []);
          orderResults = allOrders
            .filter((o: any) => o.orderNumber?.toLowerCase().includes(ql) || o.shop?.shopName?.toLowerCase().includes(ql))
            .slice(0, 3)
            .map((o: any) => ({
              id: o.id, type: 'order' as const, label: o.orderNumber,
              sub: `${o.shop?.shopName ?? 'Shop'} · R${Number(o.totalAmount ?? 0).toLocaleString()}`,
              to: '/orders', icon: <ShoppingCart size={14} />,
            }));
        }
      } catch {
        // Silently fail — just show page results
      }

      setResults([...pageResults, ...productResults, ...orderResults]);
      setLoading(false);
      setActive(0);
    }, 300);
  }, []);

  useEffect(() => { search(query); }, [query, search]);

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, []);

  // Keyboard shortcut Ctrl+K
  useEffect(() => {
    function handleKey(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') { e.preventDefault(); inputRef.current?.focus(); setOpen(true); }
    }
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, []);

  function handleKeyDown(e: React.KeyboardEvent) {
    const list = query ? results : [];
    if (e.key === 'ArrowDown') { e.preventDefault(); setActive((a) => Math.min(a + 1, list.length - 1)); }
    if (e.key === 'ArrowUp')   { e.preventDefault(); setActive((a) => Math.max(a - 1, 0)); }
    if (e.key === 'Enter' && list[active]) { go(list[active]); }
    if (e.key === 'Escape') { setOpen(false); inputRef.current?.blur(); }
  }

  function go(r: SearchResult) {
    if (query) saveRecent(query);
    navigate(r.to);
    setOpen(false);
    setQuery('');
  }

  const recent = getRecent();
  const showRecent = open && !query && recent.length > 0;
  const showResults = open && !!query;
  const showEmpty = open && !!query && !loading && results.length === 0;
  const dropdownVisible = open && (showRecent || showResults || showEmpty || loading);

  const typeColor: Record<string, string> = {
    product: 'bg-emerald-50 text-emerald-700',
    order:   'bg-blue-50 text-blue-700',
    page:    'bg-violet-50 text-violet-700',
  };

  return (
    <div ref={containerRef} className="relative hidden md:block">
      {/* Input */}
      <div
        className={`flex items-center gap-2 px-3 py-2 rounded-xl border transition-all duration-200 cursor-text ${
          open
            ? 'bg-white border-primary/40 shadow-[0_0_0_3px_rgba(27,67,50,0.08)] w-72'
            : 'bg-gray-50 border-gray-200 hover:border-gray-300 w-56'
        }`}
        onClick={() => { setOpen(true); inputRef.current?.focus(); }}
      >
        <Search size={14} className={`flex-shrink-0 transition-colors ${open ? 'text-primary' : 'text-gray-400'}`} />
        <input
          ref={inputRef}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onFocus={() => setOpen(true)}
          onKeyDown={handleKeyDown}
          placeholder="Search anything..."
          className="flex-1 bg-transparent text-sm outline-none placeholder:text-gray-400 text-gray-900 min-w-0"
        />
        {query ? (
          <button onClick={(e) => { e.stopPropagation(); setQuery(''); inputRef.current?.focus(); }} className="text-gray-400 hover:text-gray-600 transition-colors flex-shrink-0">
            <X size={13} />
          </button>
        ) : (
          <kbd className="hidden lg:flex items-center gap-0.5 text-[10px] font-bold text-gray-400 bg-gray-100 border border-gray-200 rounded px-1.5 py-0.5 flex-shrink-0">
            ⌘K
          </kbd>
        )}
      </div>

      {/* Dropdown */}
      {dropdownVisible && (
        <div
          className="absolute top-full mt-2 right-0 w-80 bg-white rounded-2xl border border-gray-100 shadow-card-xl overflow-hidden z-50"
          style={{ animation: 'searchDrop 0.18s cubic-bezier(0.16,1,0.3,1)' }}
        >
          {/* Loading skeletons */}
          {loading && (
            <div className="p-3 space-y-2">
              {[1, 2, 3].map((i) => (
                <div key={i} className="flex items-center gap-3 px-2 py-2">
                  <div className="skeleton w-7 h-7 rounded-lg flex-shrink-0" />
                  <div className="flex-1 space-y-1.5">
                    <div className="skeleton h-3 rounded w-3/4" />
                    <div className="skeleton h-2.5 rounded w-1/2" />
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Recent searches */}
          {showRecent && !loading && (
            <div>
              <div className="flex items-center justify-between px-4 pt-3 pb-1">
                <span className="text-[11px] font-bold text-gray-400 uppercase tracking-wider">Recent</span>
                <button onClick={() => { localStorage.removeItem(RECENT_KEY); setOpen(false); }} className="text-[11px] text-gray-400 hover:text-red-500 transition-colors">Clear</button>
              </div>
              {recent.map((r, i) => (
                <button
                  key={r}
                  onClick={() => setQuery(r)}
                  className={`w-full flex items-center gap-3 px-4 py-2.5 text-sm text-left transition-colors ${i === active ? 'bg-gray-50' : 'hover:bg-gray-50'}`}
                >
                  <Clock size={13} className="text-gray-400 flex-shrink-0" />
                  <span className="text-gray-700 font-medium">{r}</span>
                </button>
              ))}
            </div>
          )}

          {/* Results */}
          {showResults && !loading && results.length > 0 && (
            <div className="py-2">
              {results.map((r, i) => (
                <button
                  key={r.id}
                  onClick={() => go(r)}
                  onMouseEnter={() => setActive(i)}
                  className={`w-full flex items-center gap-3 px-4 py-2.5 text-left transition-colors ${i === active ? 'bg-primary-50' : 'hover:bg-gray-50'}`}
                >
                  <div className={`w-7 h-7 rounded-lg flex items-center justify-center flex-shrink-0 ${typeColor[r.type] ?? 'bg-gray-100 text-gray-500'}`}>
                    {r.icon}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-gray-900 truncate">{r.label}</p>
                    {r.sub && <p className="text-xs text-gray-400 truncate">{r.sub}</p>}
                  </div>
                  <ArrowRight size={13} className={`flex-shrink-0 transition-opacity ${i === active ? 'text-primary opacity-100' : 'opacity-0'}`} />
                </button>
              ))}
            </div>
          )}

          {/* Empty */}
          {showEmpty && (
            <div className="py-10 text-center">
              <Search size={28} className="mx-auto text-gray-200 mb-2" />
              <p className="text-sm font-semibold text-gray-500">No results for "{query}"</p>
              <p className="text-xs text-gray-400 mt-1">Try a product name, order number, or page</p>
            </div>
          )}

          {/* Footer hint */}
          {!loading && (showResults || showRecent) && (
            <div className="border-t border-gray-50 px-4 py-2 flex items-center gap-3 text-[11px] text-gray-400">
              <span><kbd className="bg-gray-100 border border-gray-200 rounded px-1 py-0.5 font-bold">↑↓</kbd> navigate</span>
              <span><kbd className="bg-gray-100 border border-gray-200 rounded px-1 py-0.5 font-bold">↵</kbd> open</span>
              <span><kbd className="bg-gray-100 border border-gray-200 rounded px-1 py-0.5 font-bold">Esc</kbd> close</span>
            </div>
          )}
        </div>
      )}

      <style>{`
        @keyframes searchDrop {
          from { opacity: 0; transform: translateY(-6px) scale(0.98); }
          to   { opacity: 1; transform: translateY(0) scale(1); }
        }
      `}</style>
    </div>
  );
}

import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { ArrowDown, ArrowUp, Eye, EyeOff, Loader2, Plus, Trash2 } from 'lucide-react';
import clsx from 'clsx';
import { Modal } from '../../components/ui';
import { adminCategoriesApi, type AdminCategory } from '../../services/api';

/**
 * Product categories: suppliers file products under them and shops browse and filter by them.
 * Hidden categories disappear from the shop app and supplier form; products keep them.
 */
export default function CategoriesModal({ onClose, onChanged }: { onClose: () => void; onChanged: () => void }) {
  const [items, setItems] = useState<AdminCategory[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [newName, setNewName] = useState('');
  const [names, setNames] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    try {
      const list = await adminCategoriesApi.list();
      setItems(list);
      setNames(Object.fromEntries(list.map((c) => [c.id, c.name])));
    } catch {
      // The shared API interceptor shows the backend message.
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const run = async (id: string, action: () => Promise<unknown>, done?: string) => {
    setBusyId(id);
    try {
      await action();
      if (done) toast.success(done);
      await load();
      onChanged();
    } catch {
      // The shared API interceptor shows the backend message.
    } finally {
      setBusyId(null);
    }
  };

  const add = () => {
    const name = newName.trim();
    if (name.length < 2) { toast.error('Give the category a name'); return; }
    run('new', () => adminCategoriesApi.create({ name }), 'Category added').then(() => setNewName(''));
  };

  const rename = (c: AdminCategory) => {
    const name = (names[c.id] ?? '').trim();
    if (name === c.name) return;
    if (name.length < 2) { setNames((n) => ({ ...n, [c.id]: c.name })); toast.error('Category names need at least 2 characters'); return; }
    run(c.id, () => adminCategoriesApi.update(c.id, { name }), 'Category renamed');
  };

  /** Swaps sort positions with the neighbour, renumbering so ties can't block a move. */
  const move = (index: number, direction: -1 | 1) => {
    const target = index + direction;
    if (target < 0 || target >= items.length) return;
    const reordered = [...items];
    [reordered[index], reordered[target]] = [reordered[target], reordered[index]];
    const changed = reordered
      .map((c, i) => ({ c, sortOrder: i + 1 }))
      .filter(({ c, sortOrder }) => c.sortOrder !== sortOrder);
    run(items[index].id, () => Promise.all(changed.map(({ c, sortOrder }) => adminCategoriesApi.update(c.id, { sortOrder }))));
  };

  const remove = (c: AdminCategory) => {
    if (c.productCount > 0) {
      toast.error(`${c.productCount} product${c.productCount === 1 ? ' uses' : 's use'} this category. Hide it instead.`);
      return;
    }
    if (!window.confirm(`Delete "${c.name}"?`)) return;
    run(c.id, () => adminCategoriesApi.remove(c.id), 'Category deleted');
  };

  return (
    <Modal title="Product categories" onClose={onClose} size="lg">
      <div className="p-6 space-y-4">
        <p className="text-sm text-gray-500">
          Suppliers pick one of these for each product, and shops filter the marketplace by them. The order here is the order shops see.
          Hiding a category removes it from the shop app and supplier form without changing any products.
        </p>

        <div className="flex gap-2">
          <input className="input flex-1" value={newName} maxLength={60} placeholder="New category, e.g. Pet food"
            onChange={(e) => setNewName(e.target.value)} onKeyDown={(e) => { if (e.key === 'Enter') add(); }} />
          <button className="btn-primary flex items-center gap-1.5" onClick={add} disabled={busyId === 'new'}>
            {busyId === 'new' ? <Loader2 size={14} className="animate-spin" /> : <Plus size={14} />} Add
          </button>
        </div>

        {loading ? (
          <div className="flex justify-center py-8"><Loader2 className="animate-spin text-gray-400" /></div>
        ) : items.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-6">No categories yet.</p>
        ) : (
          <div className="divide-y divide-gray-100 border border-gray-100 rounded-xl">
            {items.map((c, i) => (
              <div key={c.id} className={clsx('flex items-center gap-2 px-3 py-2', !c.isActive && 'bg-gray-50')}>
                <div className="flex flex-col">
                  <button className="text-gray-300 hover:text-gray-600 disabled:opacity-30" aria-label={`Move ${c.name} up`}
                    onClick={() => move(i, -1)} disabled={i === 0 || busyId !== null}><ArrowUp size={13} /></button>
                  <button className="text-gray-300 hover:text-gray-600 disabled:opacity-30" aria-label={`Move ${c.name} down`}
                    onClick={() => move(i, 1)} disabled={i === items.length - 1 || busyId !== null}><ArrowDown size={13} /></button>
                </div>
                <input
                  className={clsx('input flex-1 h-9 text-sm', !c.isActive && 'text-gray-400')}
                  value={names[c.id] ?? c.name}
                  maxLength={60}
                  aria-label={`Name of ${c.name}`}
                  onChange={(e) => setNames((n) => ({ ...n, [c.id]: e.target.value }))}
                  onBlur={() => rename(c)}
                  onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }}
                />
                <span className="text-[11px] text-gray-400 w-24 text-right flex-shrink-0">
                  {c.liveProductCount} live / {c.productCount}
                </span>
                <button
                  className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-primary hover:bg-gray-100"
                  title={c.isActive ? 'Hide from shops and suppliers' : 'Show again'}
                  aria-label={c.isActive ? `Hide ${c.name}` : `Show ${c.name}`}
                  onClick={() => run(c.id, () => adminCategoriesApi.update(c.id, { isActive: !c.isActive }), c.isActive ? 'Category hidden' : 'Category shown')}
                  disabled={busyId === c.id}
                >
                  {c.isActive ? <Eye size={15} /> : <EyeOff size={15} />}
                </button>
                <button
                  className="w-8 h-8 rounded-lg flex items-center justify-center text-gray-400 hover:text-red-600 hover:bg-red-50"
                  title="Delete" aria-label={`Delete ${c.name}`}
                  onClick={() => remove(c)} disabled={busyId === c.id}
                >
                  <Trash2 size={15} />
                </button>
              </div>
            ))}
          </div>
        )}

        <div className="flex justify-end"><button className="btn-secondary" onClick={onClose}>Done</button></div>
      </div>
    </Modal>
  );
}

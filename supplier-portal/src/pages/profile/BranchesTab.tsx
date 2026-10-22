import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { Loader2, MapPin, Pencil, Plus, Trash2, Warehouse } from 'lucide-react';
import { Modal } from '../../components/ui';
import AddressAutocomplete from '../../components/ui/AddressAutocomplete';
import { branchesApi, type BranchesApi, type SupplierBranchInput } from '../../services/api';
import type { SupplierBranch } from '../../types';

const empty: SupplierBranchInput = {
  name: '', address: '', city: '', province: '', postalCode: '', latitude: 0, longitude: 0, isActive: true,
};

/**
 * The supplier's warehouses and depots. Shops near any active branch can buy from the supplier,
 * and SpazaSure runs group buys from one branch to the shops around it.
 */
export default function BranchesTab({ api = branchesApi, forAdmin = false }: {
  /** Admins pass adminBranchesApi(supplierId) to manage a supplier's branches for them. */
  api?: BranchesApi;
  forAdmin?: boolean;
}) {
  const [branches, setBranches] = useState<SupplierBranch[] | null>(null);
  const [editing, setEditing] = useState<SupplierBranch | 'new' | null>(null);

  const load = () => api.list().then(setBranches).catch(() => setBranches([]));
  // Parents key this by supplier, so the api is fixed for its lifetime
  useEffect(() => { load(); }, []);

  const toggle = async (branch: SupplierBranch) => {
    try {
      const { id, ...rest } = branch;
      await api.update(id, { ...rest, isActive: !branch.isActive });
      toast.success(branch.isActive ? 'Branch paused' : 'Branch switched on');
      load();
    } catch { /* the API interceptor shows the message */ }
  };

  const remove = async (branch: SupplierBranch) => {
    if (!confirm(`Remove ${branch.name}? Stock recorded at it counts as head office stock again, and group buys already running from it keep their area.`)) return;
    try {
      await api.remove(branch.id);
      toast.success('Branch removed');
      load();
    } catch { /* the API interceptor shows the message */ }
  };

  return (
    <div className="card p-6 animate-in space-y-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="font-bold text-gray-900">Branches &amp; warehouses</h2>
          <p className="text-sm text-gray-500 mt-1">
            {forAdmin
              ? 'Every place this supplier delivers from. Shops near any active branch can order from them; orders ship from the nearest branch that has the stock.'
              : 'Add every place you deliver from, e.g. your Durban, Johannesburg and Cape Town warehouses. Shops near any active branch can order from you, orders ship from the nearest branch that has the stock, and group buys run from one branch to the shops around it.'}
          </p>
        </div>
        <button className="btn-primary flex items-center gap-2 shrink-0" onClick={() => setEditing('new')}>
          <Plus size={14} /> Add branch
        </button>
      </div>

      {branches === null ? (
        <div className="flex justify-center py-8"><Loader2 className="animate-spin text-gray-400" /></div>
      ) : branches.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-200 p-8 text-center text-sm text-gray-500">
          <Warehouse className="mx-auto mb-2 text-gray-300" size={28} />
          {forAdmin ? 'No branches yet. Shops are matched to the head office address.' : 'No branches yet. Until you add one, shops are matched to your head office address.'}
        </div>
      ) : (
        <ul className="divide-y divide-gray-100 rounded-xl border border-gray-200">
          {branches.map((b) => (
            <li key={b.id} className="flex items-center gap-4 p-4">
              <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${b.isActive ? 'bg-primary/10 text-primary' : 'bg-gray-100 text-gray-400'}`}>
                <Warehouse size={18} />
              </div>
              <div className="flex-1 min-w-0">
                <p className="font-semibold text-gray-900 flex items-center gap-2">
                  {b.name}
                  {!b.isActive && <span className="text-[10px] font-bold uppercase bg-gray-100 text-gray-500 rounded px-1.5 py-0.5">Paused</span>}
                </p>
                <p className="text-xs text-gray-500 truncate">{b.city}, {b.province} · {b.address}</p>
              </div>
              <button className="text-xs font-semibold text-gray-500 hover:text-primary" onClick={() => toggle(b)}>
                {b.isActive ? 'Pause' : 'Switch on'}
              </button>
              <button className="btn-icon" onClick={() => setEditing(b)} aria-label={`Edit ${b.name}`}><Pencil size={14} /></button>
              <button className="btn-icon hover:text-red-500" onClick={() => remove(b)} aria-label={`Remove ${b.name}`}><Trash2 size={14} /></button>
            </li>
          ))}
        </ul>
      )}

      {editing && (
        <BranchForm
          api={api}
          branch={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); load(); }}
        />
      )}
    </div>
  );
}

function BranchForm({ api, branch, onClose, onSaved }: { api: BranchesApi; branch: SupplierBranch | null; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<SupplierBranchInput>(() => {
    if (!branch) return empty;
    const { id: _id, ...rest } = branch;
    return { ...rest, postalCode: rest.postalCode ?? '' };
  });
  // Only an address picked from the suggestions (or pinned on the map) has a map position
  const [confirmed, setConfirmed] = useState(!!branch);
  const [busy, setBusy] = useState(false);
  const set = (patch: Partial<SupplierBranchInput>) => setForm((f) => ({ ...f, ...patch }));

  const save = async () => {
    if (form.name.trim().length < 2) { toast.error('Give the branch a name, e.g. Durban warehouse'); return; }
    if (!confirmed) { toast.error('Pick the address from the suggestions or pin it on the map'); return; }
    if (!form.city.trim() || !form.province.trim()) { toast.error('Enter the city and province'); return; }
    setBusy(true);
    try {
      if (branch) await api.update(branch.id, form);
      else await api.create(form);
      toast.success(branch ? 'Branch updated' : 'Branch added');
      onSaved();
    } catch {
      // The API interceptor shows the backend message.
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal title={branch ? `Edit ${branch.name}` : 'Add branch'} onClose={onClose}>
      <div className="p-6 space-y-4">
        <div>
          <label className="label">Branch name</label>
          <input className="input" value={form.name} onChange={(e) => set({ name: e.target.value })} placeholder="e.g. Durban warehouse" maxLength={80} />
        </div>
        <div>
          <label className="label">Address</label>
          <AddressAutocomplete
            value={form.address}
            confirmed={confirmed}
            onTextChange={(text) => { set({ address: text }); setConfirmed(false); }}
            onSelect={(s) => {
              set({
                address: s.label, city: s.city, province: s.province, postalCode: s.postalCode,
                latitude: s.latitude, longitude: s.longitude,
              });
              setConfirmed(true);
            }}
          />
          {confirmed && (
            <p className="text-xs text-gray-400 mt-1 font-mono">
              <MapPin size={11} className="inline mr-1" />
              {form.latitude.toFixed(5)}, {form.longitude.toFixed(5)}
            </p>
          )}
        </div>
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label className="label">City</label>
            <input className="input" value={form.city} onChange={(e) => set({ city: e.target.value })} />
          </div>
          <div>
            <label className="label">Province</label>
            <input className="input" value={form.province} onChange={(e) => set({ province: e.target.value })} />
          </div>
          <div>
            <label className="label">Postal code</label>
            <input className="input" value={form.postalCode ?? ''} onChange={(e) => set({ postalCode: e.target.value })} maxLength={10} />
          </div>
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <button className="btn-secondary" onClick={onClose}>Cancel</button>
          <button className="btn-primary flex items-center gap-2" disabled={busy} onClick={save}>
            {busy && <Loader2 size={14} className="animate-spin" />}
            {branch ? 'Save branch' : 'Add branch'}
          </button>
        </div>
      </div>
    </Modal>
  );
}

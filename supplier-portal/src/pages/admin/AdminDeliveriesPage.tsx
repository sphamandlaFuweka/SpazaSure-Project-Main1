import { useEffect, useState } from 'react';
import { Truck, PackageCheck, MapPin, Phone, CheckCircle, RefreshCw, Banknote, Package, UserRound, Plus, Copy, Pencil, Link2, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { formatDistanceToNow } from 'date-fns';
import {
  adminDeliveriesApi, adminDriversApi, apiErrorMessage,
  type AdminDelivery, type Driver, type DriverInput,
} from '../../services/api';
import { Modal, Spinner } from '../../components/ui';
import { shortOrderNumber } from '../../utils';

/**
 * SpazaSure's delivery run. Admins register drivers and allocate them to orders; suppliers pack
 * orders and mark them ready; SpazaSure collects them ("out for delivery") and delivers them.
 * Drivers share their location from their private link, and shops follow them on a map.
 */
export default function AdminDeliveriesPage() {
  const [deliveries, setDeliveries] = useState<AdminDelivery[]>([]);
  const [drivers, setDrivers] = useState<Driver[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [tab, setTab] = useState<'deliveries' | 'drivers'>('deliveries');
  const [editing, setEditing] = useState<Driver | 'new' | null>(null);

  const load = () =>
    Promise.all([adminDeliveriesApi.list(), adminDriversApi.list()])
      .then(([d, list]) => { setDeliveries(d); setDrivers(list); })
      .catch((err) => toast.error(apiErrorMessage(err, 'Failed to load deliveries')))
      .finally(() => setLoading(false));

  useEffect(() => {
    load();
    const timer = window.setInterval(load, 60_000);
    return () => window.clearInterval(timer);
  }, []);

  const move = async (d: AdminDelivery, status: 'dispatched' | 'delivered') => {
    if (status === 'dispatched' && !d.driver &&
        !window.confirm(`No driver is allocated to ${d.orderNumber}, so the shop can't follow it on the map. Mark it collected anyway?`)) return;
    if (status === 'delivered' && d.paymentMethod === 'cash_on_delivery' &&
        !window.confirm(`Mark ${d.orderNumber} delivered? Only if you collected R${d.totalAmount.toFixed(2)} in cash from ${d.shop.shopName}.`)) return;
    setBusyId(d.id);
    try {
      await adminDeliveriesApi.updateStatus(d.id, status);
      toast.success(status === 'dispatched' ? `${d.orderNumber} collected: out for delivery` : `${d.orderNumber} delivered`);
      await load();
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Could not update the delivery'));
    } finally {
      setBusyId(null);
    }
  };

  const assign = async (d: AdminDelivery, driverId: string) => {
    setBusyId(d.id);
    try {
      await adminDeliveriesApi.assignDriver(d.id, driverId || null);
      toast.success(driverId ? 'Driver allocated. The shop has been told.' : 'Driver removed from the order');
      await load();
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Could not allocate the driver'));
    } finally {
      setBusyId(null);
    }
  };

  const packing = deliveries.filter((d) => d.status === 'confirmed' || d.status === 'processing');
  const ready = deliveries.filter((d) => d.status === 'ready');
  const outForDelivery = deliveries.filter((d) => d.status === 'dispatched');
  const activeDrivers = drivers.filter((d) => d.isActive);

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-sky-500 to-indigo-600 rounded-xl flex items-center justify-center shadow-lg shadow-sky-200">
            <Truck size={18} className="text-white" />
          </div>
          <div>
            <h1 className="text-xl font-black text-gray-900 tracking-tight">Deliveries</h1>
            <p className="text-sm text-gray-400 font-medium">
              {ready.length} to collect · {outForDelivery.length} out for delivery · {activeDrivers.length} active driver{activeDrivers.length === 1 ? '' : 's'}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <div className="flex rounded-xl border border-gray-200 bg-white p-0.5 text-sm font-semibold">
            {(['deliveries', 'drivers'] as const).map((t) => (
              <button key={t} onClick={() => setTab(t)}
                className={`px-3 py-1.5 rounded-lg capitalize ${tab === t ? 'bg-primary text-white' : 'text-gray-500 hover:text-gray-800'}`}>
                {t}
              </button>
            ))}
          </div>
          <button onClick={() => { setLoading(true); load(); }} className="btn-secondary inline-flex items-center gap-2 text-sm">
            <RefreshCw size={14} /> Refresh
          </button>
        </div>
      </div>

      {loading ? (
        <div className="flex justify-center py-16"><Spinner /></div>
      ) : tab === 'drivers' ? (
        <DriversPanel drivers={drivers} onAdd={() => setEditing('new')} onEdit={setEditing} onChanged={load} />
      ) : (
        <div className="grid gap-5 lg:grid-cols-3">
          <Column
            title="Being packed" icon={<Package size={16} />} empty="No orders being packed."
            items={packing} busyId={busyId} drivers={activeDrivers} onAssign={assign}
          />
          <Column
            title="Ready to collect" icon={<PackageCheck size={16} />} empty="Nothing waiting at suppliers."
            items={ready} busyId={busyId} drivers={activeDrivers} onAssign={assign}
            action={(d) => ({ label: 'Collected · out for delivery', onClick: () => move(d, 'dispatched') })}
          />
          <Column
            title="Out for delivery" icon={<Truck size={16} />} empty="No deliveries on the road."
            items={outForDelivery} busyId={busyId} drivers={activeDrivers} onAssign={assign}
            action={(d) => ({ label: 'Delivered', onClick: () => move(d, 'delivered') })}
          />
        </div>
      )}

      {editing && (
        <DriverForm driver={editing === 'new' ? null : editing} onClose={() => setEditing(null)}
          onSaved={(d, isNew) => { setEditing(null); load(); if (isNew) copyLink(d); }} />
      )}
    </div>
  );
}

const copyLink = (d: Driver) =>
  navigator.clipboard.writeText(
    `Hi ${d.fullName.split(' ')[0]}, this is your SpazaSure driver link. Open it on your phone while delivering ` +
    `and tap "Start sharing my location": ${d.driverLink}`,
  ).then(() => toast.success('Driver link copied. Send it to them on WhatsApp.'))
    .catch(() => toast.error('Could not copy. Copy the link from the driver list instead.'));

function DriversPanel({ drivers, onAdd, onEdit, onChanged }: {
  drivers: Driver[]; onAdd: () => void; onEdit: (d: Driver) => void; onChanged: () => void;
}) {
  const resetLink = async (d: Driver) => {
    if (!window.confirm(`Make a new link for ${d.fullName}? Their current link stops working straight away.`)) return;
    try {
      copyLink(await adminDriversApi.resetLink(d.id));
      onChanged();
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Could not reset the link'));
    }
  };

  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">
          Drivers open their private link on their phone to see their deliveries and share their location. Shops see them on the map while their order is on the road.
        </p>
        <button onClick={onAdd} className="btn-primary inline-flex items-center gap-2 text-sm shrink-0"><Plus size={14} /> Register driver</button>
      </div>
      {drivers.length === 0 ? (
        <div className="card p-8 text-center text-sm text-gray-500">No drivers yet. Register your first driver to start allocating deliveries.</div>
      ) : (
        <div className="card divide-y divide-gray-100">
          {drivers.map((d) => (
            <div key={d.id} className="flex flex-wrap items-center gap-3 p-4">
              <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${d.isActive ? 'bg-primary/10 text-primary' : 'bg-gray-100 text-gray-400'}`}>
                <UserRound size={18} />
              </div>
              <div className="flex-1 min-w-[200px]">
                <p className="font-semibold text-gray-900">
                  {d.fullName} {!d.isActive && <span className="ml-1 text-[10px] font-bold uppercase bg-gray-100 text-gray-500 rounded px-1.5 py-0.5">Off</span>}
                </p>
                <p className="text-xs text-gray-500">
                  {d.vehicleType} · {d.vehicleRegistration}{d.area ? ` · ${d.area}` : ''} · {d.phone} · Badge {d.badgeCode}
                </p>
                <p className="text-xs text-gray-400">
                  {d.openJobs} open deliver{d.openJobs === 1 ? 'y' : 'ies'} · {d.lastSeenAt ? `location shared ${formatDistanceToNow(new Date(d.lastSeenAt), { addSuffix: true })}` : 'has not shared a location yet'}
                </p>
              </div>
              <button className="btn-secondary inline-flex items-center gap-1.5 text-xs" onClick={() => copyLink(d)}><Copy size={13} /> Copy link</button>
              <button className="btn-icon" title="New link (stops the old one)" onClick={() => resetLink(d)}><Link2 size={14} /></button>
              <button className="btn-icon" title="Edit" onClick={() => onEdit(d)}><Pencil size={14} /></button>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function DriverForm({ driver, onClose, onSaved }: { driver: Driver | null; onClose: () => void; onSaved: (d: Driver, isNew: boolean) => void }) {
  const [form, setForm] = useState<DriverInput>(() => driver
    ? { fullName: driver.fullName, phone: driver.phone, vehicleType: driver.vehicleType, vehicleRegistration: driver.vehicleRegistration, area: driver.area ?? '', isActive: driver.isActive }
    : { fullName: '', phone: '', vehicleType: 'Bakkie', vehicleRegistration: '', area: '', isActive: true });
  const [busy, setBusy] = useState(false);
  const set = (patch: Partial<DriverInput>) => setForm((f) => ({ ...f, ...patch }));

  const save = async () => {
    setBusy(true);
    try {
      const saved = driver ? await adminDriversApi.update(driver.id, form) : await adminDriversApi.create(form);
      toast.success(driver ? 'Driver updated' : 'Driver registered');
      onSaved(saved, !driver);
    } catch (err) {
      toast.error(apiErrorMessage(err, 'Could not save the driver'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal title={driver ? `Edit ${driver.fullName}` : 'Register driver'} onClose={onClose}>
      <div className="p-6 space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <div className="col-span-2">
            <label className="label">Full name</label>
            <input className="input" value={form.fullName} onChange={(e) => set({ fullName: e.target.value })} />
          </div>
          <div>
            <label className="label">Phone</label>
            <input className="input" value={form.phone} onChange={(e) => set({ phone: e.target.value })} placeholder="082 123 4567" />
          </div>
          <div>
            <label className="label">Area (optional)</label>
            <input className="input" value={form.area ?? ''} onChange={(e) => set({ area: e.target.value })} placeholder="e.g. Durban" />
          </div>
          <div>
            <label className="label">Vehicle</label>
            <select className="input" value={form.vehicleType} onChange={(e) => set({ vehicleType: e.target.value })}>
              {['Bakkie', 'Van', 'Truck', 'Car', 'Motorbike'].map((v) => <option key={v}>{v}</option>)}
            </select>
          </div>
          <div>
            <label className="label">Registration</label>
            <input className="input uppercase" value={form.vehicleRegistration} onChange={(e) => set({ vehicleRegistration: e.target.value })} placeholder="ND 123-456" />
          </div>
        </div>
        {driver && (
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.isActive} onChange={(e) => set({ isActive: e.target.checked })} />
            Active (can be allocated deliveries and use their link)
          </label>
        )}
        {!driver && <p className="text-xs text-gray-500">After saving, their private link is copied so you can send it to them on WhatsApp.</p>}
        <div className="flex justify-end gap-2">
          <button className="btn-secondary" onClick={onClose}>Cancel</button>
          <button className="btn-primary inline-flex items-center gap-2" disabled={busy} onClick={save}>
            {busy && <Loader2 size={14} className="animate-spin" />} {driver ? 'Save' : 'Register'}
          </button>
        </div>
      </div>
    </Modal>
  );
}

function Column({ title, icon, empty, items, busyId, drivers, onAssign, action }: {
  title: string;
  icon: React.ReactNode;
  empty: string;
  items: AdminDelivery[];
  busyId: string | null;
  drivers: Driver[];
  onAssign: (d: AdminDelivery, driverId: string) => void;
  action?: (d: AdminDelivery) => { label: string; onClick: () => void };
}) {
  return (
    <section>
      <h2 className="flex items-center gap-2 font-bold text-gray-900 mb-3">{icon} {title} <span className="text-gray-400">({items.length})</span></h2>
      {items.length === 0 ? (
        <div className="card p-6 text-center text-sm text-gray-500">{empty}</div>
      ) : (
        <div className="space-y-3">
          {items.map((d) => {
            const act = action?.(d);
            return (
              <div key={d.id} className="card p-4">
                <div className="flex items-center justify-between gap-2">
                  <p className="font-bold text-gray-900 truncate" title={d.orderNumber}>{shortOrderNumber(d.orderNumber)}</p>
                  <span className="text-xs text-gray-400">{formatDistanceToNow(new Date(d.updatedAt), { addSuffix: true })}</span>
                </div>
                <p className="text-xs text-gray-500 mt-0.5">
                  {d.items} item{d.items === 1 ? '' : 's'} · {d.deliveryType === 'express' ? 'Express (same day)' : 'Standard'}
                </p>

                <div className="grid gap-3 mt-3 text-xs">
                  <Stop heading="Collect from"
                    name={d.supplier.branchName ? `${d.supplier.companyName} · ${d.supplier.branchName}` : d.supplier.companyName}
                    phone={d.supplier.phone}
                    address={[d.supplier.address, d.supplier.city].filter(Boolean).join(', ')} />
                  <Stop heading="Deliver to" name={d.shop.shopName} phone={d.shop.phone}
                    address={[d.shop.address, d.shop.city].filter(Boolean).join(', ')} />
                </div>

                <div className="mt-3">
                  <label className="text-[10px] font-bold uppercase tracking-wide text-gray-500">Driver</label>
                  <select className="input mt-1 text-sm" value={d.driver?.id ?? ''} disabled={busyId === d.id}
                    onChange={(e) => onAssign(d, e.target.value)}>
                    <option value="">{d.driver ? 'Remove driver' : 'Allocate a driver...'}</option>
                    {drivers.map((dr) => (
                      <option key={dr.id} value={dr.id}>{dr.fullName} · {dr.vehicleRegistration}{dr.openJobs ? ` (${dr.openJobs} open)` : ''}</option>
                    ))}
                  </select>
                  {d.driver && (
                    <p className="text-[11px] text-gray-500 mt-1">
                      {d.driver.lastSeenAt ? `Last location ${formatDistanceToNow(new Date(d.driver.lastSeenAt), { addSuffix: true })}` : 'Not sharing a location yet'}
                    </p>
                  )}
                </div>

                {d.paymentMethod === 'cash_on_delivery' && (
                  <div className="mt-3 flex items-center gap-2 rounded-xl bg-amber-50 border border-amber-200 px-3 py-2 text-xs text-amber-800">
                    <Banknote size={14} />
                    <span>Collect <strong>R{d.totalAmount.toFixed(2)}</strong> cash.{d.deliveryCode && <> Delivery code for the shop: <strong className="tracking-widest">{d.deliveryCode}</strong></>}</span>
                  </div>
                )}

                {act && (
                  <button onClick={act.onClick} disabled={busyId === d.id}
                    className="btn-primary w-full mt-3 inline-flex items-center justify-center gap-2 text-sm disabled:opacity-50">
                    <CheckCircle size={15} /> {act.label}
                  </button>
                )}
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}

function Stop({ heading, name, phone, address }: { heading: string; name: string; phone: string | null; address: string }) {
  return (
    <div className="rounded-xl bg-gray-50 p-3">
      <p className="font-bold text-gray-500 uppercase tracking-wide text-[10px]">{heading}</p>
      <p className="font-semibold text-gray-900 mt-0.5">{name}</p>
      {address && (
        <a className="flex items-start gap-1 text-gray-600 mt-1 hover:text-primary" target="_blank" rel="noreferrer"
          href={`https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(address)}`}>
          <MapPin size={12} className="mt-0.5 flex-shrink-0" /> {address}
        </a>
      )}
      {phone && (
        <a className="flex items-center gap-1 text-gray-600 mt-1 hover:text-primary" href={`tel:${phone}`}>
          <Phone size={12} /> {phone}
        </a>
      )}
    </div>
  );
}

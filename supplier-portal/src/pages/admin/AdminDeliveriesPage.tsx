import { useEffect, useState } from 'react';
import { Truck, PackageCheck, MapPin, Phone, CheckCircle, RefreshCw, Banknote } from 'lucide-react';
import toast from 'react-hot-toast';
import { formatDistanceToNow } from 'date-fns';
import { adminDeliveriesApi, apiErrorMessage, type AdminDelivery } from '../../services/api';
import { Spinner } from '../../components/ui';
import { shortOrderNumber } from '../../utils';

/**
 * SpazaSure's delivery run. Suppliers pack orders and mark them ready; SpazaSure collects them
 * ("out for delivery") and delivers them to the shop.
 */
export default function AdminDeliveriesPage() {
  const [deliveries, setDeliveries] = useState<AdminDelivery[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = () =>
    adminDeliveriesApi.list()
      .then(setDeliveries)
      .catch((err) => toast.error(apiErrorMessage(err, 'Failed to load deliveries')))
      .finally(() => setLoading(false));

  useEffect(() => {
    load();
    const timer = window.setInterval(load, 60_000);
    return () => window.clearInterval(timer);
  }, []);

  const move = async (d: AdminDelivery, status: 'dispatched' | 'delivered') => {
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

  const ready = deliveries.filter((d) => d.status === 'ready');
  const outForDelivery = deliveries.filter((d) => d.status === 'dispatched');

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-sky-500 to-indigo-600 rounded-xl flex items-center justify-center shadow-lg shadow-sky-200">
            <Truck size={18} className="text-white" />
          </div>
          <div>
            <h1 className="text-xl font-black text-gray-900 tracking-tight">Deliveries</h1>
            <p className="text-sm text-gray-400 font-medium">{ready.length} to collect · {outForDelivery.length} out for delivery</p>
          </div>
        </div>
        <button onClick={() => { setLoading(true); load(); }} className="btn-secondary inline-flex items-center gap-2 text-sm">
          <RefreshCw size={14} /> Refresh
        </button>
      </div>

      {loading ? (
        <div className="flex justify-center py-16"><Spinner /></div>
      ) : (
        <div className="grid gap-5 lg:grid-cols-2">
          <Column
            title="Ready to collect" icon={<PackageCheck size={16} />} empty="Nothing waiting at suppliers."
            items={ready} busyId={busyId}
            action={(d) => ({ label: 'Collected · out for delivery', onClick: () => move(d, 'dispatched') })}
          />
          <Column
            title="Out for delivery" icon={<Truck size={16} />} empty="No deliveries on the road."
            items={outForDelivery} busyId={busyId}
            action={(d) => ({ label: 'Delivered', onClick: () => move(d, 'delivered') })}
          />
        </div>
      )}
    </div>
  );
}

function Column({ title, icon, empty, items, busyId, action }: {
  title: string;
  icon: React.ReactNode;
  empty: string;
  items: AdminDelivery[];
  busyId: string | null;
  action: (d: AdminDelivery) => { label: string; onClick: () => void };
}) {
  return (
    <section>
      <h2 className="flex items-center gap-2 font-bold text-gray-900 mb-3">{icon} {title} <span className="text-gray-400">({items.length})</span></h2>
      {items.length === 0 ? (
        <div className="card p-6 text-center text-sm text-gray-500">{empty}</div>
      ) : (
        <div className="space-y-3">
          {items.map((d) => {
            const { label, onClick } = action(d);
            return (
              <div key={d.id} className="card p-4">
                <div className="flex items-center justify-between gap-2">
                  <p className="font-bold text-gray-900 truncate" title={d.orderNumber}>{shortOrderNumber(d.orderNumber)}</p>
                  <span className="text-xs text-gray-400">{formatDistanceToNow(new Date(d.updatedAt), { addSuffix: true })}</span>
                </div>
                <p className="text-xs text-gray-500 mt-0.5">
                  {d.items} item{d.items === 1 ? '' : 's'} · {d.deliveryType === 'express' ? 'Express (same day)' : 'Standard'}
                </p>

                <div className="grid sm:grid-cols-2 gap-3 mt-3 text-xs">
                  <Stop heading="Collect from" name={d.supplier.companyName} phone={d.supplier.phone}
                    address={[d.supplier.address, d.supplier.city].filter(Boolean).join(', ')} />
                  <Stop heading="Deliver to" name={d.shop.shopName} phone={d.shop.phone}
                    address={[d.shop.address, d.shop.city].filter(Boolean).join(', ')} />
                </div>

                {d.paymentMethod === 'cash_on_delivery' && (
                  <div className="mt-3 flex items-center gap-2 rounded-xl bg-amber-50 border border-amber-200 px-3 py-2 text-xs text-amber-800">
                    <Banknote size={14} />
                    <span>Collect <strong>R{d.totalAmount.toFixed(2)}</strong> cash.{d.deliveryCode && <> Delivery code for the shop: <strong className="tracking-widest">{d.deliveryCode}</strong></>}</span>
                  </div>
                )}

                <button onClick={onClick} disabled={busyId === d.id}
                  className="btn-primary w-full mt-3 inline-flex items-center justify-center gap-2 text-sm disabled:opacity-50">
                  <CheckCircle size={15} /> {label}
                </button>
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

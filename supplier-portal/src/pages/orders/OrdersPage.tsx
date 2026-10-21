import { useState, useEffect, useCallback } from 'react';
import { Search, Eye, CheckCircle, XCircle, Truck, ShoppingCart, Clock, Package, DollarSign, Printer, ShieldAlert, FileText, ArrowRight, MapPin, RefreshCw, AlertTriangle } from 'lucide-react';
import toast from 'react-hot-toast';
import { format } from 'date-fns';
import { ordersApi, profileApi } from '../../services/api';
import type { Order, OrderStatus } from '../../types';
import { OrderStatusBadge, PaymentStatusBadge, EmptyState, Modal } from '../../components/ui';
import { formatCurrency, shortOrderNumber } from '../../utils';
import PageLoader from '../../components/ui/PageLoader';
import clsx from 'clsx';
import OrderDetailModal from './OrderDetailModal';
import OrderReceiptModal from './OrderReceiptModal';
import DeliveryTrackingModal from './DeliveryTrackingModal';
import { useOrderStore } from '../../store/orderStore';
import { useNavigate, useSearchParams } from 'react-router-dom';

const STATUS_TABS: { value: string; label: string }[] = [
  { value: 'all',        label: 'All Orders'  },
  { value: 'pending',    label: 'Pending'     },
  { value: 'confirmed',  label: 'Confirmed'   },
  { value: 'processing', label: 'Processing'  },
  { value: 'dispatched', label: 'Dispatched'  },
  { value: 'delivered',  label: 'Delivered'   },
  { value: 'cancelled',  label: 'Cancelled'   },
];

function mapOrder(o: any): Order {
  const items = (o.items ?? []).map((i: any) => {
    const quantity = Number(i.quantity ?? 0);
    const unitPrice = Number(i.unitPrice ?? i.price ?? 0);
    const originalUnitPrice = Number(i.originalUnitPrice ?? unitPrice);
    const discountAmount = Number(i.discountAmount ?? Math.max(0, originalUnitPrice - unitPrice));
    const discountPct = Number(i.discountPct ?? (originalUnitPrice > 0 ? discountAmount / originalUnitPrice * 100 : 0));
    return {
      productId: i.productId,
      productName: i.name ?? i.productName ?? '',
      productImage: i.productImage ?? '',
      quantity,
      price: unitPrice,
      originalUnitPrice,
      unitPrice,
      discountPct,
      discountAmount,
      originalLineTotal: Number(i.originalLineTotal ?? originalUnitPrice * quantity),
      discountLineAmount: Number(i.discountLineAmount ?? discountAmount * quantity),
      lineTotal: Number(i.lineTotal ?? unitPrice * quantity),
    };
  });
  const originalSubtotal = Number(o.originalSubtotal ?? items.reduce((sum: number, item: Order['items'][number]) => sum + Number(item.originalLineTotal ?? 0), 0));
  const subtotal = Number(o.subtotal ?? items.reduce((sum: number, item: Order['items'][number]) => sum + Number(item.lineTotal ?? 0), 0));

  return {
    id: o.id,
    orderNumber: o.orderNumber,
    shopId: o.shop?.id ?? o.shopId ?? '',
    shopName: o.shop?.shopName ?? o.shopName ?? '',
    shopAddress: o.shop?.address ?? o.shopAddress ?? '',
    items,
    originalSubtotal,
    totalDiscount: Number(o.totalDiscount ?? Math.max(0, originalSubtotal - subtotal)),
    subtotal,
    total: subtotal,
    status: o.status,
    deliveryOption: o.deliveryType ?? 'standard',
    paymentMethod: o.paymentMethod ?? 'eft',
    paymentStatus: o.paymentStatus ?? 'held',
    directPaystackSplit: o.directPaystackSplit ?? false,
    createdAt: o.createdAt,
    estimatedDelivery: o.estimatedDelivery,
    groupBuyId: o.groupBuyId ?? undefined,
    isGroupBuy: o.isGroupBuy ?? Boolean(o.groupBuyId),
    deliveryDistanceKm: o.deliveryDistanceKm == null ? undefined : Number(o.deliveryDistanceKm),
    deliveryLatitude: o.deliveryLatitude == null ? undefined : Number(o.deliveryLatitude),
    deliveryLongitude: o.deliveryLongitude == null ? undefined : Number(o.deliveryLongitude),
  };
}

const REQUIRED_DOCS_LIST = ['cipc_certificate', 'tax_clearance', 'bee_certificate', 'product_license'];

export default function OrdersPage() {
  const navigate = useNavigate();
  const [orders, setOrders] = useState<Order[]>([]);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [selected, setSelected] = useState<Order | null>(null);
  const [receiptOrder, setReceiptOrder] = useState<Order | null>(null);
  const [trackingOrder, setTrackingOrder] = useState<Order | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [updatingId, setUpdatingId] = useState<string | null>(null);
  const [rejecting, setRejecting] = useState<Order | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [searchParams, setSearchParams] = useSearchParams();
  const { fetchPendingCount } = useOrderStore();

  // Compliance
  const [isCompliant, setIsCompliant] = useState(true);

  const loadOrders = useCallback(() => {
    setLoading(true);
    setLoadError(false);
    Promise.allSettled([
      ordersApi.list({ pageSize: 100 }),
      profileApi.get(),
    ]).then(([ordersRes, profileRes]) => {
      if (ordersRes.status === 'fulfilled') {
        const response = ordersRes.value as any;
        const raw = response?.data?.items ?? response?.data ?? response?.items ?? [];
        const mapped = (Array.isArray(raw) ? raw : []).map(mapOrder);
        setOrders(mapped);
        // Deep link from the dashboard / notifications: /orders?order=<id>
        const linkedId = searchParams.get('order');
        if (linkedId) {
          const linked = mapped.find((o) => o.id === linkedId || o.orderNumber === linkedId);
          if (linked) setSelected(linked);
        }
      } else {
        setLoadError(true);
      }
      if (profileRes.status === 'fulfilled') {
        const profile = profileRes.value as any;
        const docs: any[] = profile?.documents ?? [];
        const missing = REQUIRED_DOCS_LIST.filter(dt => {
          const doc = docs.find((d: any) => d.docType === dt);
          return !doc || doc.status === 'rejected';
        });
        setIsCompliant(missing.length === 0);
      }
    }).finally(() => setLoading(false));
    // searchParams is only read for the initial deep link
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => { loadOrders(); }, [loadOrders]);

  const closeDetail = () => {
    setSelected(null);
    if (searchParams.has('order')) {
      searchParams.delete('order');
      setSearchParams(searchParams, { replace: true });
    }
  };

  if (loading) return <PageLoader variant="table" />;

  const filtered = orders.filter((o) => {
    const matchSearch = (o.orderNumber ?? '').toLowerCase().includes(search.toLowerCase()) || (o.shopName ?? '').toLowerCase().includes(search.toLowerCase());
    const matchStatus = statusFilter === 'all' || o.status === statusFilter;
    return matchSearch && matchStatus;
  });

  const STATUS_MESSAGES: Partial<Record<OrderStatus, string>> = {
    processing: 'Order accepted. It is now being prepared.',
    ready:      'Marked ready. SpazaSure will collect it.',
    delivered:  'Order marked as collected by the shop.',
    cancelled:  'Order rejected. The shop has been notified.',
  };

  const applyStatus = async (id: string, status: OrderStatus, reason?: string) => {
    setUpdatingId(id);
    try {
      await ordersApi.updateStatus(id, status, reason);
      // Accepting a card order means the server confirmed the payment (with Paystack if the webhook was missed)
      const confirmPaid = (o: Order) => (status === 'processing' && o.paymentMethod === 'paystack' ? { paymentStatus: 'paid' as const } : {});
      setOrders((prev) => prev.map((o) => o.id === id ? { ...o, status, ...confirmPaid(o) } : o));
      toast.success(STATUS_MESSAGES[status] ?? `Order ${status}`);
      if (selected?.id === id) setSelected((prev) => prev ? { ...prev, status, ...confirmPaid(prev) } : null);
      // Refresh the sidebar badge count immediately
      useOrderStore.setState({ lastFetched: 0 });
      fetchPendingCount();
    } catch {
      // error toast handled by api interceptor
    } finally {
      setUpdatingId(null);
    }
  };

  // Rejecting is irreversible, so ask for confirmation (and an optional reason) first
  const updateStatus = (id: string, status: OrderStatus) => {
    if (status === 'cancelled') {
      const order = orders.find((o) => o.id === id);
      if (order) { setRejectReason(''); setRejecting(order); }
      return;
    }
    void applyStatus(id, status);
  };

  const totalRevenue = orders.filter((o) => o.status === 'delivered').reduce((s, o) => s + o.total, 0);
  const pendingCount = orders.filter((o) => o.status === 'pending' || o.status === 'confirmed').length;
  const processingCount = orders.filter((o) => o.status === 'processing').length;
  const isFiltering = search.trim() !== '' || statusFilter !== 'all';

  return (
    <div className="p-4 sm:p-6 space-y-5 animate-in">
      {/* Compliance Warning */}
      {!isCompliant && (
        <div className="relative overflow-hidden rounded-2xl bg-gradient-to-r from-red-50 to-orange-50 border border-red-200 p-5 flex flex-col sm:flex-row sm:items-center gap-4">
          <div className="w-12 h-12 bg-red-100 rounded-xl flex items-center justify-center flex-shrink-0">
            <ShieldAlert size={22} className="text-red-600" />
          </div>
          <div className="flex-1">
            <p className="font-bold text-red-900 text-sm">Account Restricted — Missing Compliance Documents</p>
            <p className="text-xs text-red-700 mt-1">You cannot process orders until all required documents are submitted and approved.</p>
          </div>
          <button onClick={() => navigate('/profile')} className="flex items-center gap-1.5 text-xs font-bold text-red-700 bg-white border border-red-200 px-4 py-2 rounded-xl hover:bg-red-50 transition-all flex-shrink-0">
            <FileText size={14} /> Upload Documents <ArrowRight size={12} />
          </button>
        </div>
      )}

      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="page-title">Orders</h1>
          <p className="page-subtitle">{orders.length} total order{orders.length === 1 ? '' : 's'}</p>
        </div>
        <button onClick={loadOrders} className="btn-secondary flex items-center gap-2" aria-label="Refresh orders">
          <RefreshCw size={14} /> Refresh
        </button>
      </div>

      {loadError && (
        <div role="alert" className="flex flex-col sm:flex-row sm:items-center gap-3 rounded-2xl border border-red-200 bg-red-50 p-4">
          <AlertTriangle size={18} className="text-red-500 flex-shrink-0" />
          <p className="flex-1 text-sm font-medium text-red-800">We couldn't load your orders. Check your connection and try again.</p>
          <button onClick={loadOrders} className="btn-secondary text-sm">Try again</button>
        </div>
      )}

      {/* Summary Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <div className="card p-4 flex items-center gap-3">
          <div className="w-10 h-10 bg-amber-50 rounded-xl flex items-center justify-center flex-shrink-0">
            <Clock size={18} className="text-amber-600" />
          </div>
          <div>
            <p className="text-xl font-black text-gray-900">{pendingCount}</p>
            <p className="text-xs text-gray-500 font-medium">Awaiting Action</p>
          </div>
        </div>
        <div className="card p-4 flex items-center gap-3">
          <div className="w-10 h-10 bg-violet-50 rounded-xl flex items-center justify-center flex-shrink-0">
            <Package size={18} className="text-violet-600" />
          </div>
          <div>
            <p className="text-xl font-black text-gray-900">{processingCount}</p>
            <p className="text-xs text-gray-500 font-medium">In Processing</p>
          </div>
        </div>
        <div className="card p-4 flex items-center gap-3">
          <div className="w-10 h-10 bg-indigo-50 rounded-xl flex items-center justify-center flex-shrink-0">
            <Truck size={18} className="text-indigo-600" />
          </div>
          <div>
            <p className="text-xl font-black text-gray-900">{orders.filter((o) => o.status === 'dispatched').length}</p>
            <p className="text-xs text-gray-500 font-medium">Out for Delivery</p>
          </div>
        </div>
        <div className="card p-4 flex items-center gap-3">
          <div className="w-10 h-10 bg-emerald-50 rounded-xl flex items-center justify-center flex-shrink-0">
            <DollarSign size={18} className="text-emerald-600" />
          </div>
          <div>
            <p className="text-xl font-black text-gray-900">{formatCurrency(totalRevenue)}</p>
            <p className="text-xs text-gray-500 font-medium">Revenue Delivered</p>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-0.5 border-b border-gray-200 overflow-x-auto">
        {STATUS_TABS.map(({ value, label }) => {
          const count = value === 'all' ? null : orders.filter((o) => o.status === value).length;
          return (
            <button
              key={value}
              onClick={() => setStatusFilter(value)}
              className={clsx(
                'flex items-center gap-1.5 px-4 py-3 text-sm font-semibold border-b-2 transition-all whitespace-nowrap',
                statusFilter === value
                  ? 'border-primary text-primary'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
              )}
            >
              {label}
              {count !== null && count > 0 && (
                <span className={clsx('text-[10px] font-bold px-1.5 py-0.5 rounded-full min-w-[18px] text-center',
                  statusFilter === value ? 'bg-primary text-white' : 'bg-gray-100 text-gray-600'
                )}>
                  {count}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* Search */}
      <div className="relative max-w-sm">
        <Search size={15} className="absolute left-3.5 top-3 text-gray-400" />
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="input pl-10 focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
          placeholder="Search order # or shop name..."
        />
        {search && (
          <button onClick={() => setSearch('')} className="absolute right-3 top-2.5 text-gray-400 hover:text-gray-600 transition-colors">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
          </button>
        )}
      </div>

      {/* Table */}
      <div className="card overflow-hidden">
        {filtered.length === 0 ? (
          isFiltering ? (
            <EmptyState
              title="No matching orders"
              description="Try a different search term or status tab."
              icon={<Search size={40} />}
              action={<button onClick={() => { setSearch(''); setStatusFilter('all'); }} className="btn-secondary">Clear filters</button>}
            />
          ) : (
            <EmptyState
              title="No orders yet"
              description="Orders from spaza shops will appear here as soon as they are placed."
              icon={<ShoppingCart size={40} />}
            />
          )
        ) : (
          <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[960px]">
            <thead>
              <tr className="border-b border-gray-100 bg-gray-50/80">
                <th className="table-header">Order #</th>
                <th className="table-header">Shop</th>
                <th className="table-header">Items</th>
                <th className="table-header text-right">Product Value</th>
                <th className="table-header text-center">Status</th>
                <th className="table-header text-center">Payment</th>
                <th className="table-header">Date</th>
                <th className="table-header text-center">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((order) => (
                <tr key={order.id} className="table-row">
                  <td className="table-cell">
                    <div>
                      <button onClick={() => setSelected(order)} className="font-bold text-primary hover:underline">
                        <span title={order.orderNumber}>{shortOrderNumber(order.orderNumber)}</span>
                      </button>
                      {order.isGroupBuy && (
                        <p className="text-[10px] font-bold uppercase tracking-wide text-violet-600 mt-1">Group Buy</p>
                      )}
                    </div>
                  </td>
                  <td className="table-cell">
                    <div>
                      <p className="font-semibold text-gray-900">{order.shopName}</p>
                      <p className="text-xs text-gray-400">{order.shopAddress}</p>
                    </div>
                  </td>
                  <td className="table-cell text-gray-500">
                    <p>{order.items.length} item{order.items.length !== 1 ? 's' : ''}</p>
                    {order.deliveryDistanceKm != null && (
                      <p className="text-xs text-gray-400 mt-0.5">{order.deliveryDistanceKm.toFixed(1)} km delivery</p>
                    )}
                  </td>
                  <td className="table-cell text-right font-bold text-emerald-700">
                    R{order.subtotal.toFixed(2)}
                  </td>
                  <td className="table-cell text-center">
                    <OrderStatusBadge status={order.status} />
                  </td>
                  <td className="table-cell text-center">
                    <PaymentStatusBadge status={order.paymentStatus} />
                  </td>
                  <td className="table-cell text-gray-400 text-xs">
                    <p>{format(new Date(order.createdAt), 'dd MMM yyyy')}</p>
                    <p>{format(new Date(order.createdAt), 'HH:mm')}</p>
                  </td>
                  <td className="table-cell">
                    <div className="flex items-center justify-center gap-1">
                      <button onClick={() => setSelected(order)} className="btn-icon hover:text-primary hover:bg-primary-50" title="View Details">
                        <Eye size={14} />
                      </button>
                      <button onClick={() => setReceiptOrder(order)} className="btn-icon hover:text-emerald-600 hover:bg-emerald-50" title="View Receipt">
                        <Printer size={14} />
                      </button>
                      {(order.status === 'processing' || order.status === 'ready' || order.status === 'dispatched' || order.status === 'delivered') && (
                        <button onClick={() => setTrackingOrder(order)} className="btn-icon hover:text-blue-600 hover:bg-blue-50" title="Delivery map">
                          <MapPin size={14} />
                        </button>
                      )}
                      {(order.status === 'pending' || order.status === 'confirmed') && (
                        <>
                          <button disabled={updatingId === order.id} onClick={() => updateStatus(order.id, 'processing')} aria-label="Accept order" className="btn-icon disabled:opacity-40 hover:text-emerald-600 hover:bg-emerald-50" title="Accept Order">
                            <CheckCircle size={14} />
                          </button>
                          <button disabled={updatingId === order.id} onClick={() => updateStatus(order.id, 'cancelled')} aria-label="Reject order" className="btn-icon disabled:opacity-40 hover:text-red-500 hover:bg-red-50" title="Reject Order">
                            <XCircle size={14} />
                          </button>
                        </>
                      )}
                      {/* SpazaSure collects and delivers: the supplier packs the order and marks it ready */}
                      {order.status === 'processing' && (
                        <button disabled={updatingId === order.id} onClick={() => updateStatus(order.id, 'ready')} aria-label="Mark ready for collection" className="btn-icon disabled:opacity-40 hover:text-sky-600 hover:bg-sky-50" title="Ready for collection">
                          <Package size={14} />
                        </button>
                      )}
                      {order.status === 'ready' && order.deliveryOption === 'pickup' && (
                        <button disabled={updatingId === order.id} onClick={() => updateStatus(order.id, 'delivered')} aria-label="Handed to the shop" className="btn-icon disabled:opacity-40 hover:text-emerald-600 hover:bg-emerald-50" title="Handed to the shop">
                          <Truck size={14} />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        )}
      </div>

      {selected && (
        <OrderDetailModal order={selected} onClose={closeDetail} onUpdateStatus={updateStatus} />
      )}

      {receiptOrder && (
        <OrderReceiptModal order={receiptOrder} onClose={() => setReceiptOrder(null)} />
      )}

      {trackingOrder && (
        <DeliveryTrackingModal order={trackingOrder} onClose={() => setTrackingOrder(null)} />
      )}

      {rejecting && (
        <Modal title={`Reject order ${rejecting.orderNumber}?`} onClose={() => setRejecting(null)} size="sm">
          <form
            className="p-6 space-y-4"
            onSubmit={async (e) => {
              e.preventDefault();
              const order = rejecting;
              setRejecting(null);
              await applyStatus(order.id, 'cancelled', rejectReason.trim() || undefined);
            }}
          >
            <p className="text-sm text-gray-600">
              <span className="font-semibold text-gray-900">{rejecting.shopName || 'The shop'}</span> will see this order as
              rejected. This can't be undone.
            </p>
            <div>
              <label htmlFor="reject-reason" className="label">Reason <span className="text-gray-400 font-normal text-xs">(shared with the shop)</span></label>
              <textarea
                id="reject-reason"
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
                className="input resize-none"
                rows={3}
                placeholder="e.g. Out of stock on some items"
                autoFocus
              />
            </div>
            <div className="flex justify-end gap-3">
              <button type="button" onClick={() => setRejecting(null)} className="btn-secondary">Keep order</button>
              <button type="submit" className="btn-danger flex items-center gap-2"><XCircle size={15} /> Reject order</button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}

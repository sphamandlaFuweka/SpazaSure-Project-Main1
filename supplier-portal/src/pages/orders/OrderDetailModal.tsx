import { useState } from 'react';
import { CheckCircle, XCircle, Truck, MapPin, CreditCard, Package, Printer, ShieldAlert } from 'lucide-react';
import { format } from 'date-fns';
import type { DeliveryDispute, Order, OrderStatus } from '../../types';
import { OrderStatusBadge, PaymentStatusBadge, Modal } from '../../components/ui';
import OrderReceiptModal from './OrderReceiptModal';

interface Props {
  order: Order;
  onClose: () => void;
  onUpdateStatus: (id: string, status: OrderStatus) => void;
  dispute?: DeliveryDispute | null;
  disputeLoading?: boolean;
  resolvingDispute?: boolean;
  onResolveDispute?: (resolution: 'release_supplier' | 'refund_shop' | 'confirm_refund', note?: string) => void;
}

const orderSteps: { status: OrderStatus; label: string }[] = [
  { status: 'pending',    label: 'Order Placed' },
  { status: 'confirmed',  label: 'Confirmed' },
  { status: 'processing', label: 'Processing' },
  { status: 'ready',      label: 'Ready' },
  { status: 'dispatched', label: 'Out for delivery' },
  { status: 'delivered',  label: 'Delivered' },
];

const statusOrder = ['pending', 'confirmed', 'processing', 'ready', 'dispatched', 'delivered'];

export default function OrderDetailModal({
  order,
  onClose,
  onUpdateStatus,
  dispute,
  disputeLoading = false,
  resolvingDispute = false,
  onResolveDispute,
}: Props) {
  const [showReceipt, setShowReceipt] = useState(false);
  const [resolutionNote, setResolutionNote] = useState('');
  const currentStep = statusOrder.indexOf(order.status);
  const hasDiscount = Number(order.totalDiscount ?? 0) > 0 || order.items.some((item) => Number(item.discountAmount ?? 0) > 0);

  return (
    <Modal title={order.orderNumber} onClose={onClose} size="lg">
      <div className="p-6 space-y-5">
        {/* Status & Meta */}
        <div className="flex items-center gap-3 flex-wrap">
          <OrderStatusBadge status={order.status} />
          <PaymentStatusBadge status={order.paymentStatus} />
          <span className="text-xs text-gray-400 bg-gray-100 px-2.5 py-1 rounded-full font-medium capitalize">
            {order.deliveryOption} delivery
          </span>
          {order.isGroupBuy && (
            <span className="text-xs text-violet-700 bg-violet-50 px-2.5 py-1 rounded-full font-bold">
              Group Buy order
            </span>
          )}
          <span className="text-xs text-gray-400 ml-auto">
            {format(new Date(order.createdAt), 'dd MMMM yyyy, HH:mm')}
          </span>
        </div>

        {/* Order Timeline */}
        {order.status !== 'cancelled' && order.status !== 'disputed' && (
          <div className="bg-gray-50 rounded-2xl p-4">
            <p className="text-xs font-bold text-gray-500 uppercase mb-4">Order Progress</p>
            <div className="flex items-center">
              {orderSteps.map(({ status, label }, i) => {
                const done = i <= currentStep;
                const active = i === currentStep;
                return (
                  <div key={status} className="flex items-center flex-1 last:flex-none">
                    <div className="flex flex-col items-center">
                      <div className={`w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold transition-all ${
                        done ? 'bg-primary text-white' : 'bg-gray-200 text-gray-400'
                      } ${active ? 'ring-4 ring-primary/20' : ''}`}>
                        {done && i < currentStep ? <CheckCircle size={14} /> : i + 1}
                      </div>
                      <p className={`text-[10px] font-semibold mt-1.5 text-center ${done ? 'text-primary' : 'text-gray-400'}`}>
                        {label}
                      </p>
                    </div>
                    {i < orderSteps.length - 1 && (
                      <div className={`flex-1 h-0.5 mx-1 mb-4 rounded-full transition-all ${i < currentStep ? 'bg-primary' : 'bg-gray-200'}`} />
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Shop Info */}
        <div className="flex items-start gap-3 p-4 bg-gray-50 rounded-2xl">
          <div className="w-9 h-9 bg-primary-50 rounded-xl flex items-center justify-center flex-shrink-0">
            <MapPin size={16} className="text-primary" />
          </div>
          <div>
            <p className="text-xs font-bold text-gray-500 uppercase mb-0.5">Delivery To</p>
            <p className="font-bold text-gray-900">{order.shopName}</p>
            <p className="text-sm text-gray-500">{order.shopAddress}</p>
            {order.deliveryDistanceKm != null && (
              <p className="text-xs text-gray-500 font-semibold mt-1">
                Delivery distance: {order.deliveryDistanceKm.toFixed(1)} km
              </p>
            )}
            {order.estimatedDelivery && (
              <p className="text-xs text-primary font-semibold mt-1">
                Est. delivery: {format(new Date(order.estimatedDelivery), 'dd MMM yyyy')}
              </p>
            )}
          </div>
        </div>

        {disputeLoading && (
          <div className="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm font-semibold text-amber-800">
            Loading delivery dispute…
          </div>
        )}
        {dispute && (
          <section className="rounded-2xl border border-red-200 bg-red-50/60 p-4 space-y-3">
            <div className="flex items-start gap-3">
              <ShieldAlert size={18} className="mt-0.5 text-red-600" />
              <div className="flex-1">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <h3 className="font-extrabold text-red-900">Delivery dispute: {dispute.issueType}</h3>
                  <span className="rounded-full bg-white px-2.5 py-1 text-xs font-bold capitalize text-red-800">
                    {dispute.status.replace(/_/g, ' ')}
                  </span>
                </div>
                <p className="mt-1 text-sm text-gray-700 whitespace-pre-wrap">{dispute.description}</p>
                <p className="mt-2 text-xs text-gray-500">
                  Reported {format(new Date(dispute.createdAt), 'dd MMM yyyy, HH:mm')}
                </p>
                {dispute.resolutionNote && (
                  <p className="mt-2 rounded-xl bg-white/80 p-3 text-sm text-gray-700">
                    Admin note: {dispute.resolutionNote}
                  </p>
                )}
                {dispute.refundStatus && (
                  <p className="mt-2 text-xs font-semibold text-gray-600">
                    Refund status: {dispute.refundStatus}
                    {dispute.refundReference ? ` · Reference ${dispute.refundReference}` : ''}
                  </p>
                )}
              </div>
            </div>

            {dispute.status === 'open' && onResolveDispute && (
              <>
                <label className="block text-xs font-bold text-gray-600" htmlFor="dispute-resolution-note">Resolution note</label>
                <textarea
                  id="dispute-resolution-note"
                  value={resolutionNote}
                  onChange={(event) => setResolutionNote(event.target.value)}
                  maxLength={1000}
                  rows={2}
                  className="input w-full resize-y"
                  placeholder="Add a note for the shop and supplier (optional)"
                />
                <div className="flex flex-col sm:flex-row gap-2">
                  <button
                    type="button"
                    disabled={resolvingDispute}
                    onClick={() => onResolveDispute('release_supplier', resolutionNote.trim() || undefined)}
                    className="btn-primary flex-1 disabled:opacity-50"
                  >
                    Uphold delivery · release payout
                  </button>
                  <button
                    type="button"
                    disabled={resolvingDispute}
                    onClick={() => onResolveDispute('refund_shop', resolutionNote.trim() || undefined)}
                    className="btn-danger flex-1 disabled:opacity-50"
                  >
                    Uphold shop · refund payment
                  </button>
                </div>
                <p className="text-xs text-gray-600">
                  Paystack refunds are requested automatically. Cash-on-delivery cases stay open until an admin confirms the manual refund.
                </p>
              </>
            )}

            {(dispute.status.startsWith('refund_pending') || dispute.status === 'refund_requesting') && onResolveDispute && (
              <div className="space-y-2">
                <p className="text-sm text-amber-800">
                  {dispute.status === 'refund_pending_manual'
                    ? 'Send the cash-on-delivery refund outside SpazaSure before confirming it here.'
                    : 'Verify the refund is complete with Paystack before confirming it here.'}
                </p>
                <textarea
                  value={resolutionNote}
                  onChange={(event) => setResolutionNote(event.target.value)}
                  maxLength={1000}
                  rows={2}
                  className="input w-full resize-y"
                  placeholder="Refund confirmation or receipt reference"
                  aria-label="Refund confirmation note"
                />
                <button
                  type="button"
                  disabled={resolvingDispute || !resolutionNote.trim()}
                  onClick={() => onResolveDispute('confirm_refund', resolutionNote.trim() || undefined)}
                  className="btn-primary w-full disabled:opacity-50"
                >
                  Confirm refund completed
                </button>
              </div>
            )}
          </section>
        )}

        {/* Order Items */}
        <div>
          <div className="flex items-center gap-2 mb-3">
            <Package size={15} className="text-gray-400" />
            <p className="text-xs font-bold text-gray-500 uppercase">Order Items</p>
          </div>
          <div className="space-y-2">
            {order.items.map((item) => {
              const unitPrice = Number(item.unitPrice ?? item.price);
              const lineTotal = Number(item.lineTotal ?? item.quantity * unitPrice);
              const itemHasDiscount = Number(item.discountAmount ?? 0) > 0;
              return (
                <div key={item.productId} className="flex items-start justify-between gap-4 py-3 px-4 bg-gray-50 rounded-xl">
                  <div>
                    <p className="font-semibold text-gray-900 text-sm">{item.productName}</p>
                    {itemHasDiscount ? (
                      <div className="text-xs text-gray-500 mt-1 space-y-0.5">
                        <p>Original price: R{Number(item.originalUnitPrice ?? unitPrice).toFixed(2)}</p>
                        <p>Supplier discount: {Number(item.discountPct ?? 0).toFixed(0)}% (R{Number(item.discountAmount ?? 0).toFixed(2)} per unit)</p>
                        <p className="font-semibold text-emerald-700">Approved unit price: R{unitPrice.toFixed(2)}</p>
                      </div>
                    ) : (
                      <p className="text-xs text-gray-400 mt-0.5">{item.quantity} units × R{unitPrice.toFixed(2)}</p>
                    )}
                  </div>
                  <div className="text-right flex-shrink-0">
                    <p className="font-bold text-gray-900">R{lineTotal.toFixed(2)}</p>
                    {itemHasDiscount && <p className="text-[10px] text-gray-400">{item.quantity} units</p>}
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Supplier earnings */}
        <div>
          <div className="flex items-center gap-2 mb-3">
            <CreditCard size={15} className="text-gray-400" />
            <p className="text-xs font-bold text-gray-500 uppercase">Your product share</p>
          </div>
          <div className="bg-gray-50 rounded-2xl p-4 space-y-2 text-sm">
            {hasDiscount ? (
              <>
                <div className="flex justify-between text-gray-600">
                  <span>Original Subtotal</span>
                  <span>R{Number(order.originalSubtotal ?? order.subtotal + Number(order.totalDiscount ?? 0)).toFixed(2)}</span>
                </div>
                <div className="flex justify-between font-semibold text-emerald-700">
                  <span>Total Supplier Discount</span>
                  <span>-R{Number(order.totalDiscount ?? 0).toFixed(2)}</span>
                </div>
                <div className="flex justify-between text-gray-900 font-bold">
                  <span>Discounted Subtotal</span>
                  <span>R{order.subtotal.toFixed(2)}</span>
                </div>
              </>
            ) : (
              <div className="flex justify-between text-gray-600">
                <span>Product value</span>
                <span>R{order.subtotal.toFixed(2)}</span>
              </div>
            )}
            <div className="flex justify-between font-black text-base border-t border-gray-200 pt-2.5 mt-1">
              <span>Your product share</span>
              <span>R{order.subtotal.toFixed(2)}</span>
            </div>
          </div>
          <p className="text-xs text-gray-400 mt-2 text-center">
            {order.directPaystackSplit
              ? 'When Paystack confirms the shop payment, your product share is allocated to your Paystack subaccount. Bank settlement timing follows Paystack’s schedule.'
              : 'SpazaSure pays your product share after delivery and the payout delay. See Analytics → Your payouts.'}
          </p>
        </div>

        {/* Actions */}
        {(order.status === 'pending' || order.status === 'confirmed') && (
          <div className="flex gap-3 pt-2">
            <button
              onClick={() => { onUpdateStatus(order.id, 'processing'); onClose(); }}
              className="btn-primary flex-1 flex items-center justify-center gap-2"
            >
              <CheckCircle size={16} /> Accept Order
            </button>
            <button
              onClick={() => { onUpdateStatus(order.id, 'cancelled'); onClose(); }}
              className="btn-danger flex-1 flex items-center justify-center gap-2"
            >
              <XCircle size={16} /> Reject Order
            </button>
          </div>
        )}
        {order.status === 'processing' && (
          <button
            onClick={() => { onUpdateStatus(order.id, 'ready'); onClose(); }}
            className="btn-primary w-full flex items-center justify-center gap-2"
          >
            <Truck size={16} /> Packed: ready for collection
          </button>
        )}
        {order.status === 'ready' && (
          order.deliveryOption === 'pickup' ? (
            <button
              onClick={() => { onUpdateStatus(order.id, 'delivered'); onClose(); }}
              className="btn-primary w-full flex items-center justify-center gap-2"
            >
              <CheckCircle size={16} /> Handed to the shop
            </button>
          ) : (
            <p className="text-sm text-center text-sky-700 bg-sky-50 border border-sky-200 rounded-2xl p-3">
              SpazaSure has been notified and will collect this order from you, then deliver it to the shop.
            </p>
          )
        )}

        {/* View Receipt Button */}
        <button
          onClick={() => setShowReceipt(true)}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 text-sm font-semibold text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-xl transition-colors"
        >
          <Printer size={16} /> View Receipt
        </button>
      </div>

      {showReceipt && (
        <OrderReceiptModal order={order} onClose={() => setShowReceipt(false)} />
      )}
    </Modal>
  );
}

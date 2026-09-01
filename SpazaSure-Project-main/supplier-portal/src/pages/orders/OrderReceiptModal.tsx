import { useRef } from 'react';
import { Printer, X, Download, Store } from 'lucide-react';
import { format } from 'date-fns';
import type { Order } from '../../types';
import toast from 'react-hot-toast';

interface Props {
  order: Order;
  onClose: () => void;
}

export default function OrderReceiptModal({ order, onClose }: Props) {
  const receiptRef = useRef<HTMLDivElement>(null);
  const hasDiscount = Number(order.totalDiscount ?? 0) > 0 || order.items.some((item) => Number(item.discountAmount ?? 0) > 0);

  const handlePrint = () => {
    const content = receiptRef.current;
    if (!content) return;

    const printWindow = window.open('', '_blank', 'width=420,height=700');
    if (!printWindow) { toast.error('Please allow popups to print'); return; }

    printWindow.document.write(`
      <html>
      <head>
        <title>Receipt - ${order.orderNumber}</title>
        <style>
          * { margin: 0; padding: 0; box-sizing: border-box; }
          body { font-family: 'Courier New', monospace; padding: 24px; max-width: 380px; margin: 0 auto; color: #1a1a1a; }
          .center { text-align: center; }
          .bold { font-weight: bold; }
          .divider { border-top: 1px dashed #ccc; margin: 12px 0; }
          .row { display: flex; justify-content: space-between; margin: 4px 0; font-size: 12px; }
          .row-bold { display: flex; justify-content: space-between; margin: 8px 0; font-size: 14px; font-weight: bold; }
          .header { text-align: center; margin-bottom: 16px; }
          .header h1 { font-size: 20px; color: #1B5E20; margin: 8px 0 4px; }
          .header p { font-size: 10px; letter-spacing: 3px; color: #999; text-transform: uppercase; }
          .section-label { font-size: 9px; font-weight: bold; color: #999; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 4px; }
          table { width: 100%; border-collapse: collapse; font-size: 11px; margin: 8px 0; }
          th { text-align: left; border-bottom: 1px solid #eee; padding: 4px 0; font-size: 10px; color: #666; }
          th:nth-child(2) { text-align: center; }
          th:nth-child(3), th:nth-child(4) { text-align: right; }
          td { padding: 6px 0; border-bottom: 1px solid #f5f5f5; }
          td:nth-child(2) { text-align: center; }
          td:nth-child(3), td:nth-child(4) { text-align: right; }
          .total-box { background: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 8px; padding: 10px 12px; margin: 8px 0; }
          .total-box .row-bold { font-size: 16px; margin: 0; }
          .total-box .amount { color: #15803d; }
          .footer { text-align: center; margin-top: 16px; font-size: 12px; color: #555; }
          .footer small { display: block; margin-top: 4px; font-size: 9px; color: #aaa; }
          @media print { body { padding: 12px; } }
        </style>
      </head>
      <body>
        <div class="header">
          <div style="width:40px;height:40px;background:#f0fdf4;border-radius:10px;display:flex;align-items:center;justify-content:center;margin:0 auto 8px;">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#15803d" stroke-width="2"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V9z"/><polyline points="9,22 9,12 15,12 15,22"/></svg>
          </div>
          <h1>SpazaSure</h1>
          <p>Order Receipt</p>
        </div>

        <div class="divider"></div>

        <div class="row">
          <div><span class="section-label">Order #</span><br/><strong>${order.orderNumber}</strong></div>
          <div style="text-align:right"><span class="section-label">Date</span><br/><strong>${format(new Date(order.createdAt), 'dd MMM yyyy')}</strong><br/><span style="font-size:11px;color:#666">${format(new Date(order.createdAt), 'HH:mm')}</span></div>
        </div>

        <div class="divider"></div>

        <p class="section-label">Buyer</p>
        <p class="bold">${order.shopName || 'Shop'}</p>
        ${order.shopAddress ? `<p style="font-size:11px;color:#666">${order.shopAddress}</p>` : ''}
        <p style="font-size:11px;color:#999;margin-top:2px">${order.deliveryOption} delivery</p>

        <div class="divider"></div>

        <p class="section-label">Items</p>
        <table>
          <thead><tr><th>Item</th><th>Qty</th><th>Price</th><th>Total</th></tr></thead>
          <tbody>
            ${order.items.map(item => {
              const unitPrice = Number(item.unitPrice ?? item.price);
              const lineTotal = Number(item.lineTotal ?? item.quantity * unitPrice);
              const itemHasDiscount = Number(item.discountAmount ?? 0) > 0;
              return `
              <tr>
                <td>${item.productName}${itemHasDiscount ? `<br/><span style="font-size:9px;color:#666">Original R${Number(item.originalUnitPrice ?? unitPrice).toFixed(2)} · ${Number(item.discountPct ?? 0).toFixed(0)}% supplier discount · Save R${Number(item.discountAmount ?? 0).toFixed(2)}/unit<br/><strong style="color:#15803d">Approved R${unitPrice.toFixed(2)}/unit</strong></span>` : ''}</td>
                <td>${item.quantity}</td>
                <td>R${unitPrice.toFixed(2)}</td>
                <td><strong>R${lineTotal.toFixed(2)}</strong></td>
              </tr>`;
            }).join('')}
          </tbody>
        </table>

        <div class="divider"></div>

        ${order.isGroupBuy ? `<div class="row"><span>Order Type</span><span class="bold">GROUP BUY</span></div>` : ''}
        ${order.deliveryDistanceKm != null ? `<div class="row"><span>Delivery Distance</span><span>${order.deliveryDistanceKm.toFixed(1)} km</span></div>` : ''}
        ${hasDiscount ? `
          <div class="row"><span>Original Subtotal</span><span>R${Number(order.originalSubtotal ?? order.subtotal + Number(order.totalDiscount ?? 0)).toFixed(2)}</span></div>
          <div class="row"><span>Total Supplier Discount</span><span>-R${Number(order.totalDiscount ?? 0).toFixed(2)}</span></div>
          <div class="row"><span>Discounted Subtotal</span><span>R${order.subtotal.toFixed(2)}</span></div>
        ` : `<div class="row"><span>Subtotal</span><span>R${order.subtotal.toFixed(2)}</span></div>`}
        <div class="row"><span>Delivery Fee</span><span>R${order.deliveryFee.toFixed(2)}</span></div>
        <div class="row"><span>Platform Commission</span><span>R${order.platformCommission.toFixed(2)}</span></div>

        <div class="total-box">
          <div class="row-bold"><span>TOTAL</span><span class="amount">R${order.total.toFixed(2)}</span></div>
        </div>

        <div class="divider"></div>

        <div class="row"><span style="color:#666">Payment</span><span class="bold">${order.paymentMethod.toUpperCase()}</span></div>
        <div class="row"><span style="color:#666">Status</span><span class="bold" style="text-transform:capitalize">${order.status}</span></div>

        <div class="divider"></div>

        <div class="footer">
          <p>Thank you for your order!</p>
          <small>SpazaSure — Empowering Spaza Shops</small>
          <small style="margin-top:8px">Generated: ${format(new Date(), 'dd MMM yyyy HH:mm')}</small>
        </div>
      </body>
      </html>
    `);
    printWindow.document.close();
    setTimeout(() => { printWindow.print(); }, 300);
  };

  const handleDownloadPDF = () => {
    // Use the same print approach — browser "Save as PDF" from print dialog
    toast.success('Use "Save as PDF" in the print dialog');
    handlePrint();
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 sm:p-6">
      <div className="absolute inset-0 bg-black/50 backdrop-blur-sm animate-fade-in" onClick={onClose} />
      <div className="relative bg-white rounded-2xl shadow-card-lg w-full max-w-lg max-h-[90vh] flex flex-col overflow-hidden animate-scale-in">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 flex-shrink-0">
          <h2 className="text-base font-bold text-gray-900">Order Receipt</h2>
          <div className="flex items-center gap-2">
            <button onClick={handlePrint} className="btn-icon hover:text-primary hover:bg-primary-50" title="Print">
              <Printer size={18} />
            </button>
            <button onClick={onClose} className="btn-icon"><X size={18} /></button>
          </div>
        </div>

        {/* Receipt Content */}
        <div className="overflow-y-auto flex-1 p-6" ref={receiptRef}>
          <div className="space-y-5">
            {/* Branding */}
            <div className="text-center pb-4 border-b border-dashed border-gray-300">
              <div className="w-12 h-12 bg-green-50 rounded-xl flex items-center justify-center mx-auto mb-3">
                <Store size={22} className="text-green-700" />
              </div>
              <h3 className="text-xl font-bold text-green-800">SpazaSure</h3>
              <p className="text-[10px] font-semibold text-gray-400 uppercase tracking-[3px] mt-1">Order Receipt</p>
            </div>

            {/* Order Number & Date */}
            <div className="flex justify-between items-start border-b border-dashed border-gray-300 pb-4">
              <div>
                <p className="text-[10px] font-semibold text-gray-400 uppercase">Order #</p>
                <p className="text-sm font-mono font-bold text-gray-900">{order.orderNumber}</p>
              </div>
              <div className="text-right">
                <p className="text-[10px] font-semibold text-gray-400 uppercase">Date</p>
                <p className="text-sm font-mono font-bold text-gray-900">
                  {format(new Date(order.createdAt), 'dd MMM yyyy')}
                </p>
                <p className="text-xs font-mono text-gray-500">
                  {format(new Date(order.createdAt), 'HH:mm')}
                </p>
              </div>
            </div>

            {/* Buyer */}
            <div className="border-b border-dashed border-gray-300 pb-4">
              <p className="text-[10px] font-semibold text-gray-400 uppercase mb-2">Buyer</p>
              <p className="text-sm font-semibold text-gray-900">{order.shopName || 'Shop'}</p>
              {order.shopAddress && <p className="text-xs text-gray-500 mt-0.5">{order.shopAddress}</p>}
              <p className="text-xs text-gray-400 mt-1 capitalize">{order.deliveryOption} delivery</p>
            </div>

            {/* Items */}
            <div className="border-b border-dashed border-gray-300 pb-4">
              <p className="text-[10px] font-semibold text-gray-400 uppercase mb-3">Items</p>
              <table className="w-full text-xs">
                <thead>
                  <tr className="border-b border-gray-100">
                    <th className="text-left py-1.5 font-semibold text-gray-500">Item</th>
                    <th className="text-center py-1.5 font-semibold text-gray-500 w-12">Qty</th>
                    <th className="text-right py-1.5 font-semibold text-gray-500 w-20">Price</th>
                    <th className="text-right py-1.5 font-semibold text-gray-500 w-20">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {order.items.map((item) => {
                    const unitPrice = Number(item.unitPrice ?? item.price);
                    const lineTotal = Number(item.lineTotal ?? item.quantity * unitPrice);
                    const itemHasDiscount = Number(item.discountAmount ?? 0) > 0;
                    return (
                      <tr key={item.productId} className="border-b border-gray-50 align-top">
                        <td className="py-2 text-gray-900 font-medium">
                          {item.productName}
                          {itemHasDiscount && (
                            <div className="mt-1 text-[10px] leading-4 font-normal text-gray-500">
                              <p>Original: R{Number(item.originalUnitPrice ?? unitPrice).toFixed(2)}</p>
                              <p>{Number(item.discountPct ?? 0).toFixed(0)}% supplier discount · R{Number(item.discountAmount ?? 0).toFixed(2)} per unit</p>
                              <p className="font-semibold text-emerald-700">Approved: R{unitPrice.toFixed(2)} per unit</p>
                            </div>
                          )}
                        </td>
                        <td className="py-2 text-center text-gray-600 font-mono">{item.quantity}</td>
                        <td className="py-2 text-right text-gray-600 font-mono">R{unitPrice.toFixed(2)}</td>
                        <td className="py-2 text-right text-gray-900 font-mono font-semibold">R{lineTotal.toFixed(2)}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {/* Totals */}
            <div className="border-b border-dashed border-gray-300 pb-4 space-y-1.5">
              {hasDiscount ? (
                <>
                  <div className="flex justify-between text-xs text-gray-600">
                    <span>Original Subtotal</span><span className="font-mono">R{Number(order.originalSubtotal ?? order.subtotal + Number(order.totalDiscount ?? 0)).toFixed(2)}</span>
                  </div>
                  <div className="flex justify-between text-xs font-semibold text-emerald-700">
                    <span>Total Supplier Discount</span><span className="font-mono">-R{Number(order.totalDiscount ?? 0).toFixed(2)}</span>
                  </div>
                  <div className="flex justify-between text-xs font-bold text-gray-900">
                    <span>Discounted Subtotal</span><span className="font-mono">R{order.subtotal.toFixed(2)}</span>
                  </div>
                </>
              ) : (
                <div className="flex justify-between text-xs text-gray-600">
                  <span>Subtotal</span><span className="font-mono">R{order.subtotal.toFixed(2)}</span>
                </div>
              )}
              <div className="flex justify-between text-xs text-gray-600">
                <span>Delivery Fee</span><span className="font-mono">R{order.deliveryFee.toFixed(2)}</span>
              </div>
              {order.isGroupBuy && (
                <div className="flex justify-between text-xs">
                  <span className="text-gray-500">Order Type</span>
                  <span className="font-bold text-violet-700">Group Buy</span>
                </div>
              )}
              {order.deliveryDistanceKm != null && (
                <div className="flex justify-between text-xs text-gray-600">
                  <span>Delivery Distance</span><span className="font-mono">{order.deliveryDistanceKm.toFixed(1)} km</span>
                </div>
              )}
              <div className="flex justify-between text-xs text-gray-600">
                <span>Platform Commission</span><span className="font-mono">R{order.platformCommission.toFixed(2)}</span>
              </div>
              <div className="flex justify-between text-sm font-bold text-gray-900 pt-2 mt-2 border-t border-gray-200">
                <span>TOTAL</span>
                <span className="font-mono text-green-700">R{order.total.toFixed(2)}</span>
              </div>
            </div>

            {/* Payment & Status */}
            <div className="border-b border-dashed border-gray-300 pb-4 space-y-2">
              <div className="flex justify-between text-xs">
                <span className="text-gray-500">Payment Method</span>
                <span className="font-semibold text-gray-900 uppercase">{order.paymentMethod}</span>
              </div>
              <div className="flex justify-between text-xs">
                <span className="text-gray-500">Payment Status</span>
                <span className="font-semibold text-gray-900 capitalize">
                  {order.paymentStatus === 'held' ? 'Held in Escrow' : order.paymentStatus}
                </span>
              </div>
              <div className="flex justify-between text-xs">
                <span className="text-gray-500">Order Status</span>
                <span className="font-semibold text-gray-900 capitalize">{order.status}</span>
              </div>
            </div>

            {/* Footer */}
            <div className="text-center pt-2">
              <p className="text-sm font-semibold text-gray-700">Thank you for your order!</p>
              <p className="text-[10px] text-gray-400 mt-1">SpazaSure — Empowering Spaza Shops</p>
              <p className="text-[9px] text-gray-300 mt-2">Generated: {format(new Date(), 'dd MMM yyyy HH:mm')}</p>
            </div>
          </div>
        </div>

        {/* Actions */}
        <div className="px-6 py-4 border-t border-gray-100 flex gap-3 flex-shrink-0">
          <button onClick={handlePrint} className="btn-primary flex-1 flex items-center justify-center gap-2">
            <Printer size={16} /> Print Receipt
          </button>
          <button onClick={handleDownloadPDF} className="btn-secondary flex-1 flex items-center justify-center gap-2">
            <Download size={16} /> Save as PDF
          </button>
        </div>
      </div>
    </div>
  );
}

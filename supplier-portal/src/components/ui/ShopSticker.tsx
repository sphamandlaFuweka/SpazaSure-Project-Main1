import { useRef } from 'react';
import { QRCodeSVG } from 'qrcode.react';
import { Download, ExternalLink, Printer } from 'lucide-react';

/** Where a shop's QR code points: the public page anyone can open to check the shop. */
export const shopVerifyUrl = (shopNumber: string) => `https://portal.spazasure.co.za/verify/${shopNumber}`;

/**
 * A shop's SpazaSure sticker as admins see it: the QR code shoppers scan, the shop number they
 * can type instead, and buttons to print it, save it, or open the page it leads to.
 */
export default function ShopSticker({ shopNumber, shopName, verified }: {
  shopNumber: string;
  shopName: string;
  verified: boolean;
}) {
  const qr = useRef<HTMLDivElement>(null);
  const url = shopVerifyUrl(shopNumber);

  /** A print-ready sticker on its own page, so only the sticker is printed. */
  const print = () => {
    const svg = qr.current?.querySelector('svg')?.outerHTML ?? '';
    const win = window.open('', '_blank', 'width=480,height=640');
    if (!win) return;
    const status = verified ? 'SpazaSure Verified' : 'Check this shop on SpazaSure';
    const escape = (text: string) => text.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);
    win.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>${escape(shopName)} - SpazaSure sticker</title>
<style>
  @page { size: A6; margin: 8mm; }
  body { font-family: Nunito, Arial, sans-serif; margin: 0; display: flex; justify-content: center; }
  .sticker { width: 88mm; border: 3px solid ${verified ? '#078C50' : '#25449A'}; border-radius: 6mm; padding: 6mm; text-align: center; }
  .brand { font-weight: 900; font-size: 20px; color: #25449A; letter-spacing: .5px; }
  .status { margin: 2mm 0 4mm; font-weight: 800; font-size: 13px; color: ${verified ? '#078C50' : '#25449A'}; text-transform: uppercase; }
  .qr svg { width: 60mm; height: 60mm; }
  .number { font-family: monospace; font-size: 24px; font-weight: 800; margin-top: 3mm; letter-spacing: 2px; }
  .name { font-size: 14px; font-weight: 700; margin-top: 1mm; }
  .hint { font-size: 11px; color: #555; margin-top: 3mm; }
</style></head><body><div class="sticker">
  <div class="brand">SpazaSure</div>
  <div class="status">${status}</div>
  <div class="qr">${svg}</div>
  <div class="number">${escape(shopNumber)}</div>
  <div class="name">${escape(shopName)}</div>
  <div class="hint">Scan the code or enter the shop number at portal.spazasure.co.za/verify</div>
</div><script>window.onload = () => { window.print(); };</script></body></html>`);
    win.document.close();
  };

  /** The QR code as an image file, e.g. to send to a printer or the shop. */
  const download = () => {
    const svg = qr.current?.querySelector('svg');
    if (!svg) return;
    const size = 1024;
    const image = new Image();
    image.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = size;
      canvas.height = size;
      const ctx = canvas.getContext('2d')!;
      ctx.fillStyle = '#ffffff';
      ctx.fillRect(0, 0, size, size);
      ctx.drawImage(image, 0, 0, size, size);
      const link = document.createElement('a');
      link.download = `${shopNumber}-qr.png`;
      link.href = canvas.toDataURL('image/png');
      link.click();
    };
    image.src = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(new XMLSerializer().serializeToString(svg))}`;
  };

  return (
    <div className="rounded-2xl border border-gray-100 p-4 flex flex-col sm:flex-row items-center gap-4">
      <div ref={qr} className="rounded-xl bg-white p-2 border border-gray-100 flex-shrink-0">
        <QRCodeSVG value={url} size={132} level="M" marginSize={1} fgColor="#0E1B42" />
      </div>
      <div className="min-w-0 flex-1 text-center sm:text-left">
        <p className="text-[11px] font-bold uppercase tracking-wider text-gray-400">Shop number</p>
        <p className="font-mono text-2xl font-black text-brand-blue tracking-wider">{shopNumber}</p>
        <p className="text-xs text-gray-500 mt-1">Shoppers scan this code, or type the number, to check the shop.</p>
        <div className="flex flex-wrap justify-center sm:justify-start gap-2 mt-3">
          <button type="button" onClick={print} className="btn-primary flex items-center gap-1.5 text-sm">
            <Printer size={14} /> Print sticker
          </button>
          <button type="button" onClick={download} className="btn-secondary flex items-center gap-1.5 text-sm">
            <Download size={14} /> Download QR
          </button>
          <a href={url} target="_blank" rel="noreferrer" className="btn-secondary flex items-center gap-1.5 text-sm">
            <ExternalLink size={14} /> Open
          </a>
        </div>
      </div>
    </div>
  );
}

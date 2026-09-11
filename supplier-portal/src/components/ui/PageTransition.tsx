import { useEffect, useState, useRef } from 'react';
import { useLocation } from 'react-router-dom';

export default function PageTransition({ children }: { children: React.ReactNode }) {
  const location = useLocation();
  const [visible, setVisible] = useState(false);
  const [show, setShow] = useState(false);
  const isFirst = useRef(true);

  useEffect(() => {
    if (isFirst.current) { isFirst.current = false; return; }
    setVisible(true);
    setShow(true);
    const hideTimer = setTimeout(() => setShow(false), 500);
    const removeTimer = setTimeout(() => setVisible(false), 750);
    return () => { clearTimeout(hideTimer); clearTimeout(removeTimer); };
  }, [location.pathname]);

  return (
    <>
      {children}
      {visible && (
        <div
          className="fixed inset-0 z-[9999] flex items-center justify-center pointer-events-none"
          style={{
            background: 'rgba(13,59,15,0.92)',
            opacity: show ? 1 : 0,
            transition: 'opacity 250ms ease',
          }}
        >
          <div
            style={{
              transform: show ? 'scale(1)' : 'scale(0.8)',
              opacity: show ? 1 : 0,
              transition: 'transform 300ms cubic-bezier(0.34,1.56,0.64,1), opacity 250ms ease',
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: 14,
            }}
          >
            {/* Logo */}
            <div style={{
              width: 72, height: 72, borderRadius: 18, overflow: 'hidden',
              boxShadow: '0 0 32px rgba(76,175,80,0.5), 0 0 8px rgba(255,255,255,0.2)',
              border: '2px solid rgba(255,255,255,0.15)',
            }}>
              <img src="/spazasure_logo.jpg" alt="SpazaSure" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
            </div>
            {/* Spinner dots */}
            <div style={{ display: 'flex', gap: 6 }}>
              {[0, 1, 2].map(i => (
                <span key={i} style={{
                  width: 6, height: 6, borderRadius: '50%',
                  background: '#4CAF50',
                  animation: `spaza-dot 0.6s ${i * 0.15}s infinite alternate`,
                }} />
              ))}
            </div>
          </div>
        </div>
      )}
      <style>{`
        @keyframes spaza-dot {
          from { opacity: 0.3; transform: translateY(0); }
          to   { opacity: 1;   transform: translateY(-5px); }
        }
      `}</style>
    </>
  );
}

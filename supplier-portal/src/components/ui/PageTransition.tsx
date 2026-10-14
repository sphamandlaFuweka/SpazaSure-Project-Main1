import { useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';

/**
 * Lightweight route transition: a thin progress bar at the top of the viewport
 * and a short fade-in of the new page. It never blocks the screen, so
 * navigation feels instant.
 */
export default function PageTransition({ children }: { children: React.ReactNode }) {
  const location = useLocation();
  const wrapperRef = useRef<HTMLDivElement>(null);
  const isFirst = useRef(true);
  const [barState, setBarState] = useState<'idle' | 'running' | 'done'>('idle');

  useEffect(() => {
    // The layout's <main> is the scroll container, so a new page would
    // otherwise open at the previous page's scroll position.
    wrapperRef.current?.closest('main')?.scrollTo({ top: 0 });

    if (isFirst.current) { isFirst.current = false; return; }
    setBarState('running');
    const doneTimer = setTimeout(() => setBarState('done'), 250);
    const idleTimer = setTimeout(() => setBarState('idle'), 600);
    return () => { clearTimeout(doneTimer); clearTimeout(idleTimer); };
  }, [location.pathname]);

  return (
    <>
      <div
        aria-hidden="true"
        className="fixed top-0 left-0 z-[9999] h-0.5 bg-gradient-to-r from-primary via-emerald-400 to-accent pointer-events-none"
        style={{
          width: barState === 'idle' ? '0%' : barState === 'running' ? '70%' : '100%',
          opacity: barState === 'done' ? 0 : 1,
          transition: barState === 'idle'
            ? 'none'
            : 'width 250ms ease-out, opacity 300ms ease 100ms',
        }}
      />
      <div ref={wrapperRef} key={location.pathname} className="animate-fade-in">
        {children}
      </div>
    </>
  );
}

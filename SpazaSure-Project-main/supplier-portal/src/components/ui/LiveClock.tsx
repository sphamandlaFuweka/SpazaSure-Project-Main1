import { useState, useEffect, useRef } from 'react';

function Digit({ value, prev }: { value: string; prev: string }) {
  const [flip, setFlip] = useState(false);
  const prevRef = useRef(value);

  useEffect(() => {
    if (value !== prevRef.current) {
      setFlip(true);
      prevRef.current = value;
      const t = setTimeout(() => setFlip(false), 300);
      return () => clearTimeout(t);
    }
  }, [value]);

  return (
    <span
      className="inline-block tabular-nums font-black"
      style={{
        display: 'inline-block',
        transition: flip ? 'transform 0.28s cubic-bezier(0.34,1.56,0.64,1), opacity 0.18s ease' : 'none',
        transform: flip ? 'translateY(-3px) scale(1.18)' : 'translateY(0) scale(1)',
        opacity: flip ? 0.7 : 1,
        color: 'inherit',
      }}
    >
      {value}
    </span>
  );
}

function SecondsArc({ seconds }: { seconds: number }) {
  const r = 18;
  const circ = 2 * Math.PI * r;
  const progress = (seconds / 60) * circ;

  return (
    <svg width="44" height="44" viewBox="0 0 44 44" className="absolute inset-0 -rotate-90">
      {/* Track */}
      <circle cx="22" cy="22" r={r} fill="none" stroke="rgba(27,67,50,0.08)" strokeWidth="2.5" />
      {/* Progress */}
      <circle
        cx="22" cy="22" r={r}
        fill="none"
        stroke="url(#arcGrad)"
        strokeWidth="2.5"
        strokeLinecap="round"
        strokeDasharray={`${progress} ${circ}`}
        style={{ transition: seconds === 0 ? 'none' : 'stroke-dasharray 0.9s cubic-bezier(0.4,0,0.2,1)' }}
      />
      <defs>
        <linearGradient id="arcGrad" x1="0%" y1="0%" x2="100%" y2="0%">
          <stop offset="0%" stopColor="#4CAF50" />
          <stop offset="100%" stopColor="#1B4332" />
        </linearGradient>
      </defs>
    </svg>
  );
}

export default function LiveClock({ variant = 'supplier' }: { variant?: 'supplier' | 'admin' }) {
  const [time, setTime] = useState(new Date());
  const [prevTime, setPrevTime] = useState(new Date());

  useEffect(() => {
    const t = setInterval(() => {
      setPrevTime((p) => p);
      setTime(new Date());
    }, 1000);
    return () => clearInterval(t);
  }, []);

  const h = time.getHours();
  const m = time.getMinutes();
  const s = time.getSeconds();
  const ampm = h >= 12 ? 'PM' : 'AM';
  const h12 = h % 12 || 12;

  const hStr = String(h12).padStart(2, '0');
  const mStr = String(m).padStart(2, '0');
  const sStr = String(s).padStart(2, '0');

  const dayName = time.toLocaleDateString('en-ZA', { weekday: 'short' });
  const dateNum = time.getDate();
  const monthName = time.toLocaleDateString('en-ZA', { month: 'short' });

  const accentColor = variant === 'admin' ? '#3B82F6' : '#1B4332';
  const accentLight = variant === 'admin' ? 'rgba(59,130,246,0.08)' : 'rgba(27,67,50,0.06)';
  const accentBorder = variant === 'admin' ? 'rgba(59,130,246,0.15)' : 'rgba(27,67,50,0.12)';

  return (
    <div
      className="hidden lg:flex items-center gap-3 select-none"
      style={{
        background: `linear-gradient(135deg, ${accentLight} 0%, rgba(255,255,255,0.9) 100%)`,
        border: `1px solid ${accentBorder}`,
        borderRadius: 16,
        padding: '6px 14px 6px 8px',
        backdropFilter: 'blur(8px)',
        boxShadow: `0 1px 8px rgba(0,0,0,0.04), inset 0 1px 0 rgba(255,255,255,0.8)`,
      }}
    >
      {/* Seconds arc ring */}
      <div className="relative w-11 h-11 flex items-center justify-center flex-shrink-0">
        <SecondsArc seconds={s} />
        {/* Live dot */}
        <span
          style={{
            width: 7, height: 7, borderRadius: '50%',
            background: `radial-gradient(circle, #4CAF50 0%, #1B4332 100%)`,
            boxShadow: '0 0 6px rgba(76,175,80,0.8)',
            animation: 'livePulse 1.5s ease-in-out infinite',
            display: 'block',
          }}
        />
      </div>

      {/* Time digits */}
      <div className="flex flex-col gap-0">
        {/* HH:MM:SS */}
        <div className="flex items-baseline gap-0.5" style={{ color: accentColor }}>
          <span style={{ fontSize: 18, letterSpacing: '-0.5px', lineHeight: 1 }}>
            <Digit value={hStr[0]} prev={hStr[0]} />
            <Digit value={hStr[1]} prev={hStr[1]} />
          </span>
          <span style={{ fontSize: 16, fontWeight: 900, opacity: 0.5, animation: 'colonBlink 1s step-end infinite', lineHeight: 1 }}>:</span>
          <span style={{ fontSize: 18, letterSpacing: '-0.5px', lineHeight: 1 }}>
            <Digit value={mStr[0]} prev={mStr[0]} />
            <Digit value={mStr[1]} prev={mStr[1]} />
          </span>
          <span style={{ fontSize: 16, fontWeight: 900, opacity: 0.5, animation: 'colonBlink 1s step-end infinite', lineHeight: 1 }}>:</span>
          <span style={{ fontSize: 13, letterSpacing: '-0.5px', lineHeight: 1, opacity: 0.6 }}>
            <Digit value={sStr[0]} prev={sStr[0]} />
            <Digit value={sStr[1]} prev={sStr[1]} />
          </span>
          <span style={{ fontSize: 10, fontWeight: 800, marginLeft: 3, opacity: 0.7, lineHeight: 1, alignSelf: 'flex-end', paddingBottom: 1 }}>
            {ampm}
          </span>
        </div>

        {/* Date */}
        <div className="flex items-center gap-1" style={{ marginTop: 1 }}>
          <span style={{ fontSize: 10, fontWeight: 700, color: '#9CA3AF', letterSpacing: '0.04em' }}>
            {dayName}
          </span>
          <span style={{ width: 2, height: 2, borderRadius: '50%', background: '#D1D5DB', display: 'inline-block' }} />
          <span style={{ fontSize: 10, fontWeight: 800, color: accentColor, opacity: 0.7 }}>
            {dateNum} {monthName}
          </span>
          <span style={{ width: 2, height: 2, borderRadius: '50%', background: '#D1D5DB', display: 'inline-block' }} />
          <span style={{ fontSize: 9, fontWeight: 700, color: '#9CA3AF', letterSpacing: '0.03em' }}>SAST</span>
        </div>
      </div>

      <style>{`
        @keyframes livePulse {
          0%, 100% { transform: scale(1);   box-shadow: 0 0 6px rgba(76,175,80,0.8); }
          50%       { transform: scale(1.3); box-shadow: 0 0 12px rgba(76,175,80,1); }
        }
        @keyframes colonBlink {
          0%, 100% { opacity: 0.5; }
          50%       { opacity: 0.15; }
        }
      `}</style>
    </div>
  );
}

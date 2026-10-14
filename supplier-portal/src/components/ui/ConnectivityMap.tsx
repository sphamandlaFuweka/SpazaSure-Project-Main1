import clsx from 'clsx';
import { MAP_DOTS_PATH, MAP_VIEW } from './connectivityMapData';

type City = { name: string; lon: number; lat: number; label?: boolean };

// Supplier hubs and the towns they deliver to across South Africa and the region
const CITIES: Record<string, City> = {
  jhb: { name: 'Johannesburg', lon: 28.05, lat: -26.2, label: true },
  pta: { name: 'Pretoria', lon: 28.19, lat: -25.75 },
  dbn: { name: 'Durban', lon: 31.02, lat: -29.86, label: true },
  cpt: { name: 'Cape Town', lon: 18.42, lat: -33.92, label: true },
  pe: { name: 'Gqeberha', lon: 25.6, lat: -33.96, label: true },
  el: { name: 'East London', lon: 27.91, lat: -33.02 },
  bfn: { name: 'Bloemfontein', lon: 26.21, lat: -29.09, label: true },
  kim: { name: 'Kimberley', lon: 24.76, lat: -28.73 },
  plk: { name: 'Polokwane', lon: 29.45, lat: -23.9, label: true },
  mbo: { name: 'Mbombela', lon: 30.97, lat: -25.47 },
  gab: { name: 'Gaborone', lon: 25.91, lat: -24.65 },
  mpm: { name: 'Maputo', lon: 32.57, lat: -25.97 },
  hre: { name: 'Harare', lon: 31.05, lat: -17.83 },
  whk: { name: 'Windhoek', lon: 17.08, lat: -22.56 },
  msu: { name: 'Maseru', lon: 27.48, lat: -29.31 },
  mts: { name: 'Mbabane', lon: 31.13, lat: -26.31 },
  lun: { name: 'Lusaka', lon: 28.28, lat: -15.42 },
};

const LINKS: [keyof typeof CITIES, keyof typeof CITIES][] = [
  ['jhb', 'dbn'], ['jhb', 'cpt'], ['jhb', 'pe'], ['jhb', 'bfn'], ['jhb', 'plk'],
  ['jhb', 'mbo'], ['jhb', 'gab'], ['jhb', 'mpm'], ['jhb', 'hre'], ['jhb', 'whk'],
  ['dbn', 'el'], ['cpt', 'pe'], ['bfn', 'msu'], ['bfn', 'kim'], ['mbo', 'mts'],
  ['hre', 'lun'], ['pe', 'el'], ['jhb', 'pta'],
];

// Brand colours, in logo order
const PULSE_COLORS = ['#F8B217', '#078C50', '#25449A', '#E02526'];

const project = (lon: number, lat: number): [number, number] => [
  ((lon - MAP_VIEW.lonMin) / (MAP_VIEW.lonMax - MAP_VIEW.lonMin)) * MAP_VIEW.width,
  ((MAP_VIEW.latMax - lat) / (MAP_VIEW.latMax - MAP_VIEW.latMin)) * MAP_VIEW.height,
];

/** Gentle curved arc between two points, bowing "up" like a flight path. */
function arcPath(a: [number, number], b: [number, number]) {
  const [x1, y1] = a;
  const [x2, y2] = b;
  const mx = (x1 + x2) / 2;
  const my = (y1 + y2) / 2;
  const dx = x2 - x1;
  const dy = y2 - y1;
  const bow = Math.hypot(dx, dy) * 0.22;
  // Perpendicular offset, always towards the top of the map
  let nx = -dy, ny = dx;
  const len = Math.hypot(nx, ny) || 1;
  nx /= len; ny /= len;
  if (ny > 0) { nx = -nx; ny = -ny; }
  return `M${x1.toFixed(1)} ${y1.toFixed(1)} Q${(mx + nx * bow).toFixed(1)} ${(my + ny * bow).toFixed(1)} ${x2.toFixed(1)} ${y2.toFixed(1)}`;
}

/**
 * Decorative dotted map of southern Africa with animated supplier→shop connections,
 * in the style of the Paystack landing page. Purely visual: hidden from screen readers,
 * and animation stops for users who prefer reduced motion.
 */
export default function ConnectivityMap({ className, tone = 'light' }: { className?: string; tone?: 'light' | 'dark' }) {
  const hub = project(CITIES.jhb.lon, CITIES.jhb.lat);
  // 'dark' = drawn in white on a blue background (e.g. the login hero panel)
  const ink = tone === 'dark' ? '#FFFFFF' : '#25449A';
  const dotOpacity = tone === 'dark' ? 0.16 : 0.2;
  const routeOpacity = tone === 'dark' ? 0.32 : 0.22;
  const labelColor = tone === 'dark' ? '#FFFFFF' : '#5A5A7A';
  const hubLabelColor = tone === 'dark' ? '#FFFFFF' : '#1A3278';

  return (
    <svg
      className={clsx('connectivity-map pointer-events-none select-none', className)}
      viewBox={`0 0 ${MAP_VIEW.width} ${MAP_VIEW.height}`}
      preserveAspectRatio="xMidYMid slice"
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <radialGradient id="map-glow" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor="#F8B217" stopOpacity="0.28" />
          <stop offset="100%" stopColor="#F8B217" stopOpacity="0" />
        </radialGradient>
      </defs>

      {/* Land as dots */}
      <path d={MAP_DOTS_PATH} stroke={ink} strokeOpacity={dotOpacity} strokeWidth={4.2} strokeLinecap="round" fill="none" />

      {/* Soft glow around the Johannesburg hub */}
      <circle cx={hub[0]} cy={hub[1]} r={140} fill="url(#map-glow)" />

      {/* Connections: faint route + a travelling coloured pulse */}
      {LINKS.map(([from, to], i) => {
        const d = arcPath(project(CITIES[from].lon, CITIES[from].lat), project(CITIES[to].lon, CITIES[to].lat));
        const color = PULSE_COLORS[i % PULSE_COLORS.length];
        return (
          <g key={`${from}-${to}`}>
            <path d={d} fill="none" stroke={ink} strokeOpacity={routeOpacity} strokeWidth={1.6} strokeDasharray="2 6" strokeLinecap="round" />
            <path
              d={d} fill="none" stroke={color} strokeWidth={2.6} strokeLinecap="round"
              pathLength={1000} className="map-arc-pulse"
              style={{ animationDelay: `${(i * 0.55) % 6}s`, animationDuration: `${4 + (i % 3)}s` }}
            />
          </g>
        );
      })}

      {/* City nodes */}
      {Object.entries(CITIES).map(([key, city], i) => {
        const [x, y] = project(city.lon, city.lat);
        const isHub = key === 'jhb';
        return (
          <g key={key}>
            <circle cx={x} cy={y} r={isHub ? 9 : 6} fill={isHub ? '#F8B217' : ink} className="map-node-ring"
              style={{ animationDelay: `${(i * 0.4) % 3}s` }} />
            <circle cx={x} cy={y} r={isHub ? 6.5 : 4} fill={isHub ? '#F8B217' : tone === 'dark' ? '#FFFFFF' : '#25449A'} stroke={tone === 'dark' ? '#25449A' : '#fff'} strokeWidth={2} />
            {city.label && tone === 'dark' && isHub && (
              <text x={x + (isHub ? 12 : 9)} y={y + 4} fontSize={isHub ? 15 : 13} fontWeight={800}
                fill={isHub ? hubLabelColor : labelColor} fillOpacity={isHub ? 0.9 : 0.65} fontFamily="Nunito, sans-serif">
                {city.name}
              </text>
            )}
          </g>
        );
      })}
    </svg>
  );
}

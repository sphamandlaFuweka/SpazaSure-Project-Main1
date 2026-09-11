/** Animated skeleton loader shown while page data is loading */
export default function PageLoader({ variant = 'dashboard' }: { variant?: 'dashboard' | 'table' | 'cards' }) {
  if (variant === 'table') return (
    <div className="p-6 space-y-5 animate-fade-in">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="space-y-2">
          <div className="skeleton h-7 w-32 rounded-xl" />
          <div className="skeleton h-4 w-48 rounded-lg" />
        </div>
        <div className="skeleton h-9 w-28 rounded-xl" />
      </div>
      {/* Summary cards */}
      <div className="grid grid-cols-4 gap-3">
        {[...Array(4)].map((_, i) => (
          <div key={i} className="card p-4 flex items-center gap-3">
            <div className="skeleton w-10 h-10 rounded-xl flex-shrink-0" />
            <div className="space-y-2 flex-1">
              <div className="skeleton h-5 w-12 rounded" />
              <div className="skeleton h-3 w-24 rounded" />
            </div>
          </div>
        ))}
      </div>
      {/* Table */}
      <div className="card overflow-hidden">
        <div className="border-b border-gray-100 bg-gray-50/80 px-5 py-3.5 flex gap-6">
          {[...Array(6)].map((_, i) => <div key={i} className="skeleton h-3 rounded w-16" />)}
        </div>
        {[...Array(6)].map((_, i) => (
          <div key={i} className="flex items-center gap-6 px-5 py-4 border-b border-gray-50">
            <div className="skeleton w-11 h-11 rounded-xl flex-shrink-0" />
            <div className="flex-1 space-y-2">
              <div className="skeleton h-3.5 rounded w-36" />
              <div className="skeleton h-3 rounded w-24" />
            </div>
            <div className="skeleton h-3.5 rounded w-16" />
            <div className="skeleton h-3.5 rounded w-20" />
            <div className="skeleton h-6 rounded-full w-20" />
            <div className="skeleton h-6 rounded-full w-16" />
          </div>
        ))}
      </div>
    </div>
  );

  if (variant === 'cards') return (
    <div className="p-6 space-y-5 animate-fade-in">
      <div className="flex items-center justify-between">
        <div className="space-y-2">
          <div className="skeleton h-7 w-32 rounded-xl" />
          <div className="skeleton h-4 w-48 rounded-lg" />
        </div>
        <div className="skeleton h-9 w-28 rounded-xl" />
      </div>
      <div className="grid grid-cols-3 gap-3">
        {[...Array(3)].map((_, i) => (
          <div key={i} className="card p-4 flex items-center gap-3">
            <div className="skeleton w-10 h-10 rounded-xl" />
            <div className="space-y-2 flex-1">
              <div className="skeleton h-5 w-10 rounded" />
              <div className="skeleton h-3 w-28 rounded" />
            </div>
          </div>
        ))}
      </div>
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
        {[...Array(8)].map((_, i) => (
          <div key={i} className="card overflow-hidden">
            <div className="skeleton w-full h-36" />
            <div className="p-3 space-y-2">
              <div className="skeleton h-4 rounded w-3/4" />
              <div className="skeleton h-3 rounded w-1/2" />
              <div className="flex justify-between mt-2">
                <div className="skeleton h-5 rounded w-16" />
                <div className="skeleton h-4 rounded w-20" />
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );

  // Dashboard (default)
  return (
    <div className="p-6 space-y-5 animate-fade-in">
      {/* Welcome banner skeleton */}
      <div className="skeleton rounded-2xl h-32 w-full" />

      {/* KPI cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {[...Array(4)].map((_, i) => (
          <div key={i} className="card p-5 space-y-3">
            <div className="flex items-start justify-between">
              <div className="space-y-2 flex-1">
                <div className="skeleton h-3 rounded w-24" />
                <div className="skeleton h-8 rounded w-28" />
                <div className="skeleton h-4 rounded-full w-20" />
              </div>
              <div className="skeleton w-10 h-10 rounded-xl" />
            </div>
          </div>
        ))}
      </div>

      {/* Quick actions */}
      <div className="grid grid-cols-4 gap-3">
        {[...Array(4)].map((_, i) => <div key={i} className="skeleton h-14 rounded-2xl" />)}
      </div>

      {/* Charts row */}
      <div className="grid grid-cols-3 gap-4">
        <div className="card p-5 col-span-2 space-y-4">
          <div className="flex justify-between">
            <div className="space-y-2">
              <div className="skeleton h-4 rounded w-32" />
              <div className="skeleton h-3 rounded w-48" />
            </div>
            <div className="skeleton h-6 rounded-full w-16" />
          </div>
          <div className="skeleton h-52 rounded-xl w-full" />
        </div>
        <div className="card p-5 space-y-4">
          <div className="skeleton h-4 rounded w-28" />
          {[...Array(4)].map((_, i) => (
            <div key={i} className="space-y-1.5">
              <div className="flex justify-between">
                <div className="skeleton h-3 rounded w-24" />
                <div className="skeleton h-3 rounded w-6" />
              </div>
              <div className="skeleton h-1.5 rounded-full w-full" />
            </div>
          ))}
        </div>
      </div>

      {/* Bottom row */}
      <div className="grid grid-cols-3 gap-4">
        <div className="card p-5 space-y-3">
          <div className="skeleton h-4 rounded w-28" />
          <div className="skeleton h-44 rounded-xl w-full" />
        </div>
        <div className="card col-span-2">
          <div className="px-5 py-4 border-b border-gray-100 flex justify-between">
            <div className="skeleton h-4 rounded w-28" />
            <div className="skeleton h-4 rounded w-16" />
          </div>
          {[...Array(5)].map((_, i) => (
            <div key={i} className="flex items-center gap-4 px-5 py-3.5 border-b border-gray-50">
              <div className="skeleton h-3.5 rounded w-24" />
              <div className="flex-1 space-y-1.5">
                <div className="skeleton h-3.5 rounded w-32" />
                <div className="skeleton h-3 rounded w-24" />
              </div>
              <div className="skeleton h-3.5 rounded w-16" />
              <div className="skeleton h-6 rounded-full w-20" />
              <div className="skeleton h-3 rounded w-20" />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

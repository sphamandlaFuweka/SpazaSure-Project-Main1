import { lazy, Suspense } from 'react';
import PageLoader from './components/ui/PageLoader';

// The target is a build-time constant, so the other app's code never reaches this bundle.
const Portal = __APP_TARGET__ === 'admin'
  ? lazy(() => import('./AdminApp'))
  : lazy(() => import('./SupplierApp'));

export default function App() {
  return (
    <Suspense fallback={<PageLoader variant="dashboard" />}>
      <Portal />
    </Suspense>
  );
}

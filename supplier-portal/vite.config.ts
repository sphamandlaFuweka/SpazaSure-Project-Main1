import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';

type Target = 'supplier' | 'admin';

const titles: Record<Target, string> = {
  supplier: 'SpazaSure – Supplier Portal',
  admin: 'SpazaSure – Admin Console',
};

// The supplier portal and the admin console are two separate builds of the same codebase.
// `__APP_TARGET__` is a constant, so everything belonging to the other app is dropped from the bundle.
export function createConfig(target: Target) {
  const titlePlugin: Plugin = {
    name: 'app-title',
    transformIndexHtml: (html) => html.replace(/<title>.*<\/title>/, `<title>${titles[target]}</title>`),
  };

  return defineConfig({
    plugins: [react(), titlePlugin],
    define: { __APP_TARGET__: JSON.stringify(target) },
    resolve: {
      alias: { '@': '/src' },
    },
    server: { port: target === 'admin' ? 3001 : 3000 },
    build: {
      outDir: target === 'admin' ? 'dist-admin' : 'dist',
      // The gzip size report needs extra memory and got the Docker build OOM-killed.
      reportCompressedSize: false,
      sourcemap: false,
      rollupOptions: {
        output: {
          // Smaller chunks lower peak memory while rendering and speed up first load.
          manualChunks: {
            react: ['react', 'react-dom', 'react-router-dom'],
            charts: ['recharts'],
            maps: ['leaflet', 'react-leaflet'],
          },
        },
      },
    },
  });
}

export default createConfig('supplier');

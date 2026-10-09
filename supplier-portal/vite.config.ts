import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': '/src' },
  },
  server: { port: 3000 },
  build: {
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

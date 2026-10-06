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
  },
});

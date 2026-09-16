import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:7071',
        changeOrigin: true,
        // The Functions host serves routes at /api/... already (Route = "environments" maps to
        // /api/environments) — no path rewrite needed.
      },
    },
  },
})

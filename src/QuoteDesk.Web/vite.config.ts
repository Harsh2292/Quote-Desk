import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    // Matches the Authorized JavaScript origin registered for the Google OAuth client
    // (http://localhost:8080) — Google Identity Services rejects a mismatched origin outright.
    port: 8080,
    proxy: {
      // Same-origin from the browser's point of view — no CORS configuration needed on the Api.
      '/health': 'http://localhost:5080',
      '/api': 'http://localhost:5080',
    },
  },
})

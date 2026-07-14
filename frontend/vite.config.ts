import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Puerto 3000: es el origen permitido por el CORS del Gateway.
export default defineConfig({
  plugins: [react()],
  server: { port: 3000 },
})

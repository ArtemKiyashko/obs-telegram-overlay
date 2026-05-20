import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'dist',
    assetsDir: '.',
    rollupOptions: {
      output: {
        entryFileNames: 'overlay.js',
        chunkFileNames: 'overlay.js',
        assetFileNames: (assetInfo) => {
          if (assetInfo.name && assetInfo.name.slice(-4) === '.css') {
            return 'overlay.css'
          }

          return 'assets/[name][extname]'
        },
      },
    },
  },
})

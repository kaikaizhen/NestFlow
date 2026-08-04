import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  const basePath = env.VITE_BASE_PATH || '/'

  return {
    base: basePath,

    plugins: [
      vue(),

      VitePWA({
        registerType: 'autoUpdate',

        base: basePath,
        scope: basePath,

        includeAssets: [
          'favicon.svg',
          'maskable-icon.svg',
        ],

        manifest: {
          name: 'NestFlow 個人管家',
          short_name: 'NestFlow',
          description: '記帳、行程與提醒的個人管家',
          lang: 'zh-Hant-TW',

          start_url: basePath,
          scope: basePath,

          display: 'standalone',
          orientation: 'portrait',
          background_color: '#f7f7f8',
          theme_color: '#ffffff',

          icons: [
            {
              src: 'favicon.svg',
              sizes: 'any',
              type: 'image/svg+xml',
              purpose: 'any',
            },
            {
              src: 'maskable-icon.svg',
              sizes: 'any',
              type: 'image/svg+xml',
              purpose: 'maskable',
            },
          ],
        },

        workbox: {
          globPatterns: [
            '**/*.{js,css,html,svg,png,woff2}',
          ],

          navigateFallbackDenylist: [
            /^\/api\//,
            /^\/nestflow\/backend\//,
          ],
        },

        devOptions: {
          enabled: false,
        },
      }),
    ],

    server: {
      port: 5173,
    },
  }
})
import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import './styles/main.css'
// 提早載入以立即套用手動覆蓋的主題，避免使用者進到設定頁前主題閃一下
import './stores/theme'

createApp(App).use(router).mount('#app')

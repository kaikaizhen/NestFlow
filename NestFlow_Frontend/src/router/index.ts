import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

/**
 * 底部導航對應的三個主頁面。第一版只有這三條路由。
 */
const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/calendar',
  },
  {
    path: '/calendar',
    name: 'calendar',
    component: () => import('../views/CalendarView.vue'),
    meta: { title: '行事曆' },
  },
  {
    path: '/ledger',
    name: 'ledger',
    component: () => import('../views/LedgerView.vue'),
    meta: { title: '記帳' },
  },
  {
    path: '/settings',
    name: 'settings',
    component: () => import('../views/SettingsView.vue'),
    meta: { title: '設定' },
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/calendar',
  },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
  scrollBehavior: () => ({ top: 0 }),
})

router.afterEach((to) => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title}｜NestFlow` : 'NestFlow'
})

export default router

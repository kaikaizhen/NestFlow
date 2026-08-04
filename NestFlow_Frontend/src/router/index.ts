import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuth } from '../stores/auth'

/**
 * 底部導航的四個主頁面，以及各自底下的子頁面。
 * meta.public 為 true 的路由不需登入。
 */
const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('../views/LoginView.vue'),
    meta: { title: '登入', public: true, hideNav: true },
  },
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
    path: '/calendar/new',
    name: 'event-create',
    component: () => import('../views/calendar/EventEditView.vue'),
    meta: { title: '新增行程' },
  },
  {
    path: '/calendar/:eventId/edit',
    name: 'event-edit',
    component: () => import('../views/calendar/EventEditView.vue'),
    meta: { title: '編輯行程' },
  },
  {
    path: '/todos',
    name: 'todos',
    component: () => import('../views/TodosView.vue'),
    meta: { title: '代辦' },
  },
  {
    path: '/todos/new',
    name: 'todo-create',
    component: () => import('../views/todos/TodoEditView.vue'),
    meta: { title: '新增代辦' },
  },
  {
    path: '/todos/:todoId/edit',
    name: 'todo-edit',
    component: () => import('../views/todos/TodoEditView.vue'),
    meta: { title: '編輯代辦' },
  },
  {
    path: '/ledger',
    name: 'ledger',
    component: () => import('../views/LedgerView.vue'),
    meta: { title: '記帳' },
  },
  {
    path: '/ledger/new',
    name: 'entry-create',
    component: () => import('../views/ledger/EntryEditView.vue'),
    meta: { title: '新增記帳' },
  },
  {
    path: '/ledger/:entryId/edit',
    name: 'entry-edit',
    component: () => import('../views/ledger/EntryEditView.vue'),
    meta: { title: '編輯記帳' },
  },
  {
    path: '/settings',
    name: 'settings',
    component: () => import('../views/SettingsView.vue'),
    meta: { title: '設定' },
  },
  {
    path: '/settings/workspaces',
    name: 'workspaces',
    component: () => import('../views/settings/WorkspacesView.vue'),
    meta: { title: '資料空間' },
  },
  {
    path: '/settings/workspaces/:workspaceId/members',
    name: 'workspace-members',
    component: () => import('../views/settings/MembersView.vue'),
    meta: { title: '家庭成員' },
  },
  {
    path: '/settings/timezone',
    name: 'timezone',
    component: () => import('../views/settings/TimeZoneView.vue'),
    meta: { title: '時區' },
  },
  {
    path: '/settings/line-binding',
    name: 'line-binding',
    component: () => import('../views/settings/LineBindingView.vue'),
    meta: { title: 'LINE 綁定' },
  },
  {
    path: '/settings/join-family',
    name: 'join-family',
    component: () => import('../views/settings/JoinFamilyView.vue'),
    meta: { title: '加入家庭' },
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

router.beforeEach(async (to) => {
  const { isAuthenticated, isResolved, refresh } = useAuth()

  // 首次進入或登入導回後，先向後端確認 Session 是否有效
  if (!isResolved.value) {
    await refresh()
  }

  if (to.meta.public) {
    // 已登入就不需要再看登入頁
    return isAuthenticated.value && to.name === 'login' ? { name: 'calendar' } : true
  }

  return isAuthenticated.value ? true : { name: 'login' }
})

router.afterEach((to) => {
  const title = to.meta.title as string | undefined
  document.title = title ? `${title}｜NestFlow` : 'NestFlow'
})

export default router

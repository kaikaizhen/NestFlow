import { computed, readonly, ref } from 'vue'
import { ApiError, api, type CurrentUser } from '../services/apiClient'

/**
 * 登入狀態。以模組層級的 ref 共用，整個 App 只有一份。
 */
const currentUser = ref<CurrentUser | null>(null)
const isLoading = ref(false)
const isResolved = ref(false)

export function useAuth() {
  /** 向後端確認登入狀態。App 啟動與登入導回後各呼叫一次。 */
  async function refresh(): Promise<CurrentUser | null> {
    isLoading.value = true

    try {
      currentUser.value = await api.me()
    } catch (error) {
      // 401 代表未登入，屬正常狀態，不視為錯誤
      if (!(error instanceof ApiError && error.isUnauthorized)) {
        console.error('取得登入狀態失敗', error)
      }

      currentUser.value = null
    } finally {
      isLoading.value = false
      isResolved.value = true
    }

    return currentUser.value
  }

  async function logout() {
    await api.logout()
    currentUser.value = null
  }

  function setDefaultWorkspaceId(workspaceId: string) {
    if (currentUser.value) {
      currentUser.value = { ...currentUser.value, defaultWorkspaceId: workspaceId }
    }
  }

  return {
    currentUser: readonly(currentUser),
    isLoading: readonly(isLoading),
    isResolved: readonly(isResolved),
    isAuthenticated: computed(() => currentUser.value !== null),
    refresh,
    logout,
    setDefaultWorkspaceId,
  }
}

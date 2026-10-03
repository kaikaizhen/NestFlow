import { computed, readonly, ref } from 'vue'
import { ApiError, api, type CurrentUser } from '../services/apiClient'

/** 後端重啟（重新發布）期間會短暫回 502/503 或連線失敗，這類暫時性錯誤不該被當成「未登入」。 */
const TRANSIENT_RETRY_COUNT = 8
const TRANSIENT_RETRY_DELAY_MS = 1500

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

/**
 * 登入狀態。以模組層級的 ref 共用，整個 App 只有一份。
 */
const currentUser = ref<CurrentUser | null>(null)
const isLoading = ref(false)
const isResolved = ref(false)

/** 只有 401 代表未登入；其餘錯誤（後端重啟、網路中斷）重試後仍失敗才往外丟。 */
async function fetchCurrentUserWithRetry(): Promise<CurrentUser> {
  for (let attempt = 1; ; attempt++) {
    try {
      return await api.me()
    } catch (error) {
      const isUnauthorized = error instanceof ApiError && error.isUnauthorized
      const isClientError = error instanceof ApiError && error.status >= 400 && error.status < 500

      if (isUnauthorized || isClientError || attempt >= TRANSIENT_RETRY_COUNT) {
        throw error
      }

      await sleep(TRANSIENT_RETRY_DELAY_MS)
    }
  }
}

export function useAuth() {
  /** 向後端確認登入狀態。App 啟動與登入導回後各呼叫一次。 */
  async function refresh(): Promise<CurrentUser | null> {
    isLoading.value = true

    try {
      currentUser.value = await fetchCurrentUserWithRetry()
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

import { computed, readonly, ref } from 'vue'
import { api, type Workspace } from '../services/apiClient'
import { useAuth } from './auth'

const STORAGE_KEY = 'nestflow.activeWorkspaceId'

const workspaces = ref<Workspace[]>([])
const activeId = ref<string | null>(localStorage.getItem(STORAGE_KEY))
const isLoaded = ref(false)

/**
 * 目前作用中的資料空間。優先沿用上次選擇，其次使用者的預設空間，最後取第一個。
 */
export function useWorkspaces() {
  const { currentUser } = useAuth()

  const active = computed(
    () => workspaces.value.find((w) => w.id === activeId.value) ?? null,
  )

  async function load(force = false) {
    if (isLoaded.value && !force) {
      return
    }

    workspaces.value = await api.listWorkspaces()
    isLoaded.value = true

    const stillValid = workspaces.value.some((w) => w.id === activeId.value)

    if (!stillValid) {
      const fallback =
        workspaces.value.find((w) => w.id === currentUser.value?.defaultWorkspaceId) ??
        workspaces.value[0]

      setActive(fallback?.id ?? null)
    }
  }

  function setActive(workspaceId: string | null) {
    activeId.value = workspaceId

    if (workspaceId) {
      localStorage.setItem(STORAGE_KEY, workspaceId)
    } else {
      localStorage.removeItem(STORAGE_KEY)
    }
  }

  /** 登出時清除，避免下一位使用者沿用到無權存取的空間。 */
  function reset() {
    workspaces.value = []
    isLoaded.value = false
    setActive(null)
  }

  return {
    workspaces: readonly(workspaces),
    active,
    load,
    setActive,
    reset,
  }
}

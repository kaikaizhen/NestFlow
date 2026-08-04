import { computed, readonly, ref } from 'vue'
import { api, type Workspace } from '../services/apiClient'
import { useAuth } from './auth'

const workspaces = ref<Workspace[]>([])
const isLoaded = ref(false)

/**
 * 資料空間清單與「目前作用中」的空間。
 * 作用中空間一律等於使用者在設定頁選擇的預設資料空間，不再另外提供頁面內切換器；
 * 記帳與行事曆頁想看其他空間的資料，請先到設定改預設資料空間。
 * 新增記帳或行程時仍可在表單內另外選擇要存放的資料空間（不影響這裡的作用中空間）。
 */
export function useWorkspaces() {
  const { currentUser } = useAuth()

  const active = computed(
    () =>
      workspaces.value.find((w) => w.id === currentUser.value?.defaultWorkspaceId) ??
      workspaces.value[0] ??
      null,
  )

  async function load(force = false) {
    if (isLoaded.value && !force) {
      return
    }

    workspaces.value = await api.listWorkspaces()
    isLoaded.value = true
  }

  /** 登出時清除，避免下一位使用者沿用到無權存取的空間。 */
  function reset() {
    workspaces.value = []
    isLoaded.value = false
  }

  return {
    workspaces: readonly(workspaces),
    active,
    load,
    reset,
  }
}

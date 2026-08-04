import { computed, readonly, ref } from 'vue'

/** null 代表跟隨系統，light/dark 代表使用者手動覆蓋後記住的選擇。 */
export type ThemePreference = 'light' | 'dark' | null

const STORAGE_KEY = 'nestflow.theme'

const media = window.matchMedia('(prefers-color-scheme: dark)')

function readStoredPreference(): ThemePreference {
  const stored = localStorage.getItem(STORAGE_KEY)
  return stored === 'light' || stored === 'dark' ? stored : null
}

const preference = ref<ThemePreference>(readStoredPreference())
const systemPrefersDark = ref(media.matches)

/** 目前實際顯示的主題：使用者手動選過就以手動為準，否則跟隨系統設定。 */
const resolvedTheme = computed<'light' | 'dark'>(
  () => preference.value ?? (systemPrefersDark.value ? 'dark' : 'light'),
)

/**
 * 把主題寫到 <html> 的 data-theme，CSS 依此套用對應色票（見 styles/main.css）。
 * 沒有手動覆蓋時故意不寫入屬性，讓 CSS 的 prefers-color-scheme 媒體查詢直接生效，
 * 這樣系統之後切換深色/淺色時，畫面會立即跟著變、不需要重新整理。
 */
function applyToDocument() {
  const root = document.documentElement

  if (preference.value) {
    root.dataset.theme = preference.value
  } else {
    delete root.dataset.theme
  }

  // 同步瀏覽器工具列顏色（Android 網址列、iOS Safari 狀態列背景）
  const meta = document.querySelector('meta[name="theme-color"]')
  meta?.setAttribute('content', resolvedTheme.value === 'dark' ? '#0b0b0c' : '#ffffff')
}

// 系統設定改變時（例如手機排程在日落自動切換深色），沒有手動覆蓋就即時跟著換
media.addEventListener('change', (event) => {
  systemPrefersDark.value = event.matches
  applyToDocument()
})

// 模組載入時立即套用一次，避免畫面先以錯誤主題閃一下
applyToDocument()

export function useTheme() {
  function setPreference(next: ThemePreference) {
    preference.value = next

    if (next) {
      localStorage.setItem(STORAGE_KEY, next)
    } else {
      localStorage.removeItem(STORAGE_KEY)
    }

    applyToDocument()
  }

  /** 設定頁的深色模式開關：切換一次就成為手動覆蓋，之後不再跟隨系統。 */
  function toggleDark() {
    setPreference(resolvedTheme.value === 'dark' ? 'light' : 'dark')
  }

  return {
    preference: readonly(preference),
    resolvedTheme: readonly(resolvedTheme),
    setPreference,
    toggleDark,
  }
}

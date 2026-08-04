import { onMounted, onUnmounted } from 'vue'

/**
 * 定期重新整理資料，並在頁面從背景切回前景（切換 App、螢幕從熄滅喚醒、
 * 切回瀏覽器分頁）時立即補抓一次，讓家庭成員間的異動不用等使用者手動
 * 下拉重新整理才看得到。分頁在背景時暫停輪詢，避免浪費電量與後端負載。
 *
 * 這不是真正的即時推送（沒有 WebSocket），異動要等下一次輪詢或使用者
 * 切回頁面才會看到，但不需要新增後端基礎設施。
 */
export function useAutoRefresh(callback: () => void | Promise<void>, intervalMs = 30000) {
  let timer: ReturnType<typeof setInterval> | null = null

  function start() {
    stop()
    timer = setInterval(callback, intervalMs)
  }

  function stop() {
    if (timer) {
      clearInterval(timer)
      timer = null
    }
  }

  function handleVisibilityChange() {
    if (document.visibilityState === 'visible') {
      callback()
      start()
    } else {
      stop()
    }
  }

  onMounted(() => {
    if (document.visibilityState === 'visible') {
      start()
    }

    document.addEventListener('visibilitychange', handleVisibilityChange)
    window.addEventListener('focus', callback)
  })

  onUnmounted(() => {
    stop()
    document.removeEventListener('visibilitychange', handleVisibilityChange)
    window.removeEventListener('focus', callback)
  })
}

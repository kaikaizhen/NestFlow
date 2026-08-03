/**
 * 後端 API 位址由環境變數提供，不寫死於程式碼。
 * 開發環境見 .env.development，容器部署由建置參數注入。
 */
const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export interface ApiHealth {
  service: string
  status: string
  environment: string
  serverTime: string
}

export async function getApiHealth(signal?: AbortSignal): Promise<ApiHealth> {
  const response = await fetch(`${baseUrl}/api/health`, {
    signal,
    credentials: 'include',
  })

  if (!response.ok) {
    throw new Error(`API 回應狀態 ${response.status}`)
  }

  return (await response.json()) as ApiHealth
}

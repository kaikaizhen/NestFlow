/**
 * 後端 API 位址由環境變數提供，不寫死於程式碼。
 * 開發環境見 .env.development，容器部署由建置參數注入。
 */
export const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

/** 後端回傳的錯誤訊息。 */
export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }

  get isUnauthorized() {
    return this.status === 401
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    // Session 以 Cookie 傳遞，跨來源請求必須帶上認證資訊
    credentials: 'include',
    headers: {
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    const message = await readErrorMessage(response)
    throw new ApiError(message, response.status)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

async function readErrorMessage(response: Response): Promise<string> {
  try {
    const body = await response.json()
    return body?.message ?? body?.title ?? '操作失敗，請稍後再試。'
  } catch {
    return '操作失敗，請稍後再試。'
  }
}

// ---------------------------------------------------------------
// 型別
// ---------------------------------------------------------------
export interface ApiHealth {
  service: string
  status: string
  environment: string
  serverTime: string
}

export interface CurrentUser {
  id: string
  displayName: string
  pictureUrl: string | null
  defaultWorkspaceId: string | null
  isLineLinked: boolean
}

export type WorkspaceType = 'personal' | 'family'
export type MembershipType = 'owner' | 'member'

export interface Workspace {
  id: string
  name: string
  type: WorkspaceType
  membershipType: MembershipType
  isDefault: boolean
  createdAt: string
}

export interface WorkspaceMember {
  userId: string
  displayName: string
  pictureUrl: string | null
  membershipType: MembershipType
  joinedAt: string
}

export interface Invitation {
  code: string
  expiresAt: string
}

// ---------------------------------------------------------------
// 端點
// ---------------------------------------------------------------
export const api = {
  health: () => request<ApiHealth>('/api/health'),

  me: () => request<CurrentUser>('/api/auth/me'),

  logout: () => request<void>('/api/auth/logout', { method: 'POST' }),

  /** 導向 LINE 授權頁。由瀏覽器整頁跳轉，不能用 fetch。 */
  gotoLineLogin: () => {
    window.location.href = `${apiBaseUrl}/api/auth/line/login`
  },

  devLogin: (externalSubject: string, displayName: string) =>
    request<{ displayName: string }>('/api/dev/auth/login', {
      method: 'POST',
      body: JSON.stringify({ externalSubject, displayName }),
    }),

  listWorkspaces: () => request<Workspace[]>('/api/workspaces'),

  createWorkspace: (name: string, type: WorkspaceType) =>
    request<Workspace>('/api/workspaces', {
      method: 'POST',
      body: JSON.stringify({ name, type }),
    }),

  renameWorkspace: (workspaceId: string, name: string) =>
    request<Workspace>(`/api/workspaces/${workspaceId}`, {
      method: 'PUT',
      body: JSON.stringify({ name }),
    }),

  deleteWorkspace: (workspaceId: string) =>
    request<void>(`/api/workspaces/${workspaceId}`, { method: 'DELETE' }),

  setDefaultWorkspace: (workspaceId: string) =>
    request<void>('/api/workspaces/default', {
      method: 'PUT',
      body: JSON.stringify({ workspaceId }),
    }),

  createInvitation: (workspaceId: string) =>
    request<Invitation>(`/api/workspaces/${workspaceId}/invitations`, { method: 'POST' }),

  joinWorkspace: (code: string) =>
    request<Workspace>('/api/workspaces/join', {
      method: 'POST',
      body: JSON.stringify({ code }),
    }),

  listMembers: (workspaceId: string) =>
    request<WorkspaceMember[]>(`/api/workspaces/${workspaceId}/members`),

  removeMember: (workspaceId: string, userId: string) =>
    request<void>(`/api/workspaces/${workspaceId}/members/${userId}`, { method: 'DELETE' }),
}

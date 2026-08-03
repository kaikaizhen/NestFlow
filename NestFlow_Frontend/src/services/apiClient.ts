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
  /** IANA 時區名稱，前端據此換算顯示時間與月份區間。 */
  timeZone: string
  isLineLinked: boolean
  /** 是否已綁定 LINE 官方帳號，綁定後才能用 LINE 訊息記帳。 */
  isLineMessagingLinked: boolean
  /** 後端是否已設定 Messaging Channel。未設定時不顯示綁定入口。 */
  isLineMessagingConfigured: boolean
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

export interface LineBot {
  displayName: string
  pictureUrl: string | null
  /** 加好友連結。手機開啟直接跳 LINE，電腦開啟顯示 QR Code。 */
  addFriendUrl: string
}

export type EntryType = 'expense' | 'income'

export interface AccountEntry {
  id: string
  type: EntryType
  amount: number
  currency: string
  category: string
  note: string | null
  occurredAt: string
  createdByUserId: string
  createdByDisplayName: string
}

export interface CalendarEvent {
  id: string
  title: string
  description: string | null
  startAt: string
  endAt: string
  createdByUserId: string
  createdByDisplayName: string
}

export interface SaveCalendarEventPayload {
  workspaceId: string
  title: string
  description: string | null
  /** 帶時區的 ISO 字串，後端會轉為 UTC 保存。 */
  startAt: string
  endAt: string
}

export interface CurrencySummary {
  currency: string
  income: number
  expense: number
  balance: number
}

export interface Category {
  code: string
  label: string
  type: EntryType
}

export interface SaveAccountEntryPayload {
  workspaceId: string
  type: EntryType
  amount: number
  currency: string
  category: string
  note: string | null
  /** 帶時區的 ISO 字串，後端會轉為 UTC 保存。 */
  occurredAt: string
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

  /** 取得官方帳號資訊。後端未設定 Messaging Channel 時回 204，這裡轉成 null。 */
  getLineBot: () => request<LineBot | undefined>('/api/auth/me/line-bot').then((v) => v ?? null),

  /** 產生 LINE 官方帳號綁定碼。明文只會回傳這一次。 */
  createBindingCode: () =>
    request<Invitation>('/api/auth/me/binding-code', { method: 'POST' }),

  updateTimeZone: (timeZone: string) =>
    request<void>('/api/auth/me/timezone', {
      method: 'PUT',
      body: JSON.stringify({ timeZone }),
    }),

  listCategories: () => request<Category[]>('/api/account-entries/categories'),

  listEntries: (workspaceId: string, fromUtc: string, toUtc: string, limit?: number) =>
    request<AccountEntry[]>(
      `/api/account-entries?workspaceId=${workspaceId}&from=${encodeURIComponent(fromUtc)}` +
        `&to=${encodeURIComponent(toUtc)}${limit ? `&limit=${limit}` : ''}`,
    ),

  summarize: (workspaceId: string, fromUtc: string, toUtc: string) =>
    request<CurrencySummary[]>(
      `/api/account-entries/summary?workspaceId=${workspaceId}` +
        `&from=${encodeURIComponent(fromUtc)}&to=${encodeURIComponent(toUtc)}`,
    ),

  getEntry: (entryId: string) => request<AccountEntry>(`/api/account-entries/${entryId}`),

  createEntry: (payload: SaveAccountEntryPayload) =>
    request<AccountEntry>('/api/account-entries', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  updateEntry: (entryId: string, payload: SaveAccountEntryPayload) =>
    request<AccountEntry>(`/api/account-entries/${entryId}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  deleteEntry: (entryId: string) =>
    request<void>(`/api/account-entries/${entryId}`, { method: 'DELETE' }),

  /** 取得與區間有重疊的行程，依開始時間由早到晚排序。 */
  listEvents: (workspaceId: string, fromUtc: string, toUtc: string, limit?: number) =>
    request<CalendarEvent[]>(
      `/api/calendar-events?workspaceId=${workspaceId}&from=${encodeURIComponent(fromUtc)}` +
        `&to=${encodeURIComponent(toUtc)}${limit ? `&limit=${limit}` : ''}`,
    ),

  getEvent: (eventId: string) => request<CalendarEvent>(`/api/calendar-events/${eventId}`),

  createEvent: (payload: SaveCalendarEventPayload) =>
    request<CalendarEvent>('/api/calendar-events', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  updateEvent: (eventId: string, payload: SaveCalendarEventPayload) =>
    request<CalendarEvent>(`/api/calendar-events/${eventId}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),

  deleteEvent: (eventId: string) =>
    request<void>(`/api/calendar-events/${eventId}`, { method: 'DELETE' }),
}

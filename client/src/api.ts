const fromEnv = import.meta.env.VITE_API_BASE as string | undefined
const API_BASE =
  fromEnv ?? (import.meta.env.DEV ? "/api" : (() => {
    throw new Error("VITE_API_BASE must be set for production builds")
  })()) // возможно будет другой порт, смотри вывод dotnet run

export type AccountConfig = {
  id: string
  email: string
  displayName: string
  providerHint: string
  imap: { host: string; port: number; useSsl: boolean; useStartTls: boolean }
  smtp: { host: string; port: number; useSsl: boolean; useStartTls: boolean }
  pop3?: { host: string; port: number; useSsl: boolean; useStartTls: boolean } | null
}

export type FolderDto = { id: string; name: string; unread: number; role: string }
export type MessageHeaderDto = {
  id: string
  folderId: string
  subject: string
  fromName: string
  fromEmail: string
  dateIso: string
  isUnread: boolean
  hasAttachments: boolean
  size: number
}
export type ListMessagesResponse = { items: MessageHeaderDto[]; nextCursor?: string | null }
export type MessageDto = {
  id: string
  folderId: string
  subject: string
  fromName: string
  fromEmail: string
  to: string[]
  cc: string[]
  dateIso: string
  isUnread: boolean
  hasAttachments: boolean
  bodyHtml: string
  bodyText: string
  attachments: { id: string; fileName: string; contentType: string; size: number }[]
}

async function http<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      "content-type": "application/json",
      ...(init?.headers ?? {})
    }
  })
  if (!res.ok) {
    const text = await res.text().catch(() => "")
    throw new Error(text || `http ${res.status}`)
  }
  return (await res.json()) as T
}

export const api = {
  listAccounts: () => http<AccountConfig[]>("/accounts"),
  createAccount: (body: { email: string; displayName: string; password?: string; providerHint?: string }) =>
    http<{ config: AccountConfig }>("/accounts", { method: "POST", body: JSON.stringify(body) }),
  deleteAccount: (id: string) => fetch(`${API_BASE}/accounts/${id}`, { method: "DELETE" }),

  listFolders: (accountId: string) => http<FolderDto[]>(`/mail/${accountId}/folders`),
  listMessages: (accountId: string, folderId: string, cursor?: string | null, pageSize = 50) =>
    http<ListMessagesResponse>(
      `/mail/${accountId}/messages?folderId=${encodeURIComponent(folderId)}&pageSize=${pageSize}${
        cursor ? `&cursor=${encodeURIComponent(cursor)}` : ""
      }`
    ),
  getMessage: (accountId: string, messageId: string) =>
    http<MessageDto>(`/mail/${accountId}/message?messageId=${encodeURIComponent(messageId)}`),
  patchMessage: (accountId: string, messageId: string, body: any) =>
    fetch(`${API_BASE}/mail/${accountId}/message?messageId=${encodeURIComponent(messageId)}`, {
      method: "PATCH",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body)
    }),
  send: (accountId: string, body: any) =>
    fetch(`${API_BASE}/mail/${accountId}/send`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body)
    })
}

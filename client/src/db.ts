import Dexie, { Table } from "dexie"

export type Account = {
  id: string
  email: string
  displayName: string
  providerHint: string
}

export type Folder = {
  accountId: string
  id: string
  name: string
  role: string
  unread: number
  updatedAt: string
}

export type MessageHeader = {
  accountId: string
  id: string
  folderId: string
  subject: string
  fromName: string
  fromEmail: string
  dateIso: string
  isUnread: boolean
  hasAttachments: boolean
  size: number
  cachedAt: string
}

export type MessageBody = {
  accountId: string
  id: string
  folderId: string
  bodyHtml: string
  bodyText: string
  attachments: { id: string; fileName: string; contentType: string; size: number }[]
  cachedAt: string
}

export type OutboxItem = {
  id: string
  accountId: string
  createdAt: string
  to: string
  cc?: string
  bcc?: string
  subject: string
  bodyText: string
  bodyHtml?: string
  sentFolderId: string
  status: "queued" | "sending" | "failed"
  lastError?: string
}

class MailDb extends Dexie {
  accounts!: Table<Account, string>
  folders!: Table<Folder, [string, string]>
  headers!: Table<MessageHeader, [string, string]>
  bodies!: Table<MessageBody, [string, string]>
  outbox!: Table<OutboxItem, string>

  constructor() {
    super("mailclient")
    this.version(1).stores({
      accounts: "id, email",
      folders: "[accountId+id], accountId, role, updatedAt",
      headers: "[accountId+id], accountId, folderId, dateIso, isUnread, fromEmail, subject, cachedAt",
      bodies: "[accountId+id], accountId, folderId, cachedAt",
      outbox: "id, accountId, createdAt, status"
    })
  }
}

export const db = new MailDb()

import { create } from "zustand"
import { db, type Account, type Folder, type MessageHeader, type MessageBody, type OutboxItem } from "./db"
import { api } from "./api"

type Toast = { id: string; text: string }

type State = {
  online: boolean
  accounts: Account[]
  selectedAccountId?: string
  folders: Folder[]
  selectedFolderId?: string
  headers: MessageHeader[]
  selectedMessageId?: string
  message?: MessageBody
  loading: boolean
  composeOpen: boolean
  toasts: Toast[]

  init: () => Promise<void>
  setOnline: (v: boolean) => void
  addToast: (text: string) => void
  removeToast: (id: string) => void

  addAccount: (email: string, displayName: string, password: string) => Promise<void>
  removeAccount: (id: string) => Promise<void>

  selectAccount: (id: string) => Promise<void>
  selectFolder: (folderId: string) => Promise<void>
  loadMore: () => Promise<void>
  selectMessage: (id: string) => Promise<void>

  markRead: (id: string, read: boolean) => Promise<void>
  moveTo: (id: string, folderId: string) => Promise<void>
  del: (id: string) => Promise<void>

  openCompose: () => void
  closeCompose: () => void
  queueSend: (item: Omit<OutboxItem, "id" | "createdAt" | "status">) => Promise<void>
  flushOutbox: () => Promise<void>

  _cursor?: string | null
}

function nowIso() {
  return new Date().toISOString()
}

export const useStore = create<State>((set, get) => ({
  online: navigator.onLine,
  accounts: [],
  folders: [],
  headers: [],
  loading: false,
  composeOpen: false,
  toasts: [],

  init: async () => {
    // hydrate from local cache first
    const [accounts, folders] = await Promise.all([db.accounts.toArray(), db.folders.toArray()])
    set({ accounts, folders })

    if (accounts[0]?.id) {
      set({ selectedAccountId: accounts[0].id })
      await get().selectAccount(accounts[0].id)
    }

    // refresh from server if online
    if (get().online) {
      try {
        const remote = await api.listAccounts()
        const mapped: Account[] = remote.map((a) => ({
          id: a.id,
          email: a.email,
          displayName: a.displayName,
          providerHint: a.providerHint
        }))
        await db.accounts.bulkPut(mapped)
        set({ accounts: mapped })
      } catch {
        // ignore
      }
    }
  },

  setOnline: (v) => set({ online: v }),

  addToast: (text) => {
    const id = crypto.randomUUID()
    set((s) => ({ toasts: [...s.toasts, { id, text }] }))
    setTimeout(() => get().removeToast(id), 3500)
  },

  removeToast: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),

  addAccount: async (email, displayName, password) => {
    set({ loading: true })
    try {
      const res = await api.createAccount({ email, displayName, password })
      const a: Account = {
        id: res.config.id,
        email: res.config.email,
        displayName: res.config.displayName,
        providerHint: res.config.providerHint
      }
      await db.accounts.put(a)
      set((s) => ({ accounts: [...s.accounts, a] }))
      get().addToast("account added")
      await get().selectAccount(a.id)
    } finally {
      set({ loading: false })
    }
  },

  removeAccount: async (id) => {
    await api.deleteAccount(id)
    await db.accounts.delete(id)
    await db.folders.where("accountId").equals(id).delete()
    await db.headers.where("accountId").equals(id).delete()
    await db.bodies.where("accountId").equals(id).delete()
    set((s) => ({
      accounts: s.accounts.filter((a) => a.id !== id),
      folders: s.folders.filter((f) => f.accountId !== id),
      headers: s.headers.filter((h) => h.accountId !== id),
      selectedAccountId: s.selectedAccountId === id ? undefined : s.selectedAccountId
    }))
    get().addToast("account removed")
  },

  selectAccount: async (id) => {
    set({ selectedAccountId: id, loading: true, selectedFolderId: undefined, headers: [], selectedMessageId: undefined, message: undefined })
    try {
      // local folders
      const localFolders = await db.folders.where("accountId").equals(id).toArray()
      set({ folders: localFolders })

      // pick inbox or first
      const inbox = localFolders.find((f) => f.role === "inbox")
      const folderId = inbox?.id ?? localFolders[0]?.id

      if (get().online) {
        const remoteFolders = await api.listFolders(id)
        const mapped: Folder[] = remoteFolders.map((f) => ({
          accountId: id,
          id: f.id,
          name: f.name,
          role: f.role,
          unread: f.unread,
          updatedAt: nowIso()
        }))
        await db.folders.where("accountId").equals(id).delete()
        await db.folders.bulkPut(mapped)
        set({ folders: mapped })
      }

      const finalFolders = await db.folders.where("accountId").equals(id).toArray()
      const finalInbox = finalFolders.find((f) => f.role === "inbox")
      await get().selectFolder(finalInbox?.id ?? folderId ?? "INBOX")
    } finally {
      set({ loading: false })
    }
  },

  selectFolder: async (folderId) => {
    const accountId = get().selectedAccountId
    if (!accountId) return

    set({ selectedFolderId: folderId, headers: [], selectedMessageId: undefined, message: undefined, loading: true, _cursor: null })
    try {
      // show cached first
      const cached = await db.headers
        .where("[accountId+id]")
        .between([accountId, ""], [accountId, "\uffff"])
        .filter((h) => h.folderId === folderId)
        .sortBy("dateIso")
      cached.reverse()
      set({ headers: cached })

      if (!get().online) return

      const res = await api.listMessages(accountId, folderId, null, 50)
      const mapped: MessageHeader[] = res.items.map((m) => ({
        accountId,
        id: m.id,
        folderId: m.folderId,
        subject: m.subject,
        fromName: m.fromName,
        fromEmail: m.fromEmail,
        dateIso: m.dateIso,
        isUnread: m.isUnread,
        hasAttachments: m.hasAttachments,
        size: m.size,
        cachedAt: nowIso()
      }))

      // upsert headers for that folder
      // simple: delete cached headers for folder then put new page, for demo
      const existing = await db.headers.where("accountId").equals(accountId).toArray()
      const keep = existing.filter((x) => x.folderId !== folderId)
      await db.headers.clear()
      await db.headers.bulkPut([...keep, ...mapped])

      set({ headers: mapped, _cursor: res.nextCursor ?? null })
    } finally {
      set({ loading: false })
    }
  },

  loadMore: async () => {
    const { selectedAccountId: accountId, selectedFolderId: folderId, _cursor, online } = get()
    if (!accountId || !folderId || !online || !_cursor) return

    set({ loading: true })
    try {
      const res = await api.listMessages(accountId, folderId, _cursor, 50)
      const mapped: MessageHeader[] = res.items.map((m) => ({
        accountId,
        id: m.id,
        folderId: m.folderId,
        subject: m.subject,
        fromName: m.fromName,
        fromEmail: m.fromEmail,
        dateIso: m.dateIso,
        isUnread: m.isUnread,
        hasAttachments: m.hasAttachments,
        size: m.size,
        cachedAt: nowIso()
      }))

      // append in memory
      set((s) => ({ headers: [...s.headers, ...mapped], _cursor: res.nextCursor ?? null }))

      // persist
      await db.headers.bulkPut(mapped)
    } finally {
      set({ loading: false })
    }
  },

  selectMessage: async (id) => {
    const accountId = get().selectedAccountId
    if (!accountId) return

    set({ selectedMessageId: id, loading: true })
    try {
      // cached body
      const cached = await db.bodies.get([accountId, id])
      if (cached) set({ message: cached })

      if (!get().online) return

      const m = await api.getMessage(accountId, id)
      const body: MessageBody = {
        accountId,
        id: m.id,
        folderId: m.folderId,
        bodyHtml: m.bodyHtml || "",
        bodyText: m.bodyText || "",
        attachments: m.attachments ?? [],
        cachedAt: nowIso()
      }
      await db.bodies.put(body)
      set({ message: body })

      // optimistic mark read
      if (m.isUnread) {
        void get().markRead(id, true)
      }
    } finally {
      set({ loading: false })
    }
  },

  markRead: async (id, read) => {
    const accountId = get().selectedAccountId
    if (!accountId) return

    // optimistic local update
    set((s) => ({
      headers: s.headers.map((h) => (h.id === id ? { ...h, isUnread: !read } : h))
    }))
    await db.headers.update([accountId, id], { isUnread: !read })

    if (!get().online) return

    await api.patchMessage(accountId, id, read ? { markRead: true } : { markUnread: true })
  },

  moveTo: async (id, folderId) => {
    const accountId = get().selectedAccountId
    if (!accountId) return

    // optimistic remove from list
    set((s) => ({ headers: s.headers.filter((h) => h.id !== id) }))
    await db.headers.delete([accountId, id])

    if (!get().online) return
    await api.patchMessage(accountId, id, { moveToFolderId: folderId })
  },

  del: async (id) => {
    const accountId = get().selectedAccountId
    if (!accountId) return

    set((s) => ({ headers: s.headers.filter((h) => h.id !== id) }))
    await db.headers.delete([accountId, id])

    if (!get().online) return
    await api.patchMessage(accountId, id, { delete: true })
  },

  openCompose: () => set({ composeOpen: true }),
  closeCompose: () => set({ composeOpen: false }),

  queueSend: async (item) => {
    const id = crypto.randomUUID()
    const out: OutboxItem = { ...item, id, createdAt: nowIso(), status: "queued" }
    await db.outbox.put(out)
    get().addToast(get().online ? "sending…" : "queued (offline)")
    set({ composeOpen: false })

    // try immediate
    void get().flushOutbox()

    // schedule bg sync
    if ("serviceWorker" in navigator && "SyncManager" in window) {
      try {
        const reg = await navigator.serviceWorker.ready
        await reg.sync.register("outbox-sync")
      } catch {
        // ignore
      }
    }
  },

  flushOutbox: async () => {
    if (!get().online) return
    const accountId = get().selectedAccountId
    if (!accountId) return

    const items = await db.outbox.where("status").anyOf("queued", "failed").toArray()
    for (const it of items) {
      try {
        await db.outbox.update(it.id, { status: "sending", lastError: undefined })
        const res = await api.send(it.accountId, {
          folderIdSent: it.sentFolderId,
          to: it.to,
          cc: it.cc,
          bcc: it.bcc,
          subject: it.subject,
          bodyText: it.bodyText,
          bodyHtml: it.bodyHtml
        })
        if (!res.ok) {
          const t = await res.text().catch(() => "")
          throw new Error(t || `send failed ${res.status}`)
        }
        await db.outbox.delete(it.id)
        get().addToast("sent")
      } catch (e: any) {
        await db.outbox.update(it.id, { status: "failed", lastError: String(e?.message ?? e) })
        get().addToast("send failed (will retry)")
      }
    }
  }
}))

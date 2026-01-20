import React, { useMemo, useState } from "react"
import { useStore } from "../store"

function fmt(iso: string) {
  const d = new Date(iso)
  return d.toLocaleString(undefined, { month: "short", day: "2-digit" })
}

export function MailList() {
  const headers = useStore((s) => s.headers)
  const selectedMessageId = useStore((s) => s.selectedMessageId)
  const selectMessage = useStore((s) => s.selectMessage)
  const loadMore = useStore((s) => s.loadMore)
  const loading = useStore((s) => s.loading)

  const [q, setQ] = useState("")

  const filtered = useMemo(() => {
    const qq = q.trim().toLowerCase()
    if (!qq) return headers
    return headers.filter((h) => {
      return (
        (h.subject || "").toLowerCase().includes(qq) ||
        (h.fromEmail || "").toLowerCase().includes(qq) ||
        (h.fromName || "").toLowerCase().includes(qq)
      )
    })
  }, [headers, q])

  return (
    <div className="items">
      <div style={{ display: "flex", gap: 10, padding: "4px 4px 10px 4px" }}>
        <input className="search" placeholder="filter cached list" value={q} onChange={(e) => setQ(e.target.value)} />
        <button className="btn" onClick={() => void loadMore()} disabled={loading}>
          more
        </button>
      </div>

      {filtered.map((m) => (
        <div
          key={m.id}
          className={`item ${selectedMessageId === m.id ? "active" : ""}`}
          onClick={() => void selectMessage(m.id)}
        >
          <div className="itemTop">
            <div className={m.isUnread ? "bold" : ""}>{m.fromName || m.fromEmail || "(unknown)"}</div>
            <div className="muted">{fmt(m.dateIso)}</div>
          </div>
          <div className={m.isUnread ? "bold" : ""}>{m.subject || "(no subject)"}</div>
          <div className="muted">
            {m.hasAttachments ? "📎 " : ""}
            {m.fromEmail}
          </div>
        </div>
      ))}

      {filtered.length === 0 ? <div className="muted" style={{ padding: 12 }}>no messages</div> : null}
    </div>
  )
}

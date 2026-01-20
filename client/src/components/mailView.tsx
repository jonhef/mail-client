import React, { useMemo } from "react"
import { useStore } from "../store"

export function MailView() {
  const msgId = useStore((s) => s.selectedMessageId)
  const body = useStore((s) => s.message)
  const headers = useStore((s) => s.headers)
  const markRead = useStore((s) => s.markRead)
  const del = useStore((s) => s.del)

  const header = useMemo(() => headers.find((h) => h.id === msgId), [headers, msgId])

  if (!msgId || !header) {
    return <div className="muted">select a message</div>
  }

  const html = body?.id === msgId ? body.bodyHtml : ""
  const text = body?.id === msgId ? body.bodyText : ""

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 10, alignItems: "center" }}>
        <div>
          <div style={{ fontSize: 18, fontWeight: 750 }}>{header.subject || "(no subject)"}</div>
          <div className="kv">
            <span>from</span>
            <span style={{ color: "var(--fg)" }}>{header.fromName || header.fromEmail}</span>
            <span className="muted">{header.fromEmail}</span>
          </div>
        </div>
        <div style={{ display: "flex", gap: 10 }}>
          <button className="btn" onClick={() => void markRead(msgId, header.isUnread)} title="toggle read">
            {header.isUnread ? "mark read" : "mark unread"}
          </button>
          <button className="btn" onClick={() => void del(msgId)} title="delete">
            delete
          </button>
        </div>
      </div>

      <div className="hr" />

      {!body || body.id !== msgId ? (
        <div className="muted">loading body or offline without cached body</div>
      ) : html ? (
        <div
          style={{ fontSize: 14, lineHeight: 1.55 }}
          dangerouslySetInnerHTML={{ __html: sanitizeBasic(html) }}
        />
      ) : (
        <pre style={{ whiteSpace: "pre-wrap", fontSize: 14, lineHeight: 1.55, margin: 0 }}>{text}</pre>
      )}

      {body?.attachments?.length ? (
        <>
          <div className="hr" />
          <div className="muted">attachments</div>
          <div style={{ display: "grid", gap: 8, marginTop: 8 }}>
            {body.attachments.map((a) => (
              <div key={a.id} className="card" style={{ padding: 10 }}>
                <div style={{ fontWeight: 650 }}>{a.fileName}</div>
                <div className="muted">{a.contentType}</div>
              </div>
            ))}
          </div>
        </>
      ) : null}
    </div>
  )
}

// супер-минимальная санитизация: выкидываем script и on* атрибуты, без иллюзий
function sanitizeBasic(html: string) {
  return html
    .replace(/<script[\s\S]*?>[\s\S]*?<\/script>/gi, "")
    .replace(/\son\w+="[^"]*"/gi, "")
    .replace(/\son\w+='[^']*'/gi, "")
}

import React, { useMemo, useState } from "react"
import { useStore } from "../store"

export function Compose() {
  const close = useStore((s) => s.closeCompose)
  const queueSend = useStore((s) => s.queueSend)
  const selectedAccountId = useStore((s) => s.selectedAccountId)
  const folders = useStore((s) => s.folders)

  const sent = useMemo(() => folders.find((f) => f.role === "sent")?.id ?? "Sent", [folders])

  const [to, setTo] = useState("")
  const [subject, setSubject] = useState("")
  const [body, setBody] = useState("")

  if (!selectedAccountId) return null

  return (
    <div className="composeOverlay" onMouseDown={close}>
      <div className="card compose" onMouseDown={(e) => e.stopPropagation()}>
        <div className="headerbar" style={{ padding: 12 }}>
          <div style={{ fontWeight: 750 }}>new message</div>
          <div style={{ display: "flex", gap: 10 }}>
            <button className="btn" onClick={close}>close</button>
            <button
              className="btn primary"
              onClick={() =>
                void queueSend({
                  accountId: selectedAccountId,
                  to,
                  subject,
                  bodyText: body,
                  sentFolderId: sent
                })
              }
              disabled={!to.trim()}
            >
              send
            </button>
          </div>
        </div>

        <div className="composeGrid">
          <input className="field" placeholder="to (comma-separated)" value={to} onChange={(e) => setTo(e.target.value)} />
          <input className="field" placeholder="subject" value={subject} onChange={(e) => setSubject(e.target.value)} />
          <textarea className="field" placeholder="message" value={body} onChange={(e) => setBody(e.target.value)} />
          <div className="muted">html editor, attachments, signatures, pgp will plug in here next</div>
        </div>
      </div>
    </div>
  )
}

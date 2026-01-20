import React, { useMemo, useState } from "react"
import { useStore } from "../store"
import { AddAccountWizard } from "./addAccountWizard"

export function Sidebar() {
  const accounts = useStore((s) => s.accounts)
  const folders = useStore((s) => s.folders)
  const selectedAccountId = useStore((s) => s.selectedAccountId)
  const selectedFolderId = useStore((s) => s.selectedFolderId)
  const selectAccount = useStore((s) => s.selectAccount)
  const selectFolder = useStore((s) => s.selectFolder)
  const removeAccount = useStore((s) => s.removeAccount)
  const loading = useStore((s) => s.loading)

  const [open, setOpen] = useState(false)

  const accountFolders = useMemo(() => folders.filter((f) => f.accountId === selectedAccountId), [folders, selectedAccountId])

  return (
    <div style={{ display: "grid", gap: 12 }}>
      <div className="headerbar">
        <div style={{ fontWeight: 700 }}>mail</div>
        <button className="btn" onClick={() => setOpen((v) => !v)} disabled={loading}>
          + account
        </button>
      </div>

      {open ? (
        <AddAccountWizard onClose={() => setOpen(false)} />
      ) : null}

      <div className="card" style={{ padding: 10, display: "grid", gap: 8 }}>
        <div className="muted">accounts</div>
        {accounts.length === 0 ? <div className="muted">no accounts</div> : null}
        {accounts.map((a) => (
          <div key={a.id} style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <button
              className="btn"
              style={{ flex: 1, textAlign: "left" }}
              onClick={() => void selectAccount(a.id)}
            >
              <div style={{ fontWeight: 650 }}>{a.displayName}</div>
              <div className="muted">{a.email}</div>
            </button>
            <button className="btn" onClick={() => void removeAccount(a.id)} title="remove">
              ✕
            </button>
          </div>
        ))}
      </div>

      <div className="card" style={{ padding: 10, display: "grid", gap: 8 }}>
        <div className="muted">folders</div>
        {accountFolders.map((f) => (
          <button
            key={f.id}
            className="btn"
            onClick={() => void selectFolder(f.id)}
            style={{
              textAlign: "left",
              borderColor: selectedFolderId === f.id ? "rgba(0,122,255,0.25)" : "var(--line)",
              background: selectedFolderId === f.id ? "rgba(0,122,255,0.08)" : "rgba(255,255,255,0.7)"
            }}
          >
            <div style={{ display: "flex", justifyContent: "space-between", gap: 10 }}>
              <span style={{ fontWeight: 650 }}>{f.name}</span>
              {f.unread > 0 ? <span className="pill">{f.unread}</span> : <span className="muted"> </span>}
            </div>
          </button>
        ))}
      </div>
    </div>
  )
}

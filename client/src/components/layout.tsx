import React from "react"
import { useStore } from "../store"
import { Sidebar } from "./sidebar"
import { MailList } from "./mailList"
import { MailView } from "./mailView"
import { Compose } from "./compose"

export function Layout() {
  const composeOpen = useStore((s) => s.composeOpen)
  const online = useStore((s) => s.online)
  const openCompose = useStore((s) => s.openCompose)

  return (
    <>
      <div className="container">
        <div className="panel sidebar">
          <Sidebar />
        </div>

        <div className="panel">
          <div className="list">
            <div className="listTop">
              <input className="search" placeholder="search (cached)" disabled />
              <button className="btn primary" onClick={openCompose}>
                compose
              </button>
              <span className="pill">{online ? "online" : "offline"}</span>
            </div>
            <MailList />
          </div>
        </div>

        <div className="viewer">
          <div className="card viewerInner">
            <MailView />
          </div>
        </div>
      </div>

      {composeOpen ? <Compose /> : null}
    </>
  )
}

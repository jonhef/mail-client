import React from "react"
import { useStore } from "../store"

export function Toasts() {
  const toasts = useStore((s) => s.toasts)
  return (
    <div className="toastWrap">
      {toasts.map((t) => (
        <div key={t.id} className="toast">
          {t.text}
        </div>
      ))}
    </div>
  )
}

import React, { useEffect } from "react"
import { useStore } from "./store"
import { useOnline } from "./hooks/useOnline"
import { Layout } from "./components/layout"
import { Toasts } from "./components/toasts"

export function App() {
  useOnline()
  const init = useStore((s) => s.init)
  const online = useStore((s) => s.online)
  const flush = useStore((s) => s.flushOutbox)

  useEffect(() => {
    void init()
  }, [init])

  useEffect(() => {
    if (online) void flush()
  }, [online, flush])

  // messages from sw
  useEffect(() => {
    const onMsg = (ev: MessageEvent) => {
      if (ev.data?.type === "SW_FLUSH_OUTBOX") void flush()
    }
    navigator.serviceWorker?.addEventListener("message", onMsg)
    return () => navigator.serviceWorker?.removeEventListener("message", onMsg)
  }, [flush])

  return (
    <>
      <Layout />
      <Toasts />
    </>
  )
}

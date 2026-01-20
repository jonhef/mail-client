import { useEffect } from "react"
import { useStore } from "../store"

export function useOnline() {
  const setOnline = useStore((s) => s.setOnline)

  useEffect(() => {
    const on = () => setOnline(true)
    const off = () => setOnline(false)
    window.addEventListener("online", on)
    window.addEventListener("offline", off)
    return () => {
      window.removeEventListener("online", on)
      window.removeEventListener("offline", off)
    }
  }, [setOnline])
}

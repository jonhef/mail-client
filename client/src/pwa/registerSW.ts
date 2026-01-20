export function registerSW() {
  if (!("serviceWorker" in navigator)) return

  window.addEventListener("load", async () => {
    try {
      const reg = await navigator.serviceWorker.register("/sw.js")
      // optional: listen updates
      void reg
    } catch {
      // ignore
    }
  })
}

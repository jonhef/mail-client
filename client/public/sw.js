/* simple sw: app shell cache + outbox background sync
   note: background sync works mainly in chromium pwa, safari may not
*/

const CACHE = "mailclient-v1"
const APP_SHELL = [
  "/",
  "/index.html",
  "/manifest.webmanifest"
]

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(CACHE).then((c) => c.addAll(APP_SHELL)).then(() => self.skipWaiting())
  )
})

self.addEventListener("activate", (event) => {
  event.waitUntil(self.clients.claim())
})

self.addEventListener("fetch", (event) => {
  const url = new URL(event.request.url)

  // cache-first for app shell
  if (event.request.method === "GET" && url.origin === self.location.origin) {
    event.respondWith(
      caches.match(event.request).then((cached) => cached || fetch(event.request))
    )
    return
  }
})

// background sync for outbox
self.addEventListener("sync", (event) => {
  if (event.tag === "outbox-sync") {
    event.waitUntil(flushOutbox())
  }
})

async function flushOutbox() {
  // we can’t access indexeddb easily without duplicating schema here
  // so we ask open clients to flush, best-effort
  const clients = await self.clients.matchAll({ includeUncontrolled: true })
  for (const c of clients) {
    c.postMessage({ type: "SW_FLUSH_OUTBOX" })
  }
}

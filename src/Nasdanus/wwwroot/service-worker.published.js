// Keep the app online-first on static hosting. Old PWA caches can serve stale
// Blazor WASM files whose integrity hashes no longer match the current build.
const cacheNamePrefix = "nasdanus-pwa-";

self.addEventListener("install", event => {
    self.skipWaiting();
});

self.addEventListener("activate", event => {
    event.waitUntil(clearOldCachesAndClaimClients());
});

self.addEventListener("fetch", event => {
    if (event.request.method !== "GET") {
        return;
    }

    event.respondWith(fetch(event.request).catch(() => fallbackFor(event.request)));
});

async function clearOldCachesAndClaimClients() {
    const cacheKeys = await caches.keys();
    await Promise.all(
        cacheKeys
            .filter(key => key.startsWith(cacheNamePrefix))
            .map(key => caches.delete(key)));
    await self.clients.claim();
}

async function fallbackFor(request) {
    if (request.mode === "navigate") {
        const cachedIndex = await caches.match("index.html");
        if (cachedIndex) {
            return cachedIndex;
        }
    }

    throw new Error(`Network request failed for ${request.url}`);
}

// In production, the service worker enables offline loading of the published static app.
self.importScripts("./service-worker-assets.js");

const cacheNamePrefix = "nasdanus-pwa-";
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [
    /\.dll$/,
    /\.pdb$/,
    /\.wasm$/,
    /\.html$/,
    /\.js$/,
    /\.json$/,
    /\.css$/,
    /\.svg$/,
    /\.png$/,
    /\.woff$/,
    /\.woff2$/,
    /\.dat$/
];
const offlineAssetsExclude = [/^service-worker\.js$/];

self.addEventListener("install", event => {
    self.skipWaiting();
    event.waitUntil(onInstall());
});

self.addEventListener("activate", event => {
    event.waitUntil(onActivate());
});

self.addEventListener("fetch", event => {
    if (event.request.method !== "GET") {
        return;
    }

    event.respondWith(onFetch(event));
});

async function onInstall() {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: "no-cache" }));

    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    let cachedResponse = null;
    if (event.request.mode === "navigate") {
        cachedResponse = await caches.match("index.html");
    } else {
        cachedResponse = await caches.match(event.request);
    }

    return cachedResponse || fetch(event.request);
}

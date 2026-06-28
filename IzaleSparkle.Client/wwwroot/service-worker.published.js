// Izale Sparkle — Production Service Worker
// Offline-first strategy with cache-then-network for assets

const CACHE_VERSION = 'izale-sparkle-v2';
const OFFLINE_URL = '/offline.html';

// Assets to pre-cache on install
const PRECACHE_ASSETS = [
  '/',
  '/index.html',
  '/offline.html',
  '/manifest.webmanifest',
  '/css/app.css',
  '/js/app.js',
  '/_framework/blazor.webassembly.js',
];

// Install: pre-cache critical assets
self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_VERSION).then(cache => {
      return cache.addAll(PRECACHE_ASSETS).catch(() => {
        // Continue even if some assets fail (e.g. in dev)
      });
    }).then(() => self.skipWaiting())
  );
});

// Activate: clean up old caches
self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(
        keys.filter(key => key !== CACHE_VERSION).map(key => caches.delete(key))
      )
    ).then(() => self.clients.claim())
  );
});

// Fetch: stale-while-revalidate for navigation, cache-first for assets
self.addEventListener('fetch', event => {
  const { request } = event;
  const url = new URL(request.url);

  // Only handle same-origin requests
  if (url.origin !== self.location.origin) return;

  // Skip non-GET
  if (request.method !== 'GET') return;

  // Blazor framework files (boot manifest + assemblies) — NETWORK FIRST so a new
  // deploy is always picked up. Cache is only a fallback for offline use. Serving
  // these cache-first would keep running a stale app (missing newly added routes
  // like /reset-password) even after a deploy.
  if (url.pathname.startsWith('/_framework/')) {
    event.respondWith(
      fetch(request)
        .then(response => {
          const clone = response.clone();
          caches.open(CACHE_VERSION).then(c => c.put(request, clone));
          return response;
        })
        .catch(() => caches.match(request))
    );
    return;
  }

  // Navigation requests — return app shell or offline page
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then(response => {
          const clone = response.clone();
          caches.open(CACHE_VERSION).then(c => c.put(request, clone));
          return response;
        })
        .catch(() => caches.match('/index.html').then(r => r || caches.match(OFFLINE_URL)))
    );
    return;
  }

  // CSS / JS / fonts — cache first, fallback to network
  if (url.pathname.match(/\.(css|js|woff2?|ttf)$/)) {
    event.respondWith(
      caches.match(request).then(cached => {
        const networkFetch = fetch(request).then(response => {
          const clone = response.clone();
          caches.open(CACHE_VERSION).then(c => c.put(request, clone));
          return response;
        });
        return cached || networkFetch;
      })
    );
    return;
  }

  // Images — cache first, stale-while-revalidate
  if (url.pathname.match(/\.(png|jpg|jpeg|svg|webp|gif|ico)$/)) {
    event.respondWith(
      caches.match(request).then(cached => {
        const networkFetch = fetch(request).then(response => {
          const clone = response.clone();
          caches.open(CACHE_VERSION).then(c => c.put(request, clone));
          return response;
        }).catch(() => cached);
        return cached || networkFetch;
      })
    );
    return;
  }

  // Everything else — network first, cache fallback
  event.respondWith(
    fetch(request)
      .then(response => {
        if (response.ok) {
          const clone = response.clone();
          caches.open(CACHE_VERSION).then(c => c.put(request, clone));
        }
        return response;
      })
      .catch(() => caches.match(request))
  );
});

// Push notifications support
self.addEventListener('push', event => {
  if (!event.data) return;
  const data = event.data.json();
  event.waitUntil(
    self.registration.showNotification(data.title || 'Izale Sparkle', {
      body: data.body || 'Where Every Look Sparkles ✦',
      icon: '/icons/icon-192.png',
      badge: '/icons/icon-72.png',
      vibrate: [100, 50, 100],
      data: { url: data.url || '/' },
      actions: [
        { action: 'open', title: 'View Now' },
        { action: 'dismiss', title: 'Dismiss' }
      ]
    })
  );
});

self.addEventListener('notificationclick', event => {
  event.notification.close();
  if (event.action !== 'dismiss') {
    event.waitUntil(
      clients.openWindow(event.notification.data?.url || '/')
    );
  }
});

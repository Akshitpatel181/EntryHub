self.addEventListener('install', function () { self.skipWaiting(); });
self.addEventListener('activate', function (e) { e.waitUntil(self.clients.claim()); });

// Push payload: { title, body, url, image }   (image = the visitor's photo)
self.addEventListener('push', function (e) {
  var d = { title: 'Visitor arrived', body: '', url: '/HostPortal', image: null };
  try { d = Object.assign(d, e.data.json()); } catch (_) {}
  var opts = {
    body: d.body,
    icon: d.image || '/icons/icon-192.png',   // small picture: the visitor
    badge: '/icons/icon-192.png',
    data: { url: d.url },
    requireInteraction: true,
    tag: 'visitor-' + Date.now()
  };
  if (d.image) opts.image = d.image;          // large picture (Android / Windows / Chrome)
  e.waitUntil(self.registration.showNotification(d.title, opts));
});

self.addEventListener('notificationclick', function (e) {
  e.notification.close();
  var url = (e.notification.data && e.notification.data.url) || '/HostPortal';
  e.waitUntil(clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (list) {
    for (var c of list) { if ('focus' in c) return c.focus(); }
    return clients.openWindow(url);
  }));
});

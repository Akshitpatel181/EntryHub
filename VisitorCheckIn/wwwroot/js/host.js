(function () {
  var H = window.HOSTPORTAL || {}, status = document.getElementById('pushStatus'), btn = document.getElementById('enable');

  function u8(b64) {
    var p = '='.repeat((4 - b64.length % 4) % 4), s = (b64 + p).replace(/-/g, '+').replace(/_/g, '/'), raw = atob(s);
    return Uint8Array.from(raw, function (c) { return c.charCodeAt(0); });
  }
  function sameKey(sub, key) {
    try {
      var cur = new Uint8Array(sub.options.applicationServerKey);
      if (cur.length !== key.length) return false;
      for (var i = 0; i < cur.length; i++) if (cur[i] !== key[i]) return false;
      return true;
    } catch (e) { return false; }
  }
  function label() {
    var ua = navigator.userAgent, os = /iPhone|iPad/.test(ua) ? 'iOS' : /Android/.test(ua) ? 'Android' : /Windows/.test(ua) ? 'Windows' : /Mac/.test(ua) ? 'Mac' : /Linux/.test(ua) ? 'Linux' : 'Device';
    var br = /Edg\//.test(ua) ? 'Edge' : /Firefox/.test(ua) ? 'Firefox' : /Chrome/.test(ua) ? 'Chrome' : /Safari/.test(ua) ? 'Safari' : 'Browser';
    return os + ' \u00b7 ' + br;
  }
  function say(t) { if (status) status.textContent = t; }

  // Short chime when a visitor arrives (browsers may block sound until the page was clicked once)
  function chime() {
    try {
      var C = window.AudioContext || window.webkitAudioContext; if (!C) return;
      var a = new C(), o = a.createOscillator(), g = a.createGain();
      o.type = 'sine'; o.frequency.value = 880; g.gain.value = 0.08; o.connect(g); g.connect(a.destination); o.start();
      setTimeout(function () { o.stop(); a.close(); }, 220);
    } catch (e) { }
  }
  function toast(msg, photo) {
    var t = document.getElementById('toast'); if (!t) return;
    t.textContent = '';
    if (photo) { var i = document.createElement('img'); i.src = photo; i.alt = ''; t.appendChild(i); }
    var s = document.createElement('span'); s.textContent = msg; t.appendChild(s);
    t.style.display = 'flex'; chime();
    setTimeout(function () { t.style.display = 'none'; }, 9000);
  }

  // Tell the host, in plain words, what state this device is in
  async function refresh() {
    if (!status) return;
    if (!window.isSecureContext) { say('Not secure: open this site with its https address, otherwise this browser will not allow notifications.'); return; }
    if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) {
      say('This browser cannot receive push notifications. On iPhone/iPad add this site to the Home Screen and open it from there.'); return;
    }
    if (Notification.permission === 'denied') { say('Notifications are blocked for this site. Allow them in the browser settings, then reload this page.'); return; }
    try {
      var reg = await navigator.serviceWorker.getRegistration('/');
      var sub = reg ? await reg.pushManager.getSubscription() : null;
      say(sub ? '\u2713 This device is registered and will receive alerts.' : 'This device is not registered yet. Click \u201cEnable notifications on this device\u201d.');
    } catch (e) { }
  }

  async function enable() {
    if (!window.isSecureContext) { say('Not secure: open this site with its https address first.'); return; }
    if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) {
      say('This browser cannot receive push notifications. On iPhone/iPad add this site to the Home Screen and open it from there.'); return;
    }
    if (!H.vapidPublic) { say('Push is not ready on the server. Ask the admin to restart the app.'); return; }
    try {
      var reg = await navigator.serviceWorker.register('/sw.js'); await navigator.serviceWorker.ready;
      var perm = await Notification.requestPermission();
      if (perm !== 'granted') { say('Notifications were blocked. Allow them in your browser settings and try again.'); return; }
      var key = u8(H.vapidPublic), sub = await reg.pushManager.getSubscription();
      if (sub && !sameKey(sub, key)) { await sub.unsubscribe(); sub = null; }   // old subscription made with a different key
      if (!sub) sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key });
      var j = sub.toJSON();
      await postJson('/HostPortal/Subscribe', { endpoint: j.endpoint, p256dh: j.keys.p256dh, auth: j.keys.auth, label: label() });
      location.reload();
    } catch (e) { say('Could not enable: ' + e.message); }
  }
  if (btn) btn.addEventListener('click', enable);
  refresh();

  // Live pop-up (with photo) while this page is open
  if (window.signalR) {
    var conn = new signalR.HubConnectionBuilder().withUrl('/hostHub').withAutomaticReconnect().build();
    conn.on('VisitorArrived', function (title, body, photo) { toast(body, photo); setTimeout(function () { location.reload(); }, 7000); });
    conn.start().catch(function (e) { console.warn('SignalR', e); });
  }
})();

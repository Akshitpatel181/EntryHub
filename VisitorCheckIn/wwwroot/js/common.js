// POST JSON with the anti-forgery token. Returns parsed JSON ({} when empty).
async function postJson(url, data) {
  var tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
  var r = await fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'RequestVerificationToken': tokenEl ? tokenEl.value : '',
      'X-Requested-With': 'XMLHttpRequest'
    },
    body: JSON.stringify(data)
  });
  if (r.status === 401) { location.href = '/Kiosk/Pair'; return null; }
  if (!r.ok) throw new Error((await r.text()) || 'Request failed');
  try { return await r.json(); } catch (e) { return {}; }
}

// Show UTC timestamps in the viewer's local time zone.
document.querySelectorAll('time.lt').forEach(function (t) {
  var d = new Date(t.getAttribute('datetime'));
  if (!isNaN(d)) t.textContent = d.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
});

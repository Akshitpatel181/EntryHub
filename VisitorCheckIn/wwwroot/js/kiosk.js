(function () {
  var cfg = window.KIOSK, app = document.getElementById('app');
  var IDLE_MS = 90000, idleTimer = null;
  var state = {};

  function el(tag, attrs) {
    var e = document.createElement(tag); attrs = attrs || {};
    Object.keys(attrs).forEach(function (k) {
      var v = attrs[k];
      if (k === 'class') e.className = v;
      else if (k.indexOf('on') === 0) e.addEventListener(k.slice(2), v);
      else e.setAttribute(k, v);
    });
    for (var i = 2; i < arguments.length; i++) {
      [].concat(arguments[i]).forEach(function (c) {
        if (c === null || c === undefined || c === false) return;
        e.append(c.nodeType ? c : document.createTextNode(c));
      });
    }
    return e;
  }

  function initials(n) {
    var p = (n || '').trim().split(/\s+/).filter(Boolean);
    if (!p.length) return '?';
    return (p[0][0] + (p.length > 1 ? p[p.length - 1][0] : '')).toUpperCase();
  }

  function resetIdle() {
    clearTimeout(idleTimer);
    idleTimer = setTimeout(function () { if (state.step !== 'welcome') welcome(); }, IDLE_MS);
  }
  ['pointerdown', 'keydown'].forEach(function (ev) { document.addEventListener(ev, resetIdle, true); });

  // Progress: type -> details -> questions (only when some apply) -> host
  function hasQuestions() { return cfg.questions.some(function (q) { return q.visitorType === 'All' || q.visitorType === state.type; }); }
  function progress(step) {
    var order = ['type', 'details'].concat(hasQuestions() ? ['questions'] : [], ['photo', 'host']);
    var idx = order.indexOf(step);
    if (idx < 0) return null;
    var bar = el('div', { class: 'k-progress', role: 'progressbar', 'aria-valuemin': 1, 'aria-valuemax': order.length, 'aria-valuenow': idx + 1 });
    order.forEach(function (_, i) { bar.append(el('span', { class: i <= idx ? 'on' : '' })); });
    return bar;
  }

  function screen(step, title, sub, body, opts) {
    opts = opts || {}; state.step = step;
    stopCam();
    app.innerHTML = '';
    var err = el('div', { class: 'k-error', id: 'err', role: 'alert' });
    var nav = el('div', { class: 'k-nav' },
      opts.back ? el('button', { class: 'k-btn k-ghost', onclick: opts.back }, '\u2190 Back') : el('span'),
      opts.next ? el('button', { class: 'k-btn k-primary', onclick: opts.next }, opts.nextLabel || 'Continue') : null);
    app.append(el('div', { class: 'k-card' }, el('div', { class: 'k-card-brand' }, icon(ICON_BADGE), el('span', {}, cfg.company)), progress(step), el('h1', {}, title), sub ? el('p', { class: 'k-sub' }, sub) : null, body, err, nav));
    resetIdle();
  }
  function showError(m) { var e = document.getElementById('err'); if (e) e.textContent = m; }

  // ---- 0. Welcome (new home layout) ----
  function greeting() {
    var h = new Date().getHours();
    return h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening';
  }
  function icon(inner, cls) {
    var s = document.createElement('span'); s.className = cls || 'k-ico';
    s.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + inner + '</svg>';
    return s;
  }
  var ICON_BADGE = '<rect x="4" y="2.5" width="16" height="19" rx="3"/><path d="M9.5 6h5"/><circle cx="12" cy="11.5" r="2.6"/><path d="M7.5 18.2c.8-2.2 2.4-3.2 4.5-3.2s3.7 1 4.5 3.2"/>';
  var ICON_IN = '<path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4"/><polyline points="10 17 15 12 10 7"/><line x1="15" y1="12" x2="3" y2="12"/>';
  var ICON_OUT = '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/>';
  var ICON_CHEV = '<polyline points="9 6 15 12 9 18"/>';

  var clockTimer = null;
  function tickClock() {
    var c = document.getElementById('k-clock'), d = document.getElementById('k-date');
    if (!c) { clearInterval(clockTimer); clockTimer = null; return; }
    var now = new Date();
    c.textContent = now.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
    d.textContent = now.toLocaleDateString([], { weekday: 'long', day: 'numeric', month: 'long' });
  }
  function actionButton(kind, title, sub, iconPath, onclick) {
    return el('button', { class: 'k-action k-action-' + kind, type: 'button', onclick: onclick },
      icon(iconPath),
      el('span', { class: 'k-action-text' }, el('b', {}, title), el('small', {}, sub)),
      icon(ICON_CHEV, 'k-chev'));
  }
  function welcome() {
    state = { answers: {} }; state.step = 'welcome';
    stopCam();
    app.innerHTML = '';
    app.append(el('div', { class: 'k-home' },
      el('section', { class: 'k-home-left' },
        el('div', { class: 'k-home-brand' }, icon(ICON_BADGE), el('span', {}, cfg.company)),
        el('div', {},
          el('p', { class: 'k-home-greet' }, 'Visitor check-in'),
          el('h1', { class: 'k-home-title' }, greeting()),
          el('p', { class: 'k-home-sub' }, cfg.welcome)),
        el('div', { class: 'k-home-time' },
          el('div', { class: 'k-clock', id: 'k-clock' }), el('div', { class: 'k-date', id: 'k-date' }))),
      el('section', { class: 'k-home-right' },
        actionButton('in', 'Check in', 'I\u2019m here for a visit', ICON_IN, chooseType),
        actionButton('out', 'Check out', 'I\u2019m leaving today', ICON_OUT, checkOut),
        el('p', { class: 'k-home-help' }, 'Need help? Please ask at reception.'))));
    tickClock(); if (!clockTimer) clockTimer = setInterval(tickClock, 15000);
    resetIdle();
  }

  // ---- 1. Visitor type ----
  function chooseType() {
    var grid = el('div', { class: 'k-grid' }, cfg.visitorTypes.map(function (t) {
      return el('div', { class: 'k-tile', onclick: function () { state.type = t; details(); } }, t);
    }));
    screen('type', 'What brings you here?', 'Please choose one.', grid, { back: welcome });
  }

  // ---- 2. Name ----
  function details() {
    var name = el('input', { class: 'k-input', placeholder: 'Full name', autocomplete: 'off', value: state.name || '' });
    var company = el('input', { class: 'k-input', placeholder: 'Company (optional)', autocomplete: 'off', value: state.company || '' });
    var body = el('div', {}, el('label', { class: 'k-label' }, 'Your name'), name, el('label', { class: 'k-label' }, 'Company'), company);
    screen('details', 'Tell us who you are', null, body, {
      back: chooseType,
      next: function () {
        if (!name.value.trim()) return showError('Please enter your name.');
        state.name = name.value.trim(); state.company = company.value.trim();
        questions();
      }
    });
    setTimeout(function () { name.focus(); }, 100);
  }

  // ---- 3. Questions ----
  function signaturePad() {
    var c = el('canvas', { class: 'k-sig', width: 800, height: 240 }), ctx = c.getContext('2d'), drawing = false, drawn = false;
    ctx.lineWidth = 3; ctx.lineCap = 'round'; ctx.strokeStyle = '#111';
    function pos(e) { var r = c.getBoundingClientRect(); return { x: (e.clientX - r.left) * c.width / r.width, y: (e.clientY - r.top) * c.height / r.height }; }
    c.addEventListener('pointerdown', function (e) { drawing = drawn = true; var p = pos(e); ctx.beginPath(); ctx.moveTo(p.x, p.y); c.setPointerCapture(e.pointerId); });
    c.addEventListener('pointermove', function (e) { if (!drawing) return; var p = pos(e); ctx.lineTo(p.x, p.y); ctx.stroke(); });
    ['pointerup', 'pointercancel'].forEach(function (t) { c.addEventListener(t, function () { drawing = false; }); });
    var clear = el('button', { class: 'k-btn k-ghost k-small', type: 'button', onclick: function () { ctx.clearRect(0, 0, c.width, c.height); drawn = false; } }, 'Clear');
    return { node: el('div', {}, c, clear), get: function () { return drawn ? c.toDataURL('image/png') : ''; } };
  }

  function buildInput(q) {
    var prev = state.answers[q.id] || '', t = q.type;
    if (t === 'YesNo') {
      var val = prev, tiles = ['Yes', 'No'].map(function (v) {
        var d = el('div', { class: 'k-tile' + (val === v ? ' sel' : ''), onclick: function () { val = v; tiles.forEach(function (x) { x.classList.toggle('sel', x === d); }); } }, v);
        return d;
      });
      return { node: el('div', { class: 'k-yn' }, tiles), get: function () { return val; } };
    }
    if (t === 'Dropdown') {
      var sel = el('select', { class: 'k-select' }, el('option', { value: '' }, 'Select…'),
        q.options.map(function (o) { var op = el('option', { value: o }, o); if (o === prev) op.selected = true; return op; }));
      return { node: sel, get: function () { return sel.value; } };
    }
    if (t === 'Checkbox') {
      var boxes = q.options.map(function (o) { var cb = el('input', { type: 'checkbox', value: o }); return cb; });
      var chosen = prev ? prev.split(', ') : []; boxes.forEach(function (b) { b.checked = chosen.indexOf(b.value) >= 0; });
      return { node: el('div', {}, boxes.map(function (b) { return el('label', { class: 'k-check' }, b, b.value); })),
               get: function () { return boxes.filter(function (b) { return b.checked; }).map(function (b) { return b.value; }).join(', '); } };
    }
    if (t === 'Signature') return signaturePad();
    var type = t === 'Phone' ? 'tel' : t === 'Email' ? 'email' : 'text';
    var inp = el('input', { class: 'k-input', type: type, autocomplete: 'off', value: prev });
    return { node: inp, get: function () { return inp.value.trim(); } };
  }

  function questions() {
    var qs = cfg.questions.filter(function (q) { return q.visitorType === 'All' || q.visitorType === state.type; });
    if (!qs.length) return photo();
    var inputs = qs.map(function (q) { return { q: q, inp: buildInput(q) }; });
    var body = el('div', {}, inputs.map(function (x) {
      return el('div', {}, el('label', { class: 'k-label' }, x.q.text + (x.q.isRequired ? ' *' : '')), x.inp.node);
    }));
    screen('questions', 'A few quick questions', null, body, {
      back: details,
      next: function () {
        for (var i = 0; i < inputs.length; i++) {
          var v = inputs[i].inp.get();
          if (inputs[i].q.isRequired && !v) return showError('Please answer: ' + inputs[i].q.text);
          state.answers[inputs[i].q.id] = v;
        }
        photo();
      }
    });
  }


  // ---- 3b. Photo (mandatory) ----
  var camStream = null;
  function stopCam() {
    if (camStream) { camStream.getTracks().forEach(function (t) { t.stop(); }); camStream = null; }
  }

  // Shrink any image source to max 720px and return a JPEG data URL
  function toJpeg(source, w, h) {
    var scale = Math.min(1, 720 / Math.max(w, h)), c = document.createElement('canvas');
    c.width = Math.round(w * scale); c.height = Math.round(h * scale);
    c.getContext('2d').drawImage(source, 0, 0, c.width, c.height);
    return c.toDataURL('image/jpeg', 0.82);
  }

  function camMessage(e) {
    var n = e && e.name;
    if (n === 'NotAllowedError' || n === 'PermissionDeniedError' || n === 'SecurityError')
      return 'Camera access is blocked. Tap the lock icon in the address bar, set Camera to Allow, reload, then tap Try camera again.';
    if (n === 'NotReadableError' || n === 'TrackStartError')
      return 'The camera is being used by another app. Close it, then tap Try camera again.';
    if (n === 'NotFoundError' || n === 'DevicesNotFoundError')
      return 'No camera was found. Tap \u201cUse camera app\u201d or ask reception for help.';
    return 'The camera could not start. Tap Try camera again, or use the camera app.';
  }

  function photo() {
    var required = cfg.requirePhoto !== false, nextBtn = null, counting = false;
    var video = el('video', { class: 'k-video', autoplay: '', playsinline: '' });
    video.muted = true;
    var shot = el('img', { class: 'k-shot', alt: 'Your photo' });
    var count = el('div', { class: 'k-count' });
    var stage = el('div', { class: 'k-stage' }, video, shot, count);
    var msg = el('p', { class: 'k-sub k-cam-msg' }, '');
    var controls = el('div', { class: 'k-cam-controls' });
    var fileIn = el('input', { type: 'file', accept: 'image/*', capture: 'user', class: 'k-hidden' });

    function setNext() { if (nextBtn) nextBtn.disabled = required && !state.photo; }
    function setControls(mode) {
      controls.innerHTML = '';
      stage.className = 'k-stage ' + mode;
      if (mode === 'live') {
        msg.textContent = 'Look at the camera, then tap Take photo.';
        controls.append(el('button', { class: 'k-btn k-primary k-snap', onclick: countdown }, 'Take photo'));
      } else if (mode === 'review') {
        msg.textContent = 'Looking good? Tap Continue, or retake it.';
        controls.append(el('button', { class: 'k-btn k-ghost', onclick: retake }, 'Retake'));
      } else {
        controls.append(el('button', { class: 'k-btn k-primary', onclick: startCam }, 'Try camera again'),
                        el('button', { class: 'k-btn k-ghost', onclick: function () { fileIn.click(); } }, 'Use camera app'));
      }
      if (!required && mode !== 'review') controls.append(el('button', { class: 'k-btn k-ghost k-small', onclick: pickHost }, 'Skip photo'));
      setNext();
    }
    function fail(text) { stopCam(); msg.textContent = text; setControls('error'); }

    async function startCam() {
      stopCam(); showError('');
      if (!window.isSecureContext || !navigator.mediaDevices || !navigator.mediaDevices.getUserMedia)
        return fail('The camera needs a secure https address. Tap \u201cUse camera app\u201d.');
      msg.textContent = 'Starting camera\u2026 tap Allow if asked.'; stage.className = 'k-stage wait';
      try {
        camStream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user', width: { ideal: 1280 }, height: { ideal: 960 } }, audio: false });
        video.srcObject = camStream; await video.play();
        setControls('live');
      } catch (e) {
        fail(camMessage(e));
      }
    }

    function countdown() {
      if (counting) return; counting = true;
      var n = 3, btn = controls.querySelector('.k-snap'); if (btn) btn.disabled = true;
      (function tick() {
        if (n > 0) { count.textContent = n; count.classList.remove('pop'); void count.offsetWidth; count.classList.add('pop'); n--; setTimeout(tick, 800); }
        else { count.textContent = ''; counting = false; snap(); }
      })();
    }

    function snap() {
      if (!video.videoWidth) return;
      state.photo = toJpeg(video, video.videoWidth, video.videoHeight);
      shot.src = state.photo; stopCam(); setControls('review');
    }

    function retake() { state.photo = ''; shot.removeAttribute('src'); startCam(); }

    fileIn.addEventListener('change', function () {
      var f = fileIn.files && fileIn.files[0]; if (!f) return;
      var url = URL.createObjectURL(f), img = new Image();
      img.onload = function () {
        state.photo = toJpeg(img, img.naturalWidth, img.naturalHeight); URL.revokeObjectURL(url);
        shot.src = state.photo; setControls('review');
      };
      img.onerror = function () { URL.revokeObjectURL(url); showError('That picture could not be read. Please try again.'); };
      img.src = url; fileIn.value = '';
    });

    var body = el('div', { class: 'k-center' }, stage, msg, controls, fileIn);
    screen('photo', 'Smile, please', 'We take a photo so your host knows who has arrived.', body, {
      back: function () { (hasQuestions() ? questions : details)(); },
      next: function () {
        if (required && !state.photo) return showError('Please take your photo to continue.');
        pickHost();
      }
    });
    nextBtn = app.querySelector('.k-nav .k-primary');
    if (state.photo) { shot.src = state.photo; setControls('review'); } else startCam();
  }

  // ---- 4. Host ----
  function pickHost() {
    var list = el('div', { class: 'k-grid' });
    var search = el('input', { class: 'k-input', placeholder: 'Search for who you are visiting…', autocomplete: 'off' });
    function render() {
      var f = search.value.trim().toLowerCase(); list.innerHTML = '';
      cfg.hosts.filter(function (h) { return !f || h.name.toLowerCase().indexOf(f) >= 0; }).forEach(function (h) {
        list.append(el('div', { class: 'k-tile k-host', onclick: function () { submit(h); } }, el('span', { class: 'k-avatar' }, initials(h.name)), h.name));
      });
      if (!list.children.length) list.append(el('p', { class: 'k-sub' }, 'No match.'));
    }
    search.addEventListener('input', render); render();
    screen('host', 'Who are you here to see?', null, el('div', {}, search, el('div', { style: 'height:16px' }), list),
      { back: photo });
  }

  async function submit(host) {
    var payload = {
      name: state.name, company: state.company, visitorType: state.type, hostId: host.id, photo: state.photo || '',
      answers: Object.keys(state.answers).map(function (id) { return { questionId: parseInt(id), value: state.answers[id] }; })
    };
    try {
      var r = await postJson('/Kiosk/CheckIn', payload);
      if (r) done(host.name);
    } catch (e) { showError(e.message || 'Something went wrong. Please try again.'); }
  }

  // ---- 5. Done ----
  function done(hostName) {
    var when = new Date().toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
    var badge = el('div', { class: 'k-badge' },
      el('div', { class: 'k-badge-slot' }),
      el('div', { class: 'k-badge-type' }, state.type || 'Visitor'),
      el('div', { class: 'k-badge-avatar' }, initials(state.name)),
      el('div', { class: 'k-badge-name' }, state.name),
      state.company ? el('div', { class: 'k-badge-co' }, state.company) : null,
      el('div', { class: 'k-badge-meta' }, el('span', {}, 'Visiting'), el('b', {}, hostName)),
      el('div', { class: 'k-badge-meta' }, el('span', {}, 'Arrived'), el('b', {}, when)));
    screen('done', 'You\u2019re checked in', null, el('div', { class: 'k-done' },
      badge, el('p', { class: 'k-sub' }, hostName + ' has been notified and will be with you shortly.')));
    setTimeout(welcome, 8000);
  }

  // ---- Check-out ----
  function checkOut() {
    var results = el('div', {}), search = el('input', { class: 'k-input', placeholder: 'Type your name…', autocomplete: 'off' });
    var t = null;
    search.addEventListener('input', function () {
      clearTimeout(t);
      t = setTimeout(async function () {
        results.innerHTML = '';
        if (search.value.trim().length < 2) return;
        var r = await fetch('/Kiosk/OnSite?q=' + encodeURIComponent(search.value.trim()), { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (r.status === 401) { location.href = '/Kiosk/Pair'; return; }
        var rows = await r.json();
        if (!rows.length) results.append(el('p', { class: 'k-sub' }, 'No one found. Check the spelling.'));
        rows.forEach(function (v) {
          results.append(el('div', { class: 'k-tile', style: 'margin-top:10px', onclick: async function () {
            await postJson('/Kiosk/CheckOut', { id: v.id });
            screen('out', 'Goodbye, ' + v.name + '!', 'You have been checked out.', el('div', { class: 'k-done' }, el('div', { class: 'tick' }, '✓')));
            setTimeout(welcome, 4000);
          } }, v.name + (v.company ? ' · ' + v.company : '') + '  (visiting ' + v.host + ')'));
        });
      }, 250);
    });
    screen('checkout', 'Check out', 'Start typing your name.', el('div', {}, search, results), { back: welcome });
    setTimeout(function () { search.focus(); }, 100);
  }

  // Heartbeat so admins see the kiosk as "Online"
  setInterval(function () { fetch('/Kiosk/Ping', { headers: { 'X-Requested-With': 'XMLHttpRequest' } }).catch(function () {}); }, 60000);
  welcome();
})();

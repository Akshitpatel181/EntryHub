#!/usr/bin/env python3
"""
End-to-end smoke test for Visitor Check-In. Uses only the Python standard library.

1. Start the site:      dotnet run
2. In another window:   python tests/smoke_test.py
   Options:             --base https://localhost:7080  --email admin@example.com  --password ChangeMe123  --no-cleanup

It walks the whole product: anonymous protection, admin login, add host, add question,
all 5 kiosk pairing methods, check-in, host notification path, check-out and revoke.
"""
import argparse, html as htmllib, http.cookiejar, json, re, ssl, sys, time
import urllib.error, urllib.parse, urllib.request

results = []

# Tiny valid JPEG used as the visitor photo (the kiosk now requires one)
PHOTO = "data:image/jpeg;base64,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"

def section(t): print(f"\n== {t}")

def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("  PASS  " if ok else "  FAIL  ") + name + (f"\n          -> {detail}" if (detail and not ok) else ""))
    return bool(ok)

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k): return None   # we want to SEE the 302s

class Client:
    def __init__(self, base):
        self.base = base.rstrip("/")
        self.jar = http.cookiejar.CookieJar()
        ctx = ssl.create_default_context(); ctx.check_hostname = False; ctx.verify_mode = ssl.CERT_NONE
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.jar), urllib.request.HTTPSHandler(context=ctx), NoRedirect())

    def request(self, method, path, form=None, js=None, headers=None):
        url = path if path.startswith("http") else self.base + path
        data, h = None, dict(headers or {})
        if form is not None:
            data = urllib.parse.urlencode(form).encode(); h["Content-Type"] = "application/x-www-form-urlencoded"
        if js is not None:
            data = json.dumps(js).encode(); h["Content-Type"] = "application/json"
        req = urllib.request.Request(url, data=data, method=method, headers=h)
        try:
            r = self.opener.open(req, timeout=30)
            return r.status, r.read().decode("utf-8", "replace"), r.headers.get("Location")
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode("utf-8", "replace"), e.headers.get("Location")

    def get(self, path, headers=None): return self.request("GET", path, headers=headers)

def token_from(page):
    m = re.search(r'<input[^>]*name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
    return m.group(1) if m else ""

def fresh_token(c, path):
    return token_from(c.get(path)[1])

def post_form(c, path, form, token_page):
    f = dict(form); f["__RequestVerificationToken"] = fresh_token(c, token_page)
    return c.request("POST", path, form=f)

def post_json(c, path, payload, token_page):
    return c.request("POST", path, js=payload, headers={
        "RequestVerificationToken": fresh_token(c, token_page), "X-Requested-With": "XMLHttpRequest"})

def login(c, email, pw):
    return post_form(c, "/Account/Login", {"Email": email, "Password": pw, "Remember": "false", "returnUrl": ""}, "/Account/Login")

def row_with(page, needle):
    for chunk in page.split("<tr"):
        if needle in chunk: return chunk
    return ""

def new_enrollment(admin, name, max_uses=1, minutes=15):
    s, body, _ = post_form(admin, "/Devices/Enroll", {"deviceName": name, "maxUses": max_uses, "minutes": minutes}, "/Devices/Add")
    code = re.search(r'class="code-box">(\d{6})<', body)
    link = re.search(r'<textarea[^>]*id="lnk"[^>]*>(.*?)</textarea>', body, re.S)
    link = htmllib.unescape(link.group(1)).strip() if link else ""
    parts = urllib.parse.urlsplit(link)
    path = parts.path + ("?" + parts.query if parts.query else "")
    has_qr = "data:image/png;base64," in body
    return s, (code.group(1) if code else ""), path, has_qr

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="https://localhost:7080")
    ap.add_argument("--email", default="admin@example.com")
    ap.add_argument("--password", default="ChangeMe123")
    ap.add_argument("--no-cleanup", action="store_true")
    a = ap.parse_args()
    stamp = str(int(time.time()))
    host_email, host_pw = f"smoke.host.{stamp}@example.com", "Smoke12345"
    q_text = f"Smoke question {stamp}"
    visitor = f"Smoke Visitor {stamp}"

    anon, admin, host = Client(a.base), Client(a.base), Client(a.base)

    section("1. Site is up and public pages work")
    try:
        s, _, _ = anon.get("/")
    except Exception as e:
        print(f"\nCannot reach {a.base}: {e}\nIs `dotnet run` running? Check the URL/port.\n"); sys.exit(2)
    check("Home page loads", s == 200, s)
    check("Login page loads", anon.get("/Account/Login")[0] == 200)
    check("Kiosk pairing page loads", anon.get("/Kiosk/Pair")[0] == 200)
    check("Service worker /sw.js served", anon.get("/sw.js")[0] == 200)
    check("kiosk.webmanifest served", anon.get("/kiosk.webmanifest")[0] == 200)
    check("host.webmanifest served", anon.get("/host.webmanifest")[0] == 200)
    check("App icon served", anon.get("/icons/icon-192.png")[0] == 200)

    section("2. Security: protected areas reject strangers")
    s, _, loc = anon.get("/Dashboard");  check("Anonymous /Dashboard -> login", s == 302 and "Login" in (loc or ""), (s, loc))
    s, _, loc = anon.get("/Hosts");      check("Anonymous /Hosts -> login", s == 302 and "Login" in (loc or ""), (s, loc))
    s, _, loc = anon.get("/Kiosk");      check("Unpaired /Kiosk -> pairing screen", s == 302 and "Pair" in (loc or ""), (s, loc))
    s, _, _ = anon.request("POST", "/Kiosk/CheckIn", js={}, headers={"X-Requested-With": "XMLHttpRequest"})
    check("Unpaired kiosk check-in rejected", s in (400, 401), s)
    s, _, _ = post_form(anon, "/Kiosk/Pair", {"code": "000000"}, "/Kiosk/Pair")
    check("Wrong pairing code rejected (stays on pair page)", s == 200)

    section("3. Admin login + portal")
    s, _, loc = login(admin, a.email, a.password)
    if not check("Admin login succeeds", s == 302, f"status {s} (wrong email/password in appsettings.json?)"): sys.exit(1)
    for path, word in [("/Dashboard", "Dashboard"), ("/Hosts", "Hosts"), ("/Questions", "questions"),
                       ("/Devices", "Kiosk devices"), ("/Visits", "Visitor log"), ("/Devices/Guide", "kiosk mode")]:
        s, b, _ = admin.get(path); check(f"Admin page {path} opens", s == 200 and word.lower() in b.lower(), s)

    section("4. Admin adds a host (with login) and a question")
    s, _, _ = post_form(admin, "/Hosts/Create", {"Name": f"Smoke Host {stamp}", "Email": host_email, "Phone": "0400000000",
        "IsActive": "true", "CreateLogin": "true", "Password": host_pw}, "/Hosts/Create")
    check("Create host", s == 302, s)
    hosts_page = admin.get("/Hosts")[1]
    check("Host appears in list", host_email in hosts_page)
    host_id = (re.search(r"/Hosts/Edit/(\d+)", row_with(hosts_page, host_email)) or [None, None])[1]

    s, _, _ = post_form(admin, "/Questions/Create", {"Text": q_text, "Type": "Text", "VisitorType": "All",
        "IsRequired": "true", "IsActive": "true"}, "/Questions/Create")
    check("Create question", s == 302, s)
    check("Question appears in list", q_text in admin.get("/Questions")[1])

    section("5. Kiosk pairing - all five methods")
    # A: code
    s, code, link_path, qr = new_enrollment(admin, "Smoke-Code")
    check("Method A/B/C: enrolment page gives code + QR + link", s == 200 and len(code) == 6 and qr and "token=" in link_path, (s, code, link_path))
    kiosk = Client(a.base)
    s, _, loc = post_form(kiosk, "/Kiosk/Pair", {"code": code}, "/Kiosk/Pair")
    check("Method A: pair with 6-digit code", s == 302 and "/Kiosk" in (loc or ""), (s, loc))
    s, kpage, _ = kiosk.get("/Kiosk")
    check("Paired device opens kiosk screen", s == 200 and "window.KIOSK" in kpage, s)
    s, b, _ = post_form(Client(a.base), "/Kiosk/Pair", {"code": code}, "/Kiosk/Pair")
    check("Code is single-use (2nd device refused)", s == 200 and "Invalid" in b, s)

    # C/B: link (QR encodes this link)
    s, code2, link_path2, _ = new_enrollment(admin, "Smoke-Link")
    k2 = Client(a.base)
    s, b, _ = k2.get(link_path2)
    check("Method B/C: link shows confirm page (not auto-used)", s == 200 and "Set up this device" in b, s)
    tok = token_from(b); m = re.search(r'name="Token" value="([^"]+)"', b)
    s, _, loc = k2.request("POST", "/Kiosk/Enroll", form={"Token": m.group(1) if m else "", "DeviceName": "Smoke-Link", "__RequestVerificationToken": tok})
    check("Method B/C: confirm pairs the device", s == 302, s)
    check("Link-paired device opens kiosk", k2.get("/Kiosk")[0] == 200)
    s, b, _ = Client(a.base).get(link_path2)
    check("Link is single-use", "invalid" in b.lower() or "expired" in b.lower() or "already used" in b.lower(), s)

    # D: this device
    s, _, loc = post_form(admin, "/Devices/ThisDevice", {"deviceName": "Smoke-ThisDevice"}, "/Devices/Add")
    check("Method D: 'use this device' pairs instantly", s == 302 and "/Kiosk" in (loc or ""), (s, loc))
    check("Method D: that browser can open kiosk", admin.get("/Kiosk")[0] == 200)

    # E: bulk
    s, _, bulk_path, _ = new_enrollment(admin, "Smoke-Bulk", max_uses=2, minutes=60)
    ok_count = 0
    for i in range(3):
        c = Client(a.base); s, b, _ = c.get(bulk_path)
        if "Set up this device" not in b: continue
        m = re.search(r'name="Token" value="([^"]+)"', b)
        s, _, _ = c.request("POST", "/Kiosk/Enroll", form={"Token": m.group(1), "DeviceName": "", "__RequestVerificationToken": token_from(b)})
        if s == 302 and c.get("/Kiosk")[0] == 200: ok_count += 1
    check("Method E: bulk link enrols exactly 2 devices, 3rd refused", ok_count == 2, f"{ok_count} enrolled")

    devs = admin.get("/Devices")[1]
    check("All smoke devices listed in admin", all(n in devs for n in ("Smoke-Code", "Smoke-Link", "Smoke-ThisDevice")))

    section("6. Host portal + notification wiring")
    s, _, _ = login(host, host_email, host_pw)
    check("Host can log in", s == 302, s)
    s, b, _ = host.get("/HostPortal")
    check("Host portal opens", s == 200 and "Enable notifications" in b, s)
    s, _, loc = host.get("/Dashboard")
    check("Host is blocked from admin area", s == 302 and "Denied" in (loc or ""), (s, loc))
    s, r, _ = post_json(host, "/HostPortal/Subscribe", {"endpoint": "https://push.example.invalid/smoke", "p256dh": "x", "auth": "y",
                                                         "label": "Smoke test device"}, "/HostPortal")
    check("Host registers a notification device", s == 200, (s, r))
    check("Device shows in host portal", "Smoke test device" in host.get("/HostPortal")[1])
    s, r, _ = host.request("POST", "/hostHub/negotiate?negotiateVersion=1", headers={"X-Requested-With": "XMLHttpRequest"})
    check("SignalR hub accepts host (live toast channel)", s == 200 and "connectionId" in r.lower(), s)
    s, _, _ = anon.request("POST", "/hostHub/negotiate?negotiateVersion=1", headers={"X-Requested-With": "XMLHttpRequest"})
    check("SignalR hub rejects anonymous", s in (401, 302, 403), s)
    if host_id:
        s, _, _ = post_form(admin, f"/Hosts/TestNotify/{host_id}", {}, "/Hosts")
        check("Admin 'Test ping' runs without error", s == 302, s)

    section("7. Visitor check-in on the kiosk")
    hosts_json = re.search(r"hosts:\s*(\[.*?\])\s*\n", kpage)
    q_json = re.search(r"questions:\s*(\[.*?\]),\s*\n", kpage)
    kh = json.loads(hosts_json.group(1)) if hosts_json else []
    kq = json.loads(q_json.group(1)) if q_json else []
    check("Kiosk received hosts + questions from admin", bool(kh) and any(q["text"] == q_text for q in kq), (len(kh), len(kq)))
    my_host = next((h for h in kh if "Smoke Host" in h["name"] and stamp in h["name"]), kh[0] if kh else None)
    answers = []
    for q in kq:
        if q["visitorType"] not in ("All", "Visitor"): continue
        v = {"YesNo": "Yes", "Phone": "0400000000", "Email": "a@b.co", "Signature": "data:image/png;base64,AAAA"}.get(q["type"])
        if v is None: v = (q["options"][0] if q["options"] else "ok") if q["type"] in ("Dropdown", "Checkbox") else "ok"
        answers.append({"questionId": q["id"], "value": v})
    s, r, _ = post_json(kiosk, "/Kiosk/CheckIn", {"name": visitor, "company": "Smoke Co", "visitorType": "Visitor",
                                                   "hostId": my_host["id"] if my_host else 0, "answers": answers, "photo": PHOTO}, "/Kiosk")
    check("Kiosk check-in accepted", s == 200 and '"ok":true' in r.replace(" ", ""), (s, r[:200]))
    s, r, _ = post_json(kiosk, "/Kiosk/CheckIn", {"name": "", "hostId": my_host["id"] if my_host else 0, "answers": []}, "/Kiosk")
    check("Check-in without a name is rejected", s == 400, s)
    s, r, _ = post_json(kiosk, "/Kiosk/CheckIn", {"name": "No Answers", "visitorType": "Visitor",
                                                   "hostId": my_host["id"] if my_host else 0, "answers": [], "photo": PHOTO}, "/Kiosk")
    check("Check-in missing required answers is rejected", s == 400, s)

    section("7b. Mandatory visitor photo")
    s, r, _ = post_json(kiosk, "/Kiosk/CheckIn", {"name": "No Photo", "visitorType": "Visitor", "hostId": my_host["id"] if my_host else 0, "answers": answers}, "/Kiosk")
    check("Check-in without a photo is rejected", s == 400 and "photo" in r.lower(), (s, r[:120]))
    s, r, _ = post_json(kiosk, "/Kiosk/CheckIn", {"name": "Bad Photo", "visitorType": "Visitor", "hostId": my_host["id"] if my_host else 0, "answers": answers, "photo": "data:image/jpeg;base64,AAAA"}, "/Kiosk")
    check("Check-in with a fake photo is rejected", s == 400, (s, r[:120]))
    s, r, _ = post_json(kiosk, "/Kiosk/CheckIn", {"name": "Not A Jpeg", "visitorType": "Visitor", "hostId": my_host["id"] if my_host else 0, "answers": answers, "photo": "data:image/png;base64," + PHOTO.split(",", 1)[1]}, "/Kiosk")
    check("Photo must be a JPEG", s == 400, (s, r[:120]))
    vis_page = admin.get(f"/Visits?q={urllib.parse.quote(visitor)}")[1]
    tok = re.search(r'src="/p/([0-9a-f]{48})"', vis_page)
    check("Visitor photo is shown in the admin log", tok is not None)
    if tok:
        s, _, _ = anon.get("/p/" + tok.group(1))
        check("Photo link works without login (needed by notifications and email)", s == 200, s)
    s, _, _ = anon.get("/p/" + "0" * 48)
    check("Unknown photo link returns 404", s == 404, s)
    check("Visit appears in admin log", visitor in admin.get(f"/Visits?q={urllib.parse.quote(visitor)}")[1])
    check("Visit appears as on-site on dashboard", visitor in admin.get("/Dashboard")[1])
    check("Host portal lists the visitor", visitor in host.get("/HostPortal")[1])

    section("8. Check-out")
    s, r, _ = kiosk.get("/Kiosk/OnSite?q=" + urllib.parse.quote("Smoke Visitor"), headers={"X-Requested-With": "XMLHttpRequest"})
    rows = json.loads(r) if s == 200 and r.startswith("[") else []
    mine = next((x for x in rows if x["name"] == visitor), None)
    check("Kiosk check-out search finds the visitor", mine is not None, (s, r[:200]))
    check("Check-out search needs 2+ letters (privacy)", kiosk.get("/Kiosk/OnSite?q=S", headers={"X-Requested-With": "XMLHttpRequest"})[1].strip() == "[]")
    if mine:
        s, r, _ = post_json(kiosk, "/Kiosk/CheckOut", {"id": mine["id"]}, "/Kiosk")
        check("Check-out succeeds", s == 200, (s, r))
        check("Visitor no longer on site", visitor not in admin.get("/Visits?onsite=true")[1])

    section("9. Revoke a device")
    row = row_with(admin.get("/Devices")[1], 'value="Smoke-Code"')
    rid = re.search(r"/Devices/Revoke/(\d+)", row)
    if rid:
        post_form(admin, f"/Devices/Revoke/{rid.group(1)}", {}, "/Devices")
        s, _, loc = kiosk.get("/Kiosk")
        check("Revoked device is sent back to pairing screen", s == 302 and "Pair" in (loc or ""), (s, loc))
    else:
        check("Found device row to revoke", False)

    if not a.no_cleanup:
        section("10. Cleanup")
        page = admin.get("/Devices")[1]
        for chunk in page.split("<tr"):
            if 'value="Smoke-' in chunk:
                m = re.search(r"/Devices/Remove/(\d+)", chunk)
                if m: post_form(admin, f"/Devices/Remove/{m.group(1)}", {}, "/Devices")
        for chunk in admin.get("/Questions")[1].split("<tr"):
            if q_text in chunk:
                m = re.search(r"/Questions/Delete/(\d+)", chunk)
                if m: post_form(admin, f"/Questions/Delete/{m.group(1)}", {}, "/Questions")
        if host_id: post_form(admin, f"/Hosts/Delete/{host_id}", {}, "/Hosts")
        print("  Removed smoke devices/question; test host deactivated (it has visit history).")

    passed = sum(1 for _, ok in results if ok)
    print(f"\n==== {passed}/{len(results)} checks passed ====")
    for n, ok in results:
        if not ok: print("  FAILED:", n)
    sys.exit(0 if passed == len(results) else 1)

if __name__ == "__main__":
    main()

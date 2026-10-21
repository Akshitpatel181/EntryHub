# Run it and check everything works

## 1. Start
**Visual Studio:** open `VisitorCheckIn.slnx` and press F5. **Windows:** double-click `RUN.bat`. **Mac/Linux:** `./run.sh` (or `dotnet run --project VisitorCheckIn`).

Needs the .NET 8 SDK or newer. Site: **https://localhost:7080** · Admin: `admin@example.com` / `ChangeMe123`.

* **SQL Server:** Windows uses LocalDB (comes with Visual Studio). On Mac/Linux run SQL in Docker and change `ConnectionStrings:Default`:
  `docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=Your_password123 -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest`
  → `Server=localhost,1433;Database=VisitorCheckIn;User Id=sa;Password=Your_password123;TrustServerCertificate=True`
* The database, roles, admin user and 4 sample questions are created on first start.

## 2. How the pieces work together

```
ADMIN (browser, login)                KIOSK (paired device)                 HOST (phone/laptop, login)
 Hosts ──────────────┐                                                       My notifications
 Questions ──────────┼──► SQL DB ◄── Kiosk loads hosts + questions            └ Enable notifications
 Devices ► Add ──────┘      ▲                                                   = saves a HostDevice row
   code / QR / link         │
   this device / bulk       │  1 visitor taps: type → name → questions → host
        │                   │  2 POST /Kiosk/CheckIn  ([KioskAuth] checks device cookie)
        ▼                   │  3 server saves Visit + answers
  one-time enrolment ──► device opens /Kiosk/Pair or link
  (code+link, expires)   → server sets HttpOnly cookie (hash stored)
                                        │
                                        ▼  NotificationService
                         ┌──────────────┼───────────────────┐
                         ▼              ▼                   ▼
                   SignalR toast   Web Push to EVERY    Email (if SMTP set)
                   (page open)     device of that host
```
Admin sees the visit live in **Visitor log** and **Dashboard**. Kiosk **Check out** closes it.
Revoking a device in admin makes its cookie invalid at once.

## 3. Automated check (about 15 seconds)
With the site running, in a second terminal (needs Python 3, nothing to install):
```
python tests/smoke_test.py
```
It runs about 60 checks: public pages, security redirects, admin login, add host + question, all five pairing
methods, single-use codes, host portal, SignalR, check-in, validation, check-out and revoke. Then it cleans up.
Failures print the reason. Options: `--base https://localhost:7080 --email ... --password ... --no-cleanup`.
(`dotnet run` in Development allows 200 pairing attempts/min so the test is not rate limited. In production it is 10.)

## 4. Manual checklist (what you should SEE)
| # | Do this | Expected |
|---|---|---|
| 1 | Open `/Kiosk` in a fresh browser | Redirects to **Pair this device** |
| 2 | Admin → Kiosk devices → Add device → name → **Generate** | Page with 6-digit code, QR, link |
| 3 | In a second browser open `/Kiosk/Pair`, type code | Kiosk welcome screen appears |
| 4 | Try the same code again | "Invalid or expired code" |
| 5 | Add device → **This device now** | Your browser becomes a kiosk instantly |
| 6 | Add device → **bulk**, 2 devices; open link in 3 browsers | 2 succeed, 3rd says invalid |
| 7 | Admin → Hosts → Add (tick login + password) | Host listed, "Login: Yes" |
| 8 | Admin → Questions → Add, drag rows to reorder | New order appears on kiosk after refresh |
| 9 | Kiosk: Check in → type → name → questions → **photo** → pick host | Continue stays disabled until a photo is taken; then “You're checked in” badge |
| 10 | Admin → Visitor log | Visit with the visitor's photo + answers (signature shows as image) |
| 11 | Host logs in (private window) → My notifications | Page open → pop-up **with the visitor's photo** and a chime when a visitor checks in |
| 11b | Host clicks **Enable notifications** on a phone (https), then a visitor checks in | Phone notification with the photo; page says “✓ This device is registered” |
| 12 | Kiosk: Check out → type 2+ letters of name → tap | "Goodbye", visit shows Left |
| 13 | Admin → Revoke the kiosk device | Kiosk reloads to the pairing screen |
| 14 | Leave kiosk idle 90 s mid-flow | Returns to welcome screen |

## 5. Push notifications on real phones/laptops (closed browser)
Push and "install as app" need a **trusted HTTPS** address. `https://localhost` works only on the same PC.
For phones use a tunnel:
1. `ngrok http https://localhost:7080`  (or Visual Studio **Dev Tunnels**) → gives `https://xxxx.ngrok-free.app`
2. Open **the admin portal through that URL** (so QR codes/links contain it), or set `App:PublicBaseUrl`.
3. Nothing to configure: the push keys are created automatically on first start.
4. Host opens the tunnel URL on their phone, logs in, **Enable notifications**.
   iPhone/iPad: Share → **Add to Home Screen**, open that icon, then enable (iOS 16.4+).
5. Admin → Hosts → **Test** → every enabled device of that host should buzz, and the page tells you how many were reached. Then do a real kiosk check-in: the notification shows the visitor's photo.

## 6. Troubleshooting
| Symptom | Fix |
|---|---|
| `Cannot open database` / LocalDB error | Install SQL Server LocalDB or edit the connection string |
| Browser says certificate not trusted | `dotnet dev-certs https --trust`, restart browser |
| QR/link opens "localhost" on phone | Open admin via tunnel/LAN URL or set `App:PublicBaseUrl` |
| Kiosk cookie lost after closing browser | Don't use Incognito/private mode for kiosks |
| 429 on pairing | Too many attempts: wait a minute (limit 10/min in production) |
| Test says “No phone or laptop is registered” | The host must open *My notifications* on each device and click **Enable notifications** |
| Test says “No device could be reached (403…)” | Click **Enable notifications** again on that device (it re-subscribes with the current key) |
| Kiosk says the camera is blocked / unavailable | Allow the camera for the site; use https (or localhost); or tap “Use camera app” |
| Photo missing from the phone notification | Phone must reach the site address; set `App:PublicBaseUrl`; iPhone never shows the picture in a push |
| No push on iPhone | Must be installed to Home Screen and opened from there |
| Admin login fails | Email/password in `appsettings.json` → `Admin` (only used the first time the DB is created; delete the DB to re-seed) |

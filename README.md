# Visitor Check-In  ·  ASP.NET Core MVC

Front-desk visitor management: an **admin portal**, a **touch-screen kiosk**, and **instant host alerts**
(SignalR live toast + Web Push + optional email).

## Open and run in Visual Studio

1. Install **Visual Studio 2022 (17.13 or newer)** or **Visual Studio 2026**, with the *ASP.NET and web development* workload.
   (`.slnx` is the new XML solution format; older Visual Studio versions cannot open it.)
2. Double-click **`VisitorCheckIn.slnx`**.
3. Press **F5** (or the green ▶ button). The site opens at **https://localhost:7080**.
4. Sign in: `admin@example.com` / `ChangeMe123` *(change it in `VisitorCheckIn/appsettings.json` first)*.

The database (SQL Server LocalDB, installed with Visual Studio), roles, admin user and sample questions are created
automatically on first start. No migrations to run.

### Without Visual Studio
| | |
|---|---|
| Windows | double-click **`RUN.bat`** |
| Mac / Linux | `./run.sh` |
| Any OS | `dotnet run --project VisitorCheckIn` |

Needs the .NET 8 SDK or newer. (The project targets .NET 8 and is set to roll forward, so a machine with only .NET 9 / 10 also works.)
On Mac/Linux use SQL Server in Docker and change `ConnectionStrings:Default` (see `docs/TESTING.md`).

### Launch profiles (the dropdown next to ▶)
- **VisitorCheckIn** – `https://localhost:7080` (default).
- **VisitorCheckIn (LAN - phones/tablets)** – listens on all network interfaces so a phone or iPad on the same Wi-Fi can open
  `https://<your-PC-IP>:7080`. Windows Firewall will ask for permission the first time.

## Solution layout
```
VisitorCheckIn.slnx            ← open this
README.md · RUN.bat · run.sh
docs/TESTING.md                ← how to test, manual checklist
tests/smoke_test.py            ← ~60 automated end-to-end checks (python, nothing to install)
VisitorCheckIn/                ← the web project
├─ Program.cs · appsettings.json · VisitorCheckIn.csproj
├─ Controllers/  Home, Account, Dashboard, Hosts, Questions, Devices, Visits, Kiosk, HostPortal
├─ Views/        Shared (_Layout app shell · _AuthLayout · _KioskLayout) + one folder per controller
├─ Models/ · ViewModels/ · Data/ · Services/ · Filters/ · Hubs/ · Helpers/
└─ wwwroot/
   ├─ css/       site.css (admin design system) · kiosk.css (touch kiosk)
   ├─ js/        kiosk.js · host.js · common.js
   ├─ lib/       bootstrap · sortablejs · signalr   (bundled, no CDN needed)
   ├─ fonts/     Plus Jakarta Sans (bundled)
   └─ img/ icons/ · sw.js · *.webmanifest
```

## What the interface looks like
- **Admin / host portal** – left-hand app shell (collapses to a menu on phones), stat tiles, avatar rows, status pills, empty states.
- **Kiosk** – large live clock welcome screen, step progress bar, 64px+ touch targets, host tiles with initials, and a
  **visitor badge** confirmation. Everything is bundled locally, so the kiosk does not depend on internet CDNs.
- Change the brand colour in one place: the `--accent` variables at the top of `site.css` and `kiosk.css`.

## Host notifications (with the visitor's photo)
When a visitor picks a host, **every device of that host** is alerted at once, and every alert carries the visitor's photo:

| Channel | When it works | Photo |
|---|---|---|
| **Live pop-up + chime** | the host has the *My notifications* page open | in the pop-up |
| **Push notification** | any phone / laptop the host enabled (works with the page closed) | big picture on Android / Windows / Chrome (iPhone shows the text only) |
| **Email** (optional, fill in `Smtp`) | always, if the host has an email and SMTP is set | inline in the email |

**No key setup is needed any more.** The push keys are created automatically the first time the app starts and are kept in the database.
(You can still paste your own into `Vapid:Public` / `Vapid:Private`.)

**Each host, once per device:** sign in → **My notifications** → **Enable notifications on this device**. The page then says
“✓ This device is registered”. Use **Send me a test** to check. Admins can use **Hosts → Test** to ping a host's devices;
the result says exactly what happened (for example “No phone or laptop is registered yet”).

### Notifications not arriving? Check in this order
1. **https is required.** Browsers only allow push on `https://` (or `localhost`). A phone opening `https://192.168.x.x:7080` with the
   untrusted developer certificate will refuse. Easiest fixes: in Visual Studio use a **Dev Tunnel** (a public https address with a
   valid certificate), or deploy to a real domain.
2. The host must click **Enable notifications on this device** on *each* phone / laptop (the portal shows the device in “My alert devices”).
3. iPhone / iPad: *Share → Add to Home Screen*, open it from the Home Screen, then enable (iOS 16.4+).
4. The browser must have notifications allowed for the site (not blocked) and the phone must not be in Do Not Disturb.
5. The photo in a push notification needs the phone to reach the same address. Set `App:PublicBaseUrl` to your public https address.
6. Still stuck? Press **Hosts → Test** and read the message; the app log lines starting `Push to device` show the push service's answer.

## Visitor photo (mandatory)
After the questions, the kiosk asks every visitor to **take a photo** (3-2-1 countdown, retake allowed). They cannot continue without it,
and the server rejects a check-in that has no photo.
- The photo appears in the **Dashboard**, **Visitor log** and **Host portal** (click it to enlarge) and in every notification.
- The kiosk needs camera permission once. Allow it when the browser asks. The camera needs **https** (or `localhost`); on a plain-http
  address the kiosk offers “Use camera app” instead, which opens the device camera on phones and tablets.
- Settings in `appsettings.json`: `Kiosk:RequirePhoto` (`true` by default; `false` makes the step skippable) and
  `Photos:RetentionDays` (`0` keeps photos forever; e.g. `90` deletes photos older than 90 days automatically).
- Photos are stored in the database (table `VisitPhotos`) and served at an unguessable link (`/p/<48 random characters>`), which is what lets
  notifications and emails show the picture. Tell visitors how photos are used and how long they are kept (privacy law may require this).

## Admin setup order
1. **Hosts** → Add host (tick “can sign in” + password for alerts).
2. **Questions** → add, drag to reorder, choose visitor type / required / hide.
3. **Kiosk devices** → Add device (five pairing methods below).
4. Each host signs in on **every phone/laptop** → *My notifications* → **Enable notifications**.

## Five ways to add a kiosk device
| | Method | Best for |
|---|---|---|
| A | **6-digit code** – open `/Kiosk/Pair`, type the code | any device |
| B | **QR code** – scan with the camera | iPad / phone / tablet |
| C | **Setup link** – send by WhatsApp / email | remote devices |
| D | **This device now** – one click | your own laptop, quick tests |
| E | **Bulk / MDM link** – one reusable link, N devices | Intune / Jamf / Fully Kiosk / many iPads |

Each device gets a unique, revocable token (only a SHA-256 hash is stored; cookie is HttpOnly). **Revoke** and the device
returns to the pairing screen at once. Codes/links expire and pairing endpoints are rate-limited.
Lock-down steps for iPad, Android, Windows, macOS, Chromebook and Raspberry Pi: **Admin → Kiosk devices → Lock-down guide**.

## Going live checklist
- Change the admin password; host behind HTTPS (IIS / Azure App Service / nginx).
- Set `App:PublicBaseUrl` (your public https address) and the connection string. Push keys are created automatically; keep the database backed up, because the keys live in it.
- Replace `EnsureCreatedAsync` with EF migrations: `dotnet ef migrations add Initial` then `dotnet ef database update`.
- Back up the database (signatures are stored as PNG data in answers).

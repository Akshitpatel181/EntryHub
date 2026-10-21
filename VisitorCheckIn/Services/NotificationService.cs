using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;
using VisitorCheckIn.Hubs;
using VisitorCheckIn.Models;
using WebPush;

namespace VisitorCheckIn.Services;

/// <summary>The visitor photo that travels with a notification.</summary>
/// <param name="Path">Relative url, e.g. /p/abc... (used by the live toast on an open page)</param>
/// <param name="AbsoluteUrl">Full url (used by push notifications and email)</param>
/// <param name="Bytes">Raw JPEG (attached to the email)</param>
public record NotifyPhoto(string Path, string AbsoluteUrl, byte[] Bytes);

public record NotifyResult(int PushSent, int PushFailed, bool PushConfigured, int DeviceCount = 0, string LastError = null, bool EmailSent = false)
{
    /// <summary>Plain-English result for the admin / host, including what to do when nothing was delivered.</summary>
    public string Describe()
    {
        if (!PushConfigured) return "Push keys are missing. Restart the app and they are created automatically.";
        if (DeviceCount == 0)
            return "No phone or laptop is registered for this host yet. The host must sign in on each device, open “My notifications” and click “Enable notifications on this device”.";
        if (PushSent > 0)
            return PushFailed > 0
                ? $"Sent to {PushSent} device(s); {PushFailed} could not be reached ({LastError})."
                : $"Sent to {PushSent} device(s).";
        return $"No device could be reached ({LastError}). On each device open “My notifications” and click “Enable notifications” again.";
    }
}

/// <summary>Pings EVERY device of a host: SignalR (open pages) + Web Push (all registered devices) + optional email.
/// The visitor photo is attached to all three.</summary>
public class NotificationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<HostHub> _hub;
    private readonly IConfiguration _cfg;
    private readonly VapidKeyService _vapid;
    private readonly ILogger<NotificationService> _log;

    public NotificationService(AppDbContext db, IHubContext<HostHub> hub, IConfiguration cfg, VapidKeyService vapid, ILogger<NotificationService> log)
    { _db = db; _hub = hub; _cfg = cfg; _vapid = vapid; _log = log; }

    public async Task<NotifyResult> NotifyAsync(int hostId, string title, string body, string url = "/HostPortal", NotifyPhoto photo = null)
    {
        var host = await _db.Hosts.Include(h => h.Devices).FirstOrDefaultAsync(h => h.Id == hostId);
        if (host == null) return new NotifyResult(0, 0, _vapid.Configured);
        var devices = host.Devices.ToList();

        // 1) Live: any open Host Portal page (toast with the photo)
        try { await _hub.Clients.Group($"host-{hostId}").SendAsync("VisitorArrived", title, body, photo?.Path); }
        catch (Exception ex) { _log.LogWarning(ex, "SignalR notify failed"); }

        // 2) Web Push: every registered phone / laptop / tablet, all at the same time
        int sent = 0, failed = 0;
        string lastError = null;
        if (_vapid.Configured && devices.Count > 0)
        {
            var client = new WebPushClient();
            var vapid = new VapidDetails(_vapid.Subject, _vapid.Public, _vapid.Private);
            var payload = JsonSerializer.Serialize(new { title, body, url, image = photo?.AbsoluteUrl });
            var gone = new List<HostDevice>();

            var jobs = devices.Select(async d =>
            {
                try
                {
                    var send = client.SendNotificationAsync(new PushSubscription(d.Endpoint, d.P256dh, d.Auth), payload, vapid);
                    var winner = await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(10)));
                    if (winner != send) throw new TimeoutException("the push service did not answer in time");
                    await send;
                    Interlocked.Increment(ref sent);
                }
                catch (WebPushException ex)
                {
                    Interlocked.Increment(ref failed);
                    lastError = $"{(int)ex.StatusCode} {ex.StatusCode}";
                    _log.LogWarning("Push to device {Id} failed: {Status} {Msg}", d.Id, ex.StatusCode, ex.Message);
                    if (ex.StatusCode == HttpStatusCode.Gone || ex.StatusCode == HttpStatusCode.NotFound)
                        lock (gone) gone.Add(d);   // the device unsubscribed - clean up
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    lastError = ex.Message;
                    _log.LogWarning(ex, "Push to device {Id} failed", d.Id);
                }
            }).ToList();
            await Task.WhenAll(jobs);

            if (gone.Count > 0) { _db.HostDevices.RemoveRange(gone); await _db.SaveChangesAsync(); }
        }

        // 3) Email (only if SMTP is configured) - includes the photo inline
        var emailed = await TrySendEmailAsync(host.Email, title, body, photo);
        return new NotifyResult(sent, failed, _vapid.Configured, devices.Count, lastError, emailed);
    }

    private async Task<bool> TrySendEmailAsync(string to, string subject, string body, NotifyPhoto photo)
    {
        var smtpHost = _cfg["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(to)) return false;
        try
        {
            using var smtp = new SmtpClient(smtpHost, int.Parse(_cfg["Smtp:Port"] ?? "587"))
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_cfg["Smtp:User"], _cfg["Smtp:Password"])
            };
            using var msg = new MailMessage(_cfg["Smtp:From"] ?? "noreply@example.com", to, subject, body);
            MemoryStream stream = null;
            if (photo?.Bytes != null)
            {
                stream = new MemoryStream(photo.Bytes);
                var html = "<div style=\"font-family:Segoe UI,Arial,sans-serif\"><p style=\"font-size:16px\">" + WebUtility.HtmlEncode(body) +
                           "</p><img src=\"cid:visitorphoto\" alt=\"Visitor photo\" style=\"width:240px;border-radius:12px\" /></div>";
                var view = AlternateView.CreateAlternateViewFromString(html, null, MediaTypeNames.Text.Html);
                view.LinkedResources.Add(new LinkedResource(stream, "image/jpeg") { ContentId = "visitorphoto" });
                msg.AlternateViews.Add(view);
            }
            await smtp.SendMailAsync(msg);
            stream?.Dispose();
            return true;
        }
        catch (Exception ex) { _log.LogWarning(ex, "Email notify failed"); return false; }
    }
}

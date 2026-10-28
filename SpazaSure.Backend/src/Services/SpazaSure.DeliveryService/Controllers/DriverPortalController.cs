using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;

namespace SpazaSure.DeliveryService.Controllers;

/// <summary>
/// The driver's page, opened from the private link an admin sends them (no app or password
/// needed). It lists their deliveries and past ones, can alert the phone when a new delivery is
/// allocated, and shares the phone's location while they drive, which the shop sees on its
/// tracking map.
/// </summary>
[ApiController]
[AllowAnonymous]
public class DriverPortalController(SpazaSureDbContext db, DriverAlerts? alerts = null) : ControllerBase
{
    /// <summary>How far back the driver's delivery history goes.</summary>
    private const int HistoryDays = 90;

    private Task<Driver?> FindAsync(string token) =>
        token.Length is < 32 or > 64
            ? Task.FromResult<Driver?>(null)
            : db.Drivers.FirstOrDefaultAsync(d => d.AccessToken == token && d.IsActive);

    [HttpGet("api/delivery/driver/{token}/jobs")]
    public async Task<IActionResult> Jobs(string token)
    {
        var driver = await FindAsync(token);
        if (driver is null) return NotFound(ApiResponse.Fail("This link no longer works. Ask SpazaSure for a new one."));
        var jobs = await db.Orders.AsNoTracking()
            .Include(o => o.Shop).Include(o => o.Supplier).Include(o => o.SupplierBranch)
            .Where(o => o.DriverId == driver.Id && DriversController.OpenStatuses.Contains(o.Status))
            .OrderBy(o => o.DriverAssignedAt)
            .ToListAsync();

        var since = DateTime.UtcNow.AddDays(-HistoryDays);
        var history = await db.Orders.AsNoTracking()
            .Where(o => o.DriverId == driver.Id && (o.Status == "delivered" || o.Status == "cancelled")
                && (o.DeliveredAt ?? o.UpdatedAt ?? o.CreatedAt) >= since)
            .OrderByDescending(o => o.DeliveredAt ?? o.UpdatedAt ?? o.CreatedAt)
            .Select(o => new
            {
                o.OrderNumber, o.Status,
                Date = o.DeliveredAt ?? o.UpdatedAt ?? o.CreatedAt,
                ShopName = o.Shop.ShopName,
                Pickup = o.SupplierBranch != null ? o.Supplier.CompanyName + " - " + o.SupplierBranch.Name : o.Supplier.CompanyName,
                o.DeliveryDistanceKm,
                o.DriverPay, o.DriverPaidAt,
            })
            .ToListAsync();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return Ok(ApiResponse<object>.Ok(new
        {
            driver.FullName, driver.BadgeCode, driver.VehicleRegistration,
            Jobs = jobs.Select(o => new
            {
                o.OrderNumber, o.Status,
                StatusLabel = StatusLabel(o.Status),
                AssignedAt = o.DriverAssignedAt,
                Pickup = Tracking.Pickup(o),
                Dropoff = new
                {
                    o.Shop.ShopName, o.Shop.Phone, Address = o.DeliveryAddress ?? o.Shop.Address,
                    Latitude = o.DeliveryLatitude ?? o.Shop.Latitude, Longitude = o.DeliveryLongitude ?? o.Shop.Longitude,
                },
                // Cash orders: the shop pays on arrival and reads out its delivery code
                CashOnDelivery = o.PaymentMethod == "cash_on_delivery",
                o.TotalAmount,
            }).ToList(),
            History = history.Select(h => new
            {
                h.OrderNumber, h.Status, StatusLabel = StatusLabel(h.Status), h.Date, h.ShopName, h.Pickup,
                h.DeliveryDistanceKm, h.DriverPay, Paid = h.DriverPaidAt != null,
            }).ToList(),
            ThisMonth = new
            {
                Delivered = history.Count(h => h.Status == "delivered" && h.Date >= monthStart),
                Paid = history.Where(h => h.DriverPaidAt >= monthStart).Sum(h => h.DriverPay ?? 0m),
            },
        }));
    }

    /// <summary>Plain words for an order's status, as the driver sees it.</summary>
    internal static string StatusLabel(string status) => status switch
    {
        "pending" or "confirmed" => "Waiting for the supplier",
        "processing" => "Being packed",
        "ready" => "Ready to collect",
        "dispatched" => "On the way to the shop",
        "delivered" => "Delivered",
        "cancelled" => "Cancelled",
        _ => status,
    };

    [HttpPost("api/delivery/driver/{token}/location")]
    public async Task<IActionResult> Location(string token, [FromBody] DriverLocationRequest req)
    {
        var driver = await FindAsync(token);
        if (driver is null) return NotFound(ApiResponse.Fail("This link no longer works."));
        if (!double.IsFinite(req.Latitude) || !double.IsFinite(req.Longitude) ||
            req.Latitude is < -90 or > 90 || req.Longitude is < -180 or > 180)
            return BadRequest(ApiResponse.Fail("Invalid location."));
        driver.LastLatitude = req.Latitude;
        driver.LastLongitude = req.Longitude;
        driver.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Location shared."));
    }

    /// <summary>The key the page needs to turn delivery alerts on.</summary>
    [HttpGet("api/delivery/driver/{token}/alerts/key")]
    public async Task<IActionResult> AlertsKey(string token)
    {
        if (await FindAsync(token) is null) return NotFound(ApiResponse.Fail("This link no longer works."));
        if (alerts is null) return StatusCode(503, ApiResponse.Fail("Delivery alerts are not available right now."));
        return Ok(ApiResponse<object>.Ok(new { PublicKey = await alerts.PublicKeyAsync() }));
    }

    /// <summary>Turns delivery alerts on for this phone.</summary>
    [HttpPost("api/delivery/driver/{token}/alerts")]
    public async Task<IActionResult> SubscribeAlerts(string token, [FromBody] DriverAlertSubscriptionRequest req)
    {
        var driver = await FindAsync(token);
        if (driver is null) return NotFound(ApiResponse.Fail("This link no longer works."));
        if (!Uri.TryCreate(req.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps
            || req.Endpoint.Length > 1000 || string.IsNullOrWhiteSpace(req.P256dh) || req.P256dh.Length > 200
            || string.IsNullOrWhiteSpace(req.Auth) || req.Auth.Length > 100)
            return BadRequest(ApiResponse.Fail("This phone's alert details are not valid."));

        // One row per phone; a phone that moved to another driver's link now alerts that driver
        var existing = await db.DriverPushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == req.Endpoint);
        if (existing is null)
            db.DriverPushSubscriptions.Add(new DriverPushSubscription
                { DriverId = driver.Id, Endpoint = req.Endpoint, P256dh = req.P256dh, Auth = req.Auth });
        else
        {
            existing.DriverId = driver.Id;
            existing.P256dh = req.P256dh;
            existing.Auth = req.Auth;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        if (alerts is not null) await alerts.SendAsync(driver, "Delivery alerts are on",
            "You'll get a notification here when SpazaSure gives you a delivery.", "welcome");
        return Ok(ApiResponse.Ok("Delivery alerts are on."));
    }

    [HttpDelete("api/delivery/driver/{token}/alerts")]
    public async Task<IActionResult> UnsubscribeAlerts(string token, [FromQuery] string endpoint)
    {
        var driver = await FindAsync(token);
        if (driver is null) return NotFound(ApiResponse.Fail("This link no longer works."));
        await db.DriverPushSubscriptions.Where(s => s.DriverId == driver.Id && s.Endpoint == endpoint).ExecuteDeleteAsync();
        return Ok(ApiResponse.Ok("Delivery alerts are off on this phone."));
    }

    /// <summary>Shows delivery alerts while the page is closed. Served under /driver/ so it covers the driver page.</summary>
    [HttpGet("driver/sw.js")]
    public ContentResult ServiceWorker()
    {
        Response.Headers.CacheControl = "no-cache";
        return Content(ServiceWorkerScript, "text/javascript; charset=utf-8");
    }

    /// <summary>Lets the driver add the page to the home screen (needed on iPhones for alerts).</summary>
    [HttpGet("driver/{token}/manifest.webmanifest")]
    public async Task<IActionResult> Manifest(string token)
    {
        if (await FindAsync(token) is null) return NotFound();
        return new JsonResult(new
        {
            name = "SpazaSure Driver",
            short_name = "SpazaSure",
            start_url = $"/driver/{token}",
            scope = "/driver/",
            display = "standalone",
            background_color = "#F5F7FF",
            theme_color = "#25449A",
        }) { ContentType = "application/manifest+json" };
    }

    [HttpGet("driver/{token}")]
    public async Task<ContentResult> Page(string token)
    {
        var driver = await FindAsync(token);
        var body = driver is null
            ? "<h1>Link not working</h1><p>This driver link has been replaced or switched off. Ask SpazaSure for a new one.</p>"
            : DriverPageBody(driver, token);
        var manifest = driver is null ? "" : $"<link rel=\"manifest\" href=\"/driver/{WebUtility.HtmlEncode(token)}/manifest.webmanifest\">";
        return Content(
            "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta name=\"robots\" content=\"noindex\">" +
            "<meta name=\"theme-color\" content=\"#25449A\"><meta name=\"apple-mobile-web-app-capable\" content=\"yes\">" +
            manifest +
            "<title>SpazaSure driver</title><style>" + Css + "</style></head><body><main>" + body + "</main></body></html>",
            "text/html; charset=utf-8");
    }

    private static string DriverPageBody(Driver driver, string token)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        return $$"""
            <header><h1>Hi {{E(driver.FullName)}}</h1>
            <p>Badge <b>{{E(driver.BadgeCode)}}</b> &middot; {{E(driver.VehicleRegistration)}}</p></header>
            <section class="card alerts" id="alertsBox">
              <button id="alerts" class="secondary">Turn on delivery alerts</button>
              <p id="alertsStatus">Get a notification on this phone when SpazaSure gives you a delivery.</p>
            </section>
            <section class="card share">
              <button id="share">Start sharing my location</button>
              <p id="status">Shops can follow you on the map while you share. Keep this page open while you drive.</p>
            </section>
            <h2>Your deliveries</h2>
            <div id="jobs"><p>Loading...</p></div>
            <h2>History <span id="month"></span></h2>
            <div id="history"><p>Loading...</p></div>
            <script>
            const token = {{System.Text.Json.JsonSerializer.Serialize(token)}};
            const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
            const money = n => 'R' + Number(n || 0).toFixed(2);
            const day = d => new Date(d).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
            const maps = (lat, lng, address) => lat != null && lng != null
              ? `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}`
              : `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(address || '')}`;

            // Ring and vibrate when a delivery appears that wasn't there before (while the page is open)
            let known = null;
            function ring() {
              try {
                const ctx = new (window.AudioContext || window.webkitAudioContext)();
                [0, 0.35, 0.7].forEach(t => {
                  const o = ctx.createOscillator(), g = ctx.createGain();
                  o.frequency.value = 880; o.connect(g); g.connect(ctx.destination);
                  g.gain.setValueAtTime(0.3, ctx.currentTime + t); g.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + t + 0.3);
                  o.start(ctx.currentTime + t); o.stop(ctx.currentTime + t + 0.3);
                });
              } catch {}
              if (navigator.vibrate) navigator.vibrate([300, 150, 300, 150, 600]);
            }

            async function loadJobs() {
              const res = await fetch(`/api/delivery/driver/${token}/jobs`);
              const box = document.getElementById('jobs');
              if (!res.ok) { box.innerHTML = '<p>Could not load your deliveries.</p>'; return; }
              const data = (await res.json()).data;
              const numbers = data.jobs.map(j => j.orderNumber);
              const fresh = known === null ? [] : numbers.filter(n => !known.includes(n));
              known = numbers;
              if (fresh.length) {
                ring();
                document.title = `(${fresh.length}) New delivery - SpazaSure`;
              }
              box.innerHTML = data.jobs.length === 0 ? '<p class="empty">No deliveries for you right now. You\'ll get an alert when there is one.</p>' : data.jobs.map(j => `
                <article class="${fresh.includes(j.orderNumber) ? 'new' : ''}">
                  <h3>${esc(j.orderNumber)} <span class="st ${esc(j.status)}">${esc(j.statusLabel)}</span></h3>
                  ${fresh.includes(j.orderNumber) ? '<p class="badge">New delivery</p>' : ''}
                  <p><b>Collect from</b><br>${esc(j.pickup.name)}<br>${esc(j.pickup.address)}
                    <br><a href="${maps(j.pickup.latitude, j.pickup.longitude, j.pickup.address)}" target="_blank" rel="noopener">Directions</a></p>
                  <p><b>Deliver to</b><br>${esc(j.dropoff.shopName)} &middot; <a href="tel:${esc(j.dropoff.phone)}">${esc(j.dropoff.phone)}</a><br>${esc(j.dropoff.address)}
                    <br><a href="${maps(j.dropoff.latitude, j.dropoff.longitude, j.dropoff.address)}" target="_blank" rel="noopener">Directions</a></p>
                  ${j.cashOnDelivery ? `<p class="cash">Cash on delivery: collect ${money(j.totalAmount)} and ask the shop for its delivery code.</p>` : ''}
                </article>`).join('');

              document.getElementById('month').textContent =
                `· this month: ${data.thisMonth.delivered} delivered${data.thisMonth.paid > 0 ? ', ' + money(data.thisMonth.paid) + ' paid' : ''}`;
              document.getElementById('history').innerHTML = data.history.length === 0
                ? '<p class="empty">Your completed deliveries will show here.</p>'
                : '<div class="card list">' + data.history.map(h => `
                  <div class="row">
                    <div><b>${esc(h.orderNumber)}</b> <span class="st ${esc(h.status)}">${esc(h.statusLabel)}</span>
                      <br><small>${day(h.date)} &middot; ${esc(h.pickup)} &rarr; ${esc(h.shopName)}${h.deliveryDistanceKm ? ' &middot; ' + Number(h.deliveryDistanceKm).toFixed(1) + ' km' : ''}</small></div>
                    <div class="pay">${h.status !== 'delivered' ? '' : h.paid ? money(h.driverPay) + '<br><small>paid</small>' : '<small>payment<br>pending</small>'}</div>
                  </div>`).join('') + '</div>';
            }
            document.addEventListener('visibilitychange', () => { if (!document.hidden) { document.title = 'SpazaSure driver'; loadJobs(); } });

            // Delivery alerts: standard web push, shown by /driver/sw.js even when this page is closed
            const alertsBtn = document.getElementById('alerts'), alertsStatus = document.getElementById('alertsStatus');
            const keyBytes = b64 => { const p = '='.repeat((4 - b64.length % 4) % 4); const raw = atob((b64 + p).replace(/-/g, '+').replace(/_/g, '/')); return Uint8Array.from([...raw].map(c => c.charCodeAt(0))); };
            async function alertsState() {
              if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
                const ios = /iphone|ipad|ipod/i.test(navigator.userAgent);
                alertsBtn.disabled = true;
                alertsStatus.textContent = ios
                  ? 'On an iPhone: tap Share, then "Add to Home Screen", and open SpazaSure from your home screen to turn alerts on.'
                  : 'This browser cannot show delivery alerts. Open this link in Chrome.';
                return null;
              }
              const reg = await navigator.serviceWorker.register('/driver/sw.js', { scope: '/driver/' });
              const sub = await reg.pushManager.getSubscription();
              alertsBtn.textContent = sub ? 'Turn off delivery alerts' : 'Turn on delivery alerts';
              alertsBtn.className = sub ? 'off' : 'secondary';
              alertsStatus.textContent = sub
                ? 'Alerts are on. You\'ll get a notification on this phone for every new delivery.'
                : Notification.permission === 'denied'
                  ? 'Notifications are blocked for this page. Allow them in your browser settings, then try again.'
                  : 'Get a notification on this phone when SpazaSure gives you a delivery.';
              return { reg, sub };
            }
            alertsBtn.onclick = async () => {
              const state = await alertsState();
              if (!state) return;
              alertsBtn.disabled = true;
              try {
                if (state.sub) {
                  await fetch(`/api/delivery/driver/${token}/alerts?endpoint=${encodeURIComponent(state.sub.endpoint)}`, { method: 'DELETE' });
                  await state.sub.unsubscribe();
                } else {
                  if (await Notification.requestPermission() !== 'granted') { await alertsState(); return; }
                  const key = (await (await fetch(`/api/delivery/driver/${token}/alerts/key`)).json()).data.publicKey;
                  const sub = await state.reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(key) });
                  const json = sub.toJSON();
                  const res = await fetch(`/api/delivery/driver/${token}/alerts`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth }) });
                  if (!res.ok) throw new Error('save failed');
                }
              } catch { alertsStatus.textContent = 'Could not change alerts. Check your signal and try again.'; }
              finally { alertsBtn.disabled = false; }
              await alertsState();
            };
            alertsState();

            let watch = null, lastSent = 0;
            const status = document.getElementById('status'), button = document.getElementById('share');
            async function send(pos) {
              if (Date.now() - lastSent < 15000) return;
              lastSent = Date.now();
              try {
                await fetch(`/api/delivery/driver/${token}/location`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ latitude: pos.coords.latitude, longitude: pos.coords.longitude }) });
                status.textContent = 'Sharing your location. Last sent ' + new Date().toLocaleTimeString() + '.';
              } catch { status.textContent = 'No signal. Will try again.'; }
            }
            button.onclick = () => {
              if (watch !== null) { navigator.geolocation.clearWatch(watch); watch = null; button.textContent = 'Start sharing my location'; status.textContent = 'Location sharing stopped.'; return; }
              if (!navigator.geolocation) { status.textContent = 'This phone cannot share its location from the browser.'; return; }
              watch = navigator.geolocation.watchPosition(send,
                err => { status.textContent = 'Allow location access for this page, then try again. (' + err.message + ')'; },
                { enableHighAccuracy: true, maximumAge: 10000 });
              button.textContent = 'Stop sharing';
              status.textContent = 'Starting...';
            };
            loadJobs(); setInterval(loadJobs, 30000);
            </script>
            """;
    }

    private const string ServiceWorkerScript = """
        self.addEventListener('install', () => self.skipWaiting());
        self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));
        self.addEventListener('push', e => {
          let d = {};
          try { d = e.data ? e.data.json() : {}; } catch { d = { body: e.data ? e.data.text() : '' }; }
          e.waitUntil(self.registration.showNotification(d.title || 'SpazaSure', {
            body: d.body || 'You have a new delivery.',
            tag: d.tag || 'delivery',
            renotify: true,
            requireInteraction: true,
            vibrate: [300, 150, 300, 150, 600],
            data: { url: d.url || '/driver/' },
          }));
        });
        self.addEventListener('notificationclick', e => {
          e.notification.close();
          const url = e.notification.data && e.notification.data.url || '/driver/';
          e.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(list => {
            for (const c of list) if (c.url.includes(url) && 'focus' in c) return c.focus();
            return self.clients.openWindow(url);
          }));
        });
        """;

    private const string Css =
        "body{font-family:system-ui,sans-serif;background:#F5F7FF;color:#17244F;margin:0}main{max-width:560px;margin:0 auto;padding:16px}" +
        "header{background:#25449A;color:#fff;border-radius:16px;padding:16px}header h1{margin:0 0 4px;font-size:20px}header p{margin:0;opacity:.9}" +
        ".card{background:#fff;border-radius:16px;padding:16px;margin:12px 0}button{width:100%;padding:14px;border:0;border-radius:12px;" +
        "background:#F8B217;color:#17244F;font-weight:800;font-size:16px}button.secondary{background:#25449A;color:#fff}" +
        "button.off{background:#E8ECF8;color:#25449A}button:disabled{opacity:.6}#status,#alertsStatus{font-size:13px;color:#5A5A7A;margin-bottom:0}" +
        "article{background:#fff;border-radius:16px;padding:14px;margin-bottom:10px}article.new{outline:3px solid #078C50}" +
        "article h3{margin:0 0 8px;font-size:16px;display:flex;justify-content:space-between;gap:8px;align-items:center}" +
        ".st{font-size:11px;font-weight:700;background:#E8ECF8;color:#25449A;border-radius:99px;padding:3px 9px;white-space:nowrap}" +
        ".st.dispatched{background:#FEF5E0;color:#8A5A00}.st.ready{background:#E3F4EC;color:#078C50}.st.delivered{background:#E3F4EC;color:#078C50}" +
        ".st.cancelled{background:#FDE7E7;color:#B71C1C}.badge{display:inline-block;background:#078C50;color:#fff;font-size:12px;font-weight:700;border-radius:8px;padding:3px 8px;margin:0 0 6px}" +
        "a{color:#25449A;font-weight:700}.cash{background:#FEF5E0;border-radius:10px;padding:8px}h2{font-size:16px}h2 span{font-size:12px;font-weight:600;color:#5A5A7A}" +
        ".empty{color:#5A5A7A;font-size:14px}.list{padding:4px 16px}.row{display:flex;justify-content:space-between;gap:12px;padding:12px 0;border-bottom:1px solid #EEF0F7}" +
        ".row:last-child{border-bottom:0}.row small{color:#5A5A7A}.pay{text-align:right;font-weight:800;white-space:nowrap}";
}

public record DriverLocationRequest(double Latitude, double Longitude);

public record DriverAlertSubscriptionRequest(string Endpoint, string P256dh, string Auth);

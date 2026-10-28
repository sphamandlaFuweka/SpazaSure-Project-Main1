using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
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
public class DriverPortalController(SpazaSureDbContext db, DriverAlerts? alerts = null, EventPublisher? events = null) : ControllerBase
{
    /// <summary>Further than this from the shop's pin when delivery is confirmed, and admins are told to look.</summary>
    private const double FarFromShopMetres = 1000;
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

    /// <summary>
    /// The driver confirms, at the supplier, that they have the goods: the order is on its way.
    /// The shop is told, with its shop number, which the driver checks on arrival.
    /// </summary>
    [HttpPost("api/delivery/driver/{token}/orders/{orderNumber}/collected")]
    public async Task<IActionResult> Collected(string token, string orderNumber, [FromBody] DriverStepRequest? req = null)
    {
        var driver = await FindAsync(token);
        if (driver is null) return NotFound(ApiResponse.Fail("This link no longer works."));
        var order = await DriversOrderAsync(driver, orderNumber);
        if (order is null) return NotFound(ApiResponse.Fail("This delivery isn't yours, or it was given to another driver."));
        if (order.Status == "dispatched") return Ok(ApiResponse.Ok("Already marked as collected."));
        if (order.Status is not ("processing" or "ready"))
            return BadRequest(ApiResponse.Fail(order.Status is "pending" or "confirmed"
                ? "The supplier hasn't accepted this order yet. Don't collect it until it's ready."
                : $"This order is {DriverPortalController.StatusLabel(order.Status).ToLowerInvariant()}, so it can't be collected."));
        // Taking an unpaid card order would hand over the goods for free
        if (order.PaymentMethod == "paystack" && order.PaymentStatus != "paid")
            return BadRequest(ApiResponse.Fail("This order hasn't been paid yet. Don't collect it; SpazaSure will let you know."));

        order.Status = "dispatched";
        order.CollectedAt = DateTime.UtcNow;
        if (order.PaymentMethod == "cash_on_delivery") order.DeliveryCode ??= ShopDeliveryCode();
        var shopCode = await ShopVerification.EnsureCodeAsync(db, order.Shop);
        SaveLocation(driver, req);
        await db.SaveChangesAsync();

        events?.PublishNotification(
            supplierId: order.Shop.UserId.ToString(), type: "delivery", title: "Your order is on its way",
            message: $"{driver.FullName} ({driver.VehicleRegistration}) collected order {order.OrderNumber} and is on the way. " +
                     $"When they arrive, show them your shop number {shopCode} (on your SpazaSure sticker) so they know it's the right shop." +
                     (order.DeliveryCode is null ? "" : $" Pay R{order.TotalAmount:F2} in cash."),
            priority: "high", referenceId: order.Id.ToString(), routingKey: "order.dispatched");
        events?.PublishNotification(
            supplierId: order.Supplier.UserId.ToString(), type: "order", title: "Order collected",
            message: $"SpazaSure driver {driver.FullName} collected order {order.OrderNumber}.",
            referenceId: order.Id.ToString(), routingKey: "order.dispatched");
        return Ok(ApiResponse.Ok("Collected. Drive safely, and check the shop number when you arrive."));
    }

    /// <summary>
    /// The driver confirms delivery, at the shop. They must first confirm it's the right shop, by
    /// typing the shop number on its SpazaSure sticker or scanning the sticker's QR code. Cash
    /// orders also need the driver to confirm they collected the cash.
    /// </summary>
    [HttpPost("api/delivery/driver/{token}/orders/{orderNumber}/delivered")]
    public async Task<IActionResult> Delivered(string token, string orderNumber, [FromBody] DriverDeliveredRequest req)
    {
        var driver = await FindAsync(token);
        if (driver is null) return NotFound(ApiResponse.Fail("This link no longer works."));
        var order = await DriversOrderAsync(driver, orderNumber);
        if (order is null) return NotFound(ApiResponse.Fail("This delivery isn't yours, or it was given to another driver."));
        if (order.Status == "delivered") return Ok(ApiResponse.Ok("Already delivered."));
        if (order.Status != "dispatched")
            return BadRequest(ApiResponse.Fail("Confirm that you collected the order first."));

        var entered = ShopVerification.Normalize(req.ShopCode);
        if (entered is null)
            return BadRequest(ApiResponse.Fail("Type the shop number on the shop's SpazaSure sticker (like SS123456), or scan the sticker."));
        if (string.IsNullOrEmpty(order.Shop.ShopCode) || entered != order.Shop.ShopCode)
            return BadRequest(ApiResponse.Fail(
                $"That's not the shop number for {order.Shop.ShopName}. Check you're at the right shop: {order.DeliveryAddress ?? order.Shop.Address}."));
        if (order.PaymentMethod == "cash_on_delivery" && !req.CashCollected)
            return BadRequest(ApiResponse.Fail($"Collect the R{order.TotalAmount:F2} cash from the shop first, then confirm."));

        order.Status = "delivered";
        order.DeliveredAt ??= DateTime.UtcNow;
        order.DeliveredBy = "driver";
        if (order.PaymentMethod == "cash_on_delivery") order.PaymentStatus = "paid";
        var located = SaveLocation(driver, req.Latitude, req.Longitude);
        order.DeliveredLatitude = located?.Latitude;
        order.DeliveredLongitude = located?.Longitude;
        await db.SaveChangesAsync();

        events?.PublishNotification(
            supplierId: order.Shop.UserId.ToString(), type: "delivery", title: "Order delivered",
            message: $"{driver.FullName} delivered order {order.OrderNumber}. Something missing or damaged? Report it from the order.",
            referenceId: order.Id.ToString(), routingKey: "order.delivered");
        events?.PublishNotification(
            supplierId: order.Supplier.UserId.ToString(), type: "order", title: "Delivery confirmed",
            message: $"Order {order.OrderNumber} was delivered to {order.Shop.ShopName}. Your payout for it is now scheduled.",
            referenceId: order.Id.ToString(), routingKey: "order.delivered");

        // The right shop number far from the shop's pin: possible (a wrong pin), but worth a look
        var shopLat = order.DeliveryLatitude ?? order.Shop.Latitude;
        var shopLng = order.DeliveryLongitude ?? order.Shop.Longitude;
        if (events is not null && located is { } at && shopLat is { } sLat && shopLng is { } sLng
            && DistanceMetres(at.Latitude, at.Longitude, sLat, sLng) is var metres && metres > FarFromShopMetres)
            await AdminAlerts.NotifyAdminsAsync(db, events, "delivery", "Delivery confirmed away from the shop",
                $"{driver.FullName} confirmed order {order.OrderNumber} about {metres / 1000:F1} km from {order.Shop.ShopName}'s location.",
                order.Id.ToString());

        return Ok(ApiResponse.Ok("Delivered. Thank you!"));
    }

    private Task<Order?> DriversOrderAsync(Driver driver, string orderNumber) =>
        db.Orders.Include(o => o.Shop).Include(o => o.Supplier)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber && o.DriverId == driver.Id);

    private static string ShopDeliveryCode() =>
        System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static (double Latitude, double Longitude)? SaveLocation(Driver driver, DriverStepRequest? req) =>
        SaveLocation(driver, req?.Latitude, req?.Longitude);

    /// <summary>Keeps the phone's position when it was sent with a step; it also updates the shop's map.</summary>
    private static (double Latitude, double Longitude)? SaveLocation(Driver driver, double? lat, double? lng)
    {
        if (lat is not { } la || lng is not { } lo || !double.IsFinite(la) || !double.IsFinite(lo)
            || la is < -90 or > 90 || lo is < -180 or > 180) return null;
        driver.LastLatitude = la;
        driver.LastLongitude = lo;
        driver.LastSeenAt = DateTime.UtcNow;
        return (la, lo);
    }

    private static double DistanceMetres(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lon2 - lon1) / 2), 2);
        return 6_371_000 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
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
            <div id="scanner" hidden>
              <video id="scanVideo" playsinline muted></video>
              <p>Point the camera at the QR code on the shop's SpazaSure sticker.</p>
              <button id="scanClose" class="off">Cancel</button>
            </div>
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
              // Keep what the driver typed or ticked while the list refreshes
              const typed = [...box.querySelectorAll('input')].map(i => [i.id, i.type === 'checkbox' ? i.checked : i.value]);
              const focused = document.activeElement && document.activeElement.id;
              setTimeout(() => {
                for (const [id, v] of typed) { const el = document.getElementById(id); if (el) el.type === 'checkbox' ? (el.checked = v) : (el.value = v); }
                if (focused && document.getElementById(focused)) document.getElementById(focused).focus();
              });
              box.innerHTML = data.jobs.length === 0 ? '<p class="empty">No deliveries for you right now. You\'ll get an alert when there is one.</p>' : data.jobs.map(j => `
                <article class="${fresh.includes(j.orderNumber) ? 'new' : ''}">
                  <h3>${esc(j.orderNumber)} <span class="st ${esc(j.status)}">${esc(j.statusLabel)}</span></h3>
                  ${fresh.includes(j.orderNumber) ? '<p class="badge">New delivery</p>' : ''}
                  <p><b>Collect from</b><br>${esc(j.pickup.name)}<br>${esc(j.pickup.address)}
                    <br><a href="${maps(j.pickup.latitude, j.pickup.longitude, j.pickup.address)}" target="_blank" rel="noopener">Directions</a></p>
                  <p><b>Deliver to</b><br>${esc(j.dropoff.shopName)} &middot; <a href="tel:${esc(j.dropoff.phone)}">${esc(j.dropoff.phone)}</a><br>${esc(j.dropoff.address)}
                    <br><a href="${maps(j.dropoff.latitude, j.dropoff.longitude, j.dropoff.address)}" target="_blank" rel="noopener">Directions</a></p>
                  ${j.cashOnDelivery ? `<p class="cash">Cash on delivery: collect ${money(j.totalAmount)} from the shop.</p>` : ''}
                  ${j.status === 'processing' || j.status === 'ready' ? `
                    <button class="step" data-collect="${esc(j.orderNumber)}">I've collected this order</button>
                    <p class="hint">Tap once the supplier has handed you the goods.</p>` : ''}
                  ${j.status === 'dispatched' ? `
                    <div class="atshop">
                      <b>At the shop</b>
                      <p class="hint">Check it's the right shop: type the number on its SpazaSure sticker, or scan the sticker.</p>
                      <div class="row2">
                        <input id="code-${esc(j.orderNumber)}" placeholder="Shop number, e.g. SS123456" autocapitalize="characters" autocomplete="off">
                        <button class="scan" data-scan="${esc(j.orderNumber)}">Scan</button>
                      </div>
                      ${j.cashOnDelivery ? `<label class="check"><input type="checkbox" id="cash-${esc(j.orderNumber)}"> I collected ${money(j.totalAmount)} in cash</label>` : ''}
                      <button class="step done" data-deliver="${esc(j.orderNumber)}">Confirm delivered</button>
                    </div>` : ''}
                  <p class="msg" id="msg-${esc(j.orderNumber)}"></p>
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
            // ── Collecting and delivering: the driver confirms each step here ──
            const here = () => new Promise(resolve => {
              if (!navigator.geolocation) return resolve({});
              navigator.geolocation.getCurrentPosition(
                p => resolve({ latitude: p.coords.latitude, longitude: p.coords.longitude }),
                () => resolve({}), { enableHighAccuracy: true, timeout: 8000, maximumAge: 30000 });
            });
            async function step(orderNumber, action, body, button) {
              const msg = document.getElementById('msg-' + orderNumber);
              button.disabled = true;
              msg.className = 'msg';
              msg.textContent = 'Saving...';
              try {
                const res = await fetch(`/api/delivery/driver/${token}/orders/${encodeURIComponent(orderNumber)}/${action}`, {
                  method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...body, ...(await here()) }) });
                const data = await res.json().catch(() => ({}));
                msg.className = res.ok ? 'msg ok' : 'msg err';
                msg.textContent = data.message || (res.ok ? 'Done.' : 'That did not work. Try again.');
                if (res.ok) setTimeout(loadJobs, 1500);
              } catch { msg.className = 'msg err'; msg.textContent = 'No signal. Try again in a moment.'; }
              finally { button.disabled = false; }
            }
            document.getElementById('jobs').addEventListener('click', e => {
              const b = e.target.closest('button');
              if (!b) return;
              if (b.dataset.collect) {
                if (confirm('Do you have all the goods for ' + b.dataset.collect + '?')) step(b.dataset.collect, 'collected', {}, b);
              } else if (b.dataset.deliver) {
                const n = b.dataset.deliver, cash = document.getElementById('cash-' + n);
                step(n, 'delivered', { shopCode: document.getElementById('code-' + n).value, cashCollected: !!(cash && cash.checked) }, b);
              } else if (b.dataset.scan) {
                scanSticker(b.dataset.scan);
              }
            });

            // Scans the QR code on the shop's SpazaSure sticker with the phone camera, where the browser can
            let scanStream = null;
            async function scanSticker(orderNumber) {
              const msg = document.getElementById('msg-' + orderNumber);
              if (!('BarcodeDetector' in window) || !navigator.mediaDevices) {
                msg.className = 'msg err';
                msg.textContent = 'This phone cannot scan from the browser. Type the shop number from the sticker instead.';
                return;
              }
              const box = document.getElementById('scanner'), video = document.getElementById('scanVideo');
              try {
                scanStream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } });
              } catch { msg.className = 'msg err'; msg.textContent = 'Allow the camera to scan, or type the shop number.'; return; }
              video.srcObject = scanStream; await video.play(); box.hidden = false;
              const detector = new BarcodeDetector({ formats: ['qr_code'] });
              const tick = async () => {
                if (!scanStream) return;
                try {
                  const found = (await detector.detect(video))[0];
                  if (found) {
                    document.getElementById('code-' + orderNumber).value = found.rawValue;
                    stopScan();
                    msg.className = 'msg ok'; msg.textContent = 'Sticker scanned. Now confirm delivered.';
                    if (navigator.vibrate) navigator.vibrate(150);
                    return;
                  }
                } catch {}
                requestAnimationFrame(tick);
              };
              tick();
            }
            function stopScan() {
              if (scanStream) scanStream.getTracks().forEach(t => t.stop());
              scanStream = null;
              document.getElementById('scanner').hidden = true;
            }
            document.getElementById('scanClose').onclick = stopScan;

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
        ".row:last-child{border-bottom:0}.row small{color:#5A5A7A}.pay{text-align:right;font-weight:800;white-space:nowrap}" +
        ".step{margin-top:6px;background:#25449A;color:#fff}.step.done{background:#078C50}.hint{font-size:12px;color:#5A5A7A;margin:4px 0}" +
        ".atshop{border-top:1px solid #EEF0F7;margin-top:10px;padding-top:10px}.row2{display:flex;gap:8px}" +
        ".row2 input{flex:1;min-width:0;padding:12px;border:1px solid #CBD2E6;border-radius:12px;font-size:16px;text-transform:uppercase}" +
        ".scan{width:auto;padding:12px 16px;background:#E8ECF8;color:#25449A}.check{display:flex;gap:8px;align-items:center;margin:10px 0 4px;font-weight:700}" +
        ".check input{width:22px;height:22px}.msg{font-size:13px;margin:8px 0 0;font-weight:600}.msg.ok{color:#078C50}.msg.err{color:#B71C1C}" +
        "#scanner{position:fixed;inset:0;background:#000d;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:12px;padding:16px;z-index:9}" +
        "#scanner[hidden]{display:none}#scanner video{width:100%;max-width:480px;border-radius:16px}#scanner p{color:#fff;text-align:center}" +
        "#scanner button{max-width:480px}";
}

public record DriverLocationRequest(double Latitude, double Longitude);

public record DriverAlertSubscriptionRequest(string Endpoint, string P256dh, string Auth);

public record DriverStepRequest(double? Latitude, double? Longitude);

/// <summary>ShopCode: typed off the shop's sticker, or the sticker's scanned QR (a verify link).</summary>
public record DriverDeliveredRequest(string? ShopCode, bool CashCollected, double? Latitude, double? Longitude);

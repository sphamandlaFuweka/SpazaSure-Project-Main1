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
/// needed). It lists their deliveries and shares the phone's location while they drive, which
/// the shop sees on its tracking map.
/// </summary>
[ApiController]
[AllowAnonymous]
public class DriverPortalController(SpazaSureDbContext db) : ControllerBase
{
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
        return Ok(ApiResponse<object>.Ok(new
        {
            driver.FullName, driver.BadgeCode, driver.VehicleRegistration,
            Jobs = jobs.Select(o => new
            {
                o.OrderNumber, o.Status,
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
        }));
    }

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

    [HttpGet("driver/{token}")]
    public async Task<ContentResult> Page(string token)
    {
        var driver = await FindAsync(token);
        var body = driver is null
            ? "<h1>Link not working</h1><p>This driver link has been replaced or switched off. Ask SpazaSure for a new one.</p>"
            : DriverPageBody(driver, token);
        return Content(
            "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta name=\"robots\" content=\"noindex\">" +
            "<title>SpazaSure driver</title><style>" + Css + "</style></head><body><main>" + body + "</main></body></html>",
            "text/html; charset=utf-8");
    }

    private static string DriverPageBody(Driver driver, string token)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
        return $$"""
            <header><h1>Hi {{E(driver.FullName)}}</h1>
            <p>Badge <b>{{E(driver.BadgeCode)}}</b> &middot; {{E(driver.VehicleRegistration)}}</p></header>
            <section class="share">
              <button id="share">Start sharing my location</button>
              <p id="status">Shops can follow you on the map while you share. Keep this page open while you drive.</p>
            </section>
            <h2>Your deliveries</h2>
            <div id="jobs"><p>Loading...</p></div>
            <script>
            const token = {{System.Text.Json.JsonSerializer.Serialize(token)}};
            const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
            const maps = (lat, lng, address) => lat != null && lng != null
              ? `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}`
              : `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(address || '')}`;
            async function loadJobs() {
              const res = await fetch(`/api/delivery/driver/${token}/jobs`);
              const box = document.getElementById('jobs');
              if (!res.ok) { box.innerHTML = '<p>Could not load your deliveries.</p>'; return; }
              const data = (await res.json()).data;
              box.innerHTML = data.jobs.length === 0 ? '<p>No deliveries allocated to you right now.</p>' : data.jobs.map(j => `
                <article>
                  <h3>${esc(j.orderNumber)} <span>${esc(j.status)}</span></h3>
                  <p><b>Collect from</b><br>${esc(j.pickup.name)}<br>${esc(j.pickup.address)}
                    <br><a href="${maps(j.pickup.latitude, j.pickup.longitude, j.pickup.address)}" target="_blank" rel="noopener">Directions</a></p>
                  <p><b>Deliver to</b><br>${esc(j.dropoff.shopName)} &middot; <a href="tel:${esc(j.dropoff.phone)}">${esc(j.dropoff.phone)}</a><br>${esc(j.dropoff.address)}
                    <br><a href="${maps(j.dropoff.latitude, j.dropoff.longitude, j.dropoff.address)}" target="_blank" rel="noopener">Directions</a></p>
                  ${j.cashOnDelivery ? `<p class="cash">Cash on delivery: collect R${Number(j.totalAmount).toFixed(2)} and ask the shop for its delivery code.</p>` : ''}
                </article>`).join('');
            }
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
            loadJobs(); setInterval(loadJobs, 60000);
            </script>
            """;
    }

    private const string Css =
        "body{font-family:system-ui,sans-serif;background:#F5F7FF;color:#17244F;margin:0}main{max-width:560px;margin:0 auto;padding:16px}" +
        "header{background:#25449A;color:#fff;border-radius:16px;padding:16px}header h1{margin:0 0 4px;font-size:20px}header p{margin:0;opacity:.9}" +
        ".share{background:#fff;border-radius:16px;padding:16px;margin:12px 0}button{width:100%;padding:14px;border:0;border-radius:12px;" +
        "background:#F8B217;color:#17244F;font-weight:800;font-size:16px}#status{font-size:13px;color:#5A5A7A}" +
        "article{background:#fff;border-radius:16px;padding:14px;margin-bottom:10px}article h3{margin:0 0 8px;font-size:16px}" +
        "article h3 span{font-size:11px;background:#E8ECF8;border-radius:99px;padding:2px 8px;text-transform:capitalize}" +
        "a{color:#25449A;font-weight:700}.cash{background:#FEF5E0;border-radius:10px;padding:8px}h2{font-size:16px}";
}

public record DriverLocationRequest(double Latitude, double Longitude);

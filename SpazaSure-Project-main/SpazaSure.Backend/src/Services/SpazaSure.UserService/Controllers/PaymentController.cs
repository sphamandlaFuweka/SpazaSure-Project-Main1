using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Models;

namespace SpazaSure.UserService.Controllers;

/// <summary>
/// Handles PayFast payment initiation and ITN (Instant Transaction Notification) callbacks.
/// PayFast flow:
///   1. Frontend calls POST /api/supplier/subscription/subscribe  gets pending subscription
///   2. Frontend calls POST /api/supplier/payment/initiate  gets PayFast form data + signature
///   3. Frontend redirects user to PayFast OR renders onsite payment modal
///   4. PayFast posts ITN callback to POST /api/supplier/payment/notify
///   5. Backend verifies signature, activates subscription, updates tier
/// </summary>
[ApiController]
[Route("api/supplier/payment")]
public class PaymentController(SpazaSureDbContext db, IConfiguration config) : ControllerBase
{
    // PayFast sandbox/live credentials from appsettings
    private string MerchantId => config["PayFast:MerchantId"] ?? "10000100";
    private string MerchantKey => config["PayFast:MerchantKey"] ?? "46f0cd694581a";
    private string Passphrase => config["PayFast:Passphrase"] ?? "SpazaSure2024";
    private string BaseUrl => config["PayFast:BaseUrl"] ?? "https://sandbox.payfast.co.za/eng/process";
    private string ReturnUrl => config["PayFast:ReturnUrl"] ?? "http://localhost:5173/subscription?payment=success";
    private string CancelUrl => config["PayFast:CancelUrl"] ?? "http://localhost:5173/subscription?payment=cancelled";
    private string NotifyUrl => config["PayFast:NotifyUrl"] ?? "http://localhost:5181/api/supplier/payment/notify";
    private bool IsSandbox => config["PayFast:Sandbox"]?.ToLower() != "false";

    //  POST /initiate  Generate PayFast payment data for frontend 
    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiatePaymentRequest req)
    {
        if (req.SubscriptionId == Guid.Empty)
            return BadRequest(ApiResponse.Fail("Subscription ID is required."));

        var sub = await db.SupplierSubscriptions
            .Include(s => s.Plan)
            .Include(s => s.Supplier)
            .FirstOrDefaultAsync(s => s.Id == req.SubscriptionId);

        if (sub is null)
            return NotFound(ApiResponse.Fail("Subscription not found."));

        var amount = sub.BillingCycle == "annual" ? sub.Plan.AnnualPrice : sub.Plan.MonthlyPrice;
        var itemName = $"SpazaSure {sub.Plan.Name} Plan ({sub.BillingCycle})";

        // Build PayFast parameter dictionary (order matters for signature!)
        var paymentData = new Dictionary<string, string>
        {
            ["merchant_id"] = MerchantId,
            ["merchant_key"] = MerchantKey,
            ["return_url"] = ReturnUrl,
            ["cancel_url"] = CancelUrl,
            ["notify_url"] = NotifyUrl,
            ["name_first"] = sub.Supplier.ContactPerson?.Split(' ').FirstOrDefault() ?? "Supplier",
            ["email_address"] = sub.Supplier.Email ?? "",
            ["m_payment_id"] = sub.Id.ToString(),
            ["amount"] = amount.ToString("F2"),
            ["item_name"] = itemName,
            ["item_description"] = $"{sub.Plan.Name} tier subscription - {sub.Plan.CommissionRate}% commission",
            // Subscription parameters for recurring billing
            ["subscription_type"] = "1",
            ["billing_date"] = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"),
            ["recurring_amount"] = (sub.BillingCycle == "annual" ? sub.Plan.AnnualPrice : sub.Plan.MonthlyPrice).ToString("F2"),
            ["frequency"] = sub.BillingCycle == "annual" ? "6" : "3", // 3=monthly, 6=annual
            ["cycles"] = "0", // 0 = indefinite
            ["custom_str1"] = sub.SupplierId.ToString(),
            ["custom_str2"] = sub.Plan.Tier,
        };

        // Generate signature
        var signature = GenerateSignature(paymentData);
        paymentData["signature"] = signature;

        // Update subscription with payment reference
        sub.PaymentReference = $"PF-{sub.Id.ToString()[..8].ToUpper()}";
        await db.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new
        {
            payFastUrl = BaseUrl,
            paymentData,
            signature,
            subscriptionId = sub.Id,
            amount,
            itemName,
            isSandbox = IsSandbox,
            paymentReference = sub.PaymentReference
        }));
    }

    //  POST /notify  PayFast ITN callback (server-to-server) 
    [HttpPost("notify")]
    public async Task<IActionResult> Notify()
    {
        // Read the form data from PayFast
        var formData = new Dictionary<string, string>();
        foreach (var key in Request.Form.Keys)
        {
            formData[key] = Request.Form[key].ToString();
        }

        // Log the notification
        Console.WriteLine($"[PayFast ITN] Received: payment_status={formData.GetValueOrDefault("payment_status")}, m_payment_id={formData.GetValueOrDefault("m_payment_id")}");

        // 1. Verify signature
        var receivedSignature = formData.GetValueOrDefault("signature") ?? "";
        var dataForSig = formData
            .Where(kv => kv.Key != "signature")
            .OrderBy(kv => kv.Key)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var calculatedSignature = GenerateSignature(dataForSig);
        if (calculatedSignature != receivedSignature)
        {
            Console.WriteLine("[PayFast ITN] Signature mismatch! Ignoring.");
            return Ok(); // Return 200 but don't process
        }

        // 2. Check payment status
        var paymentStatus = formData.GetValueOrDefault("payment_status") ?? "";
        var paymentId = formData.GetValueOrDefault("m_payment_id") ?? "";

        if (!Guid.TryParse(paymentId, out var subscriptionId))
        {
            Console.WriteLine($"[PayFast ITN] Invalid m_payment_id: {paymentId}");
            return Ok();
        }

        var sub = await db.SupplierSubscriptions
            .Include(s => s.Supplier)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (sub is null)
        {
            Console.WriteLine($"[PayFast ITN] Subscription not found: {subscriptionId}");
            return Ok();
        }

        if (paymentStatus == "COMPLETE")
        {
            // Payment successful  activate subscription
            sub.Status = "active";
            sub.PaymentReference = formData.GetValueOrDefault("pf_payment_id") ?? sub.PaymentReference;
            sub.PaymentMethod = "payfast";
            sub.AmountPaid = decimal.TryParse(formData.GetValueOrDefault("amount_gross"), out var amt) ? amt : sub.AmountPaid;

            // Update supplier tier
            sub.Supplier.Tier = sub.Plan.Tier;
            sub.Supplier.CommissionRate = sub.Plan.CommissionRate;

            await db.SaveChangesAsync();
            Console.WriteLine($"[PayFast ITN] Subscription {subscriptionId} ACTIVATED  {sub.Plan.Tier} tier");
        }
        else if (paymentStatus == "CANCELLED")
        {
            sub.Status = "cancelled";
            sub.CancelledReason = "Payment cancelled by user";
            await db.SaveChangesAsync();
            Console.WriteLine($"[PayFast ITN] Subscription {subscriptionId} CANCELLED");
        }

        return Ok();
    }

    //  GET /status/:subscriptionId  Check payment status 
    [HttpGet("status/{subscriptionId:guid}")]
    public async Task<IActionResult> GetStatus(Guid subscriptionId)
    {
        var sub = await db.SupplierSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId);

        if (sub is null) return NotFound(ApiResponse.Fail("Subscription not found."));

        return Ok(ApiResponse<object>.Ok(new
        {
            subscriptionId = sub.Id,
            status = sub.Status,
            tier = sub.Plan.Tier,
            paymentMethod = sub.PaymentMethod,
            paymentReference = sub.PaymentReference,
            amountPaid = sub.AmountPaid
        }));
    }

    //  Helper: Generate MD5 signature 
    private string GenerateSignature(Dictionary<string, string> data)
    {
        var paramString = string.Join("&",
            data.Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value.Trim())}"));

        if (!string.IsNullOrEmpty(Passphrase))
            paramString += $"&passphrase={Uri.EscapeDataString(Passphrase.Trim())}";

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(paramString));
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}

public record InitiatePaymentRequest(Guid SubscriptionId);
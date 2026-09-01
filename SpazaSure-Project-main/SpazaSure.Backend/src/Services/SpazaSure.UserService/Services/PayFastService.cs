using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SpazaSure.UserService.Services;

/// <summary>
/// PayFast payment integration service for subscription billing.
/// Handles payment form generation, signature calculation, and ITN validation.
/// 
/// PayFast Flow:
/// 1. Frontend calls our backend to get payment form data
/// 2. Frontend submits hidden form to PayFast (redirects user to PayFast checkout)
/// 3. User pays on PayFast (card, EFT, SnapScan)
/// 4. PayFast calls our ITN (webhook) URL with the result
/// 5. We validate the ITN and activate the subscription
/// </summary>
public class PayFastService
{
    private readonly IConfiguration _config;
    private readonly ILogger<PayFastService> _logger;
    private readonly bool _sandbox;
    private readonly string _merchantId;
    private readonly string _merchantKey;
    private readonly string _passphrase;
    private readonly string _returnUrl;
    private readonly string _cancelUrl;
    private readonly string _notifyUrl;

    public PayFastService(IConfiguration config, ILogger<PayFastService> logger)
    {
        _config = config;
        _logger = logger;

        _sandbox = config.GetValue<bool>("PayFast:Sandbox", true);
        _merchantId = config["PayFast:MerchantId"] ?? "10000100"; // sandbox default
        _merchantKey = config["PayFast:MerchantKey"] ?? "46f0cd694581a";  // sandbox default
        _passphrase = config["PayFast:Passphrase"] ?? "";
        _returnUrl = config["PayFast:ReturnUrl"] ?? "http://localhost:5173/subscription?payment=success";
        _cancelUrl = config["PayFast:CancelUrl"] ?? "http://localhost:5173/subscription?payment=cancelled";
        _notifyUrl = config["PayFast:NotifyUrl"] ?? "http://localhost:5005/api/supplier/subscription/payfast-notify";
    }

    public string BaseUrl => _sandbox
        ? "https://sandbox.payfast.co.za/eng/process"
        : "https://www.payfast.co.za/eng/process";

    public string ValidateUrl => _sandbox
        ? "https://sandbox.payfast.co.za/eng/query/validate"
        : "https://www.payfast.co.za/eng/query/validate";

    /// <summary>
    /// Generate PayFast payment form data for a subscription purchase.
    /// The frontend renders a hidden form with these fields and auto-submits it.
    /// </summary>
    public PayFastFormData GeneratePaymentForm(PayFastPaymentRequest request)
    {
        var data = new SortedDictionary<string, string>
        {
            // Merchant details
            ["merchant_id"] = _merchantId,
            ["merchant_key"] = _merchantKey,

            // URLs
            ["return_url"] = _returnUrl,
            ["cancel_url"] = _cancelUrl,
            ["notify_url"] = _notifyUrl,

            // Buyer details
            ["email_address"] = request.Email,
            ["name_first"] = request.FirstName ?? "",

            // Transaction details
            ["m_payment_id"] = request.PaymentId, // Our internal reference (subscription ID)
            ["amount"] = request.Amount.ToString("F2"),
            ["item_name"] = request.ItemName,
            ["item_description"] = request.ItemDescription ?? "",

            // Custom fields (we use these in the ITN callback)
            ["custom_str1"] = request.SupplierId.ToString(),
            ["custom_str2"] = request.SubscriptionId.ToString(),
            ["custom_str3"] = request.Tier,
            ["custom_int1"] = request.BillingCycle == "annual" ? "12" : "1",
        };

        // For recurring subscriptions (not one-time)
        if (request.IsRecurring)
        {
            data["subscription_type"] = "1"; // 1 = subscription
            data["billing_date"] = DateTime.UtcNow.ToString("yyyy-MM-dd");
            data["recurring_amount"] = request.RecurringAmount.ToString("F2");
            data["frequency"] = request.BillingCycle == "annual" ? "6" : "3"; // 3=monthly, 6=annual
            data["cycles"] = "0"; // 0 = indefinite
        }

        // Remove empty values
        var filtered = data.Where(kv => !string.IsNullOrEmpty(kv.Value))
                          .ToDictionary(kv => kv.Key, kv => kv.Value);

        // Generate signature
        var signatureString = string.Join("&",
            filtered.Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}"));

        if (!string.IsNullOrEmpty(_passphrase))
            signatureString += $"&passphrase={WebUtility.UrlEncode(_passphrase)}";

        var signature = ComputeMd5(signatureString);
        filtered["signature"] = signature;

        _logger.LogInformation("Generated PayFast form for subscription {SubscriptionId}, amount R{Amount}",
            request.SubscriptionId, request.Amount);

        return new PayFastFormData
        {
            ActionUrl = BaseUrl,
            Fields = filtered
        };
    }

    /// <summary>
    /// Validate an ITN (Instant Transaction Notification) from PayFast.
    /// Returns true if the notification is authentic and payment was successful.
    /// </summary>
    public async Task<bool> ValidateItnAsync(Dictionary<string, string> formData, string? sourceIp)
    {
        // 1. Verify the signature
        var dataForSignature = formData
            .Where(kv => kv.Key != "signature" && !string.IsNullOrEmpty(kv.Value))
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}");

        var signatureString = string.Join("&", dataForSignature);
        if (!string.IsNullOrEmpty(_passphrase))
            signatureString += $"&passphrase={WebUtility.UrlEncode(_passphrase)}";

        var calculatedSig = ComputeMd5(signatureString);
        if (calculatedSig != formData.GetValueOrDefault("signature"))
        {
            _logger.LogWarning("ITN signature mismatch for payment {PaymentId}", formData.GetValueOrDefault("m_payment_id"));
            return false;
        }

        // 2. Verify the source IP is from PayFast
        var validIps = new[]
        {
            "197.97.145.144", "197.97.145.145", "197.97.145.146", "197.97.145.147",
            "197.97.145.148", "197.97.145.149", "197.97.145.150", "197.97.145.151",
            "41.74.179.194", // sandbox
        };
        if (!_sandbox && sourceIp != null && !validIps.Contains(sourceIp))
        {
            _logger.LogWarning("ITN from invalid IP: {Ip}", sourceIp);
            return false;
        }

        // 3. Verify with PayFast server (POST back to confirm)
        try
        {
            using var client = new HttpClient();
            var postData = string.Join("&",
                formData.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

            var response = await client.PostAsync(ValidateUrl,
                new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded"));

            var result = await response.Content.ReadAsStringAsync();
            if (result.Trim() != "VALID")
            {
                _logger.LogWarning("ITN server validation failed: {Result}", result);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate ITN with PayFast server");
            // In sandbox mode, we can skip this check
            if (!_sandbox) return false;
        }

        // 4. Verify payment_status
        var status = formData.GetValueOrDefault("payment_status");
        if (status != "COMPLETE")
        {
            _logger.LogInformation("ITN payment status is {Status}, not COMPLETE", status);
            return false;
        }

        return true;
    }

    private static string ComputeMd5(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

//  DTOs 

public class PayFastPaymentRequest
{
    public string Email { get; set; } = "";
    public string? FirstName { get; set; }
    public string PaymentId { get; set; } = "";       // Our m_payment_id
    public decimal Amount { get; set; }
    public string ItemName { get; set; } = "";
    public string? ItemDescription { get; set; }
    public Guid SupplierId { get; set; }
    public Guid SubscriptionId { get; set; }
    public string Tier { get; set; } = "";
    public string BillingCycle { get; set; } = "monthly";
    public bool IsRecurring { get; set; } = true;
    public decimal RecurringAmount { get; set; }
}

public class PayFastFormData
{
    public string ActionUrl { get; set; } = "";
    public Dictionary<string, string> Fields { get; set; } = new();
}
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SpazaSure.UserService.Services;

public sealed record OnboardingPaymentForm(
    string ActionUrl,
    Dictionary<string, string> Fields,
    string ReturnUrl,
    string CancelUrl);

public sealed class OnboardingPayFastService(
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<OnboardingPayFastService> logger)
{
    private readonly bool _sandbox = config.GetValue("PayFast:Sandbox", true);
    private readonly string _merchantId = config["PayFast:MerchantId"] ?? string.Empty;
    private readonly string _merchantKey = config["PayFast:MerchantKey"] ?? string.Empty;
    private readonly string _passphrase = config["PayFast:Passphrase"] ?? string.Empty;
    private readonly string _returnUrl = config["PayFast:OnboardingReturnUrl"]
        ?? "http://localhost:5181/api/shop/profile/onboarding-fee/return";
    private readonly string _cancelUrl = config["PayFast:OnboardingCancelUrl"]
        ?? "http://localhost:5181/api/shop/profile/onboarding-fee/cancel";
    private readonly string _notifyUrl = config["PayFast:OnboardingNotifyUrl"]
        ?? "http://localhost:5181/api/shop/profile/onboarding-fee/notify";

    public OnboardingPaymentForm Generate(
        Guid paymentId,
        decimal amount,
        string email,
        string firstName)
    {
        EnsureConfigured();
        var fields = new Dictionary<string, string>
        {
            ["merchant_id"] = _merchantId,
            ["merchant_key"] = _merchantKey,
            ["return_url"] = _returnUrl,
            ["cancel_url"] = _cancelUrl,
            ["notify_url"] = _notifyUrl,
            ["name_first"] = firstName,
            ["email_address"] = email,
            ["m_payment_id"] = paymentId.ToString(),
            ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["item_name"] = "SpazaSure shop onboarding fee",
            ["item_description"] = "Once-off SpazaSure shop onboarding fee"
        };
        fields["signature"] = Sign(fields);
        return new OnboardingPaymentForm(ProcessUrl, fields, _returnUrl, _cancelUrl);
    }
    public async Task<bool> ValidateItnAsync(IReadOnlyDictionary<string, string> formData)
    {
        EnsureConfigured();
        if (!string.Equals(formData.GetValueOrDefault("merchant_id"), _merchantId, StringComparison.Ordinal))
            return false;
        var receivedSignature = formData.GetValueOrDefault("signature");
        if (string.IsNullOrWhiteSpace(receivedSignature)) return false;

        var unsigned = formData
            .Where(pair => pair.Key != "signature" && !string.IsNullOrEmpty(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var calculated = Sign(unsigned);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(calculated), Encoding.ASCII.GetBytes(receivedSignature)))
        {
            logger.LogWarning("PayFast onboarding ITN signature mismatch for {PaymentId}",
                formData.GetValueOrDefault("m_payment_id"));
            return false;
        }

        try
        {
            using var content = new FormUrlEncodedContent(formData);
            var response = await httpClientFactory.CreateClient(nameof(OnboardingPayFastService))
                .PostAsync(ValidateUrl, content);
            var result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode || !string.Equals(result.Trim(), "VALID", StringComparison.Ordinal))
            {
                logger.LogWarning("PayFast onboarding ITN server validation failed for {PaymentId}",
                    formData.GetValueOrDefault("m_payment_id"));
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PayFast onboarding ITN validation request failed");
            return false;
        }

        return string.Equals(formData.GetValueOrDefault("payment_status"), "COMPLETE", StringComparison.Ordinal);
    }

    private string ProcessUrl => _sandbox
        ? "https://sandbox.payfast.co.za/eng/process"
        : "https://www.payfast.co.za/eng/process";

    private string ValidateUrl => _sandbox
        ? "https://sandbox.payfast.co.za/eng/query/validate"
        : "https://www.payfast.co.za/eng/query/validate";

    private string Sign(IReadOnlyDictionary<string, string> fields)
    {
        var data = string.Join("&", fields
            .Where(pair => pair.Key != "signature" && !string.IsNullOrEmpty(pair.Value))
            .Select(pair => $"{pair.Key}={Encode(pair.Value)}"));
        if (!string.IsNullOrEmpty(_passphrase)) data += $"&passphrase={Encode(_passphrase)}";
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private static string Encode(string value) => WebUtility.UrlEncode(value).Replace("%20", "+");

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_merchantId) || string.IsNullOrWhiteSpace(_merchantKey))
            throw new InvalidOperationException("PayFast merchant configuration is missing.");
        if (!_sandbox && (!_returnUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || !_cancelUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || !_notifyUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Live PayFast callback URLs must use HTTPS.");
    }
}

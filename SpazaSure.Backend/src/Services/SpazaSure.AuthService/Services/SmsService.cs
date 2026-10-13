using System.Net.Http.Headers;
using System.Text.Json;

namespace SpazaSure.AuthService.Services;

public interface ISmsService
{
    Task<bool> SendOtpAsync(string phone, string otp);
}

public class SmsService(IConfiguration config, ILogger<SmsService> logger, IHttpClientFactory httpFactory) : ISmsService
{
    private readonly string _username = config["AfricasTalking:Username"]!;
    private readonly string _apiKey   = config["AfricasTalking:ApiKey"]!;
    private readonly bool   _sandbox  = bool.Parse(config["AfricasTalking:Sandbox"] ?? "true");
    private readonly string? _senderId = config["AfricasTalking:SenderId"];

    // Sandbox vs production endpoints
    private string ApiUrl => _sandbox
        ? "https://api.sandbox.africastalking.com/version1/messaging"
        : "https://api.africastalking.com/version1/messaging";

    public async Task<bool> SendOtpAsync(string phone, string otp)
    {
        try
        {
            var recipient = NormalizePhone(phone);
            if (recipient is null)
            {
                logger.LogWarning("AT SMS skipped because the phone number is not a valid South African number.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_apiKey))
            {
                logger.LogError("AT SMS is not configured: username or API key is missing.");
                return false;
            }

            var message = $"Your SpazaSure verification code is: {otp}. Valid for 10 minutes. Do not share this code.";

            var client = httpFactory.CreateClient("AfricasTalking");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("apiKey", _apiKey);

            var form = new Dictionary<string, string>
            {
                ["username"] = _username,
                ["to"]       = recipient,
                ["message"]  = message,
            };
            if (!string.IsNullOrWhiteSpace(_senderId))
                form["from"] = _senderId;

            using var content = new FormUrlEncodedContent(form);

            using var response = await client.PostAsync(ApiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("AT SMS request failed with HTTP status {Status}.", response.StatusCode);
                return false;
            }

            var body = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("SMSMessageData", out var messageData) ||
                !messageData.TryGetProperty("Recipients", out var recipients) ||
                recipients.ValueKind != JsonValueKind.Array)
            {
                logger.LogError("AT SMS response did not contain recipient delivery status.");
                return false;
            }

            var statuses = recipients.EnumerateArray()
                .Select(item => item.TryGetProperty("status", out var status)
                    ? status.GetString()
                    : null)
                .ToArray();
            if (statuses.Length == 0 || statuses.Any(status =>
                !string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, "Queued", StringComparison.OrdinalIgnoreCase)))
            {
                var providerStatus = string.Join(", ", statuses.Select(status => status ?? "unknown"));
                logger.LogError("AT SMS rejected recipient request. Provider status: {ProviderStatus}.", providerStatus);
                return false;
            }

            logger.LogInformation("AT SMS provider accepted the recipient request.");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AT SMS delivery request failed.");
            return false;
        }
    }

    private static string? NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0027", StringComparison.Ordinal))
            digits = digits[2..];
        else if (digits.StartsWith('0'))
            digits = $"27{digits[1..]}";
        else if (!digits.StartsWith("27", StringComparison.Ordinal) && digits.Length == 9)
            digits = $"27{digits}";

        if (!digits.StartsWith("27", StringComparison.Ordinal) ||
            digits.Length != 11 ||
            digits[2] is not ('6' or '7' or '8'))
            return null;

        return $"+{digits}";
    }
}

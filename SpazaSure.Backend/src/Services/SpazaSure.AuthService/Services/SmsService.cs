using System.Net.Http.Headers;
using System.Text.Json;

namespace SpazaSure.AuthService.Services;

public class SmsService(IConfiguration config, ILogger<SmsService> logger, IHttpClientFactory httpFactory)
{
    private readonly string _username = config["AfricasTalking:Username"]!;
    private readonly string _apiKey   = config["AfricasTalking:ApiKey"]!;
    private readonly bool   _sandbox  = bool.Parse(config["AfricasTalking:Sandbox"] ?? "true");
    private readonly string _senderId = config["AfricasTalking:SenderId"] ?? "SpazaSure";

    // Sandbox vs production endpoints
    private string ApiUrl => _sandbox
        ? "https://api.sandbox.africastalking.com/version1/messaging"
        : "https://api.africastalking.com/version1/messaging";

    public async Task<bool> SendOtpAsync(string phone, string otp)
    {
        try
        {
            var message = $"Your SpazaSure verification code is: {otp}. Valid for 10 minutes. Do not share this code.";

            var client = httpFactory.CreateClient("AfricasTalking");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("apiKey", _apiKey);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = _username,
                ["to"]       = phone,
                ["message"]  = message,
                ["from"]     = _senderId,
            });

            var response = await client.PostAsync(ApiUrl, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("AT SMS failed for {Phone}. Status: {Status}. Body: {Body}",
                    phone, response.StatusCode, body);
                return false;
            }

            // Africa's Talking can return HTTP 200 while reporting a recipient
            // failure in the response body. Treat that as a delivery failure.
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("SMSMessageData", out var messageData) &&
                messageData.TryGetProperty("Recipients", out var recipients) &&
                recipients.ValueKind == JsonValueKind.Array)
            {
                var statuses = recipients.EnumerateArray()
                    .Select(recipient => recipient.TryGetProperty("status", out var status)
                        ? status.GetString()
                        : null)
                    .ToArray();

                if (statuses.Length == 0 || statuses.Any(status =>
                    !string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(status, "Queued", StringComparison.OrdinalIgnoreCase)))
                {
                    logger.LogError("AT SMS rejected for {Phone}. Body: {Body}", phone, body);
                    return false;
                }
            }

            logger.LogInformation("AT SMS sent to {Phone}. Response: {Body}", phone, body);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMS delivery exception for {Phone}", phone);
            return false;
        }
    }
}

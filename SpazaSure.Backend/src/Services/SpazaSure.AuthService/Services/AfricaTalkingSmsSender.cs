using System.Net.Http.Headers;
using System.Text.Json;

namespace SpazaSure.AuthService.Services;

public class AfricaTalkingSmsSender(HttpClient httpClient, IConfiguration config) : ISmsSender
{
    private const string MessagingEndpoint = "https://api.africastalking.com/version1/messaging";

    public async Task SendAsync(
        string phone, string message, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, MessagingEndpoint);
        request.Headers.Add("apiKey", config["AfricaTalking:ApiKey"]);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var fields = new Dictionary<string, string>
        {
            ["username"] = config["AfricaTalking:Username"]!,
            ["to"] = phone,
            ["message"] = message
        };
        var senderId = config["AfricaTalking:SenderId"];
        if (!string.IsNullOrWhiteSpace(senderId)) fields["from"] = senderId;
        request.Content = new FormUrlEncodedContent(fields);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new SmsDeliveryException("Africa's Talking request failed.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SmsDeliveryException("Africa's Talking request timed out.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new SmsDeliveryException($"Africa's Talking returned HTTP {(int)response.StatusCode}.");

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var recipients = document.RootElement
                    .GetProperty("SMSMessageData")
                    .GetProperty("Recipients");

                if (recipients.ValueKind != JsonValueKind.Array || recipients.GetArrayLength() != 1)
                    throw new SmsDeliveryException("Africa's Talking returned an unexpected recipient response.");

                var statusElement = recipients[0].GetProperty("statusCode");
                var status = statusElement.ValueKind switch
                {
                    JsonValueKind.String => statusElement.GetString(),
                    JsonValueKind.Number => statusElement.GetRawText(),
                    _ => null
                };
                if (status is not ("100" or "101" or "102"))
                    throw new SmsDeliveryException("Africa's Talking did not accept the verification SMS.");
            }
            catch (JsonException ex)
            {
                throw new SmsDeliveryException("Africa's Talking returned an invalid response.", ex);
            }
            catch (KeyNotFoundException ex)
            {
                throw new SmsDeliveryException("Africa's Talking returned an incomplete response.", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new SmsDeliveryException("Africa's Talking returned an unexpected response.", ex);
            }
        }
    }
}

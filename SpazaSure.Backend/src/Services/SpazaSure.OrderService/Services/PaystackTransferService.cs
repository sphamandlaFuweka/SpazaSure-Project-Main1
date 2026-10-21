using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SpazaSure.OrderService.Services;

/// <summary>
/// Paystack Transfers: pays suppliers from SpazaSure's Paystack balance into their South African
/// bank account (recipient type "basa"; a bank is identified by its universal branch code).
/// </summary>
public sealed class PaystackTransferService(HttpClient httpClient, IConfiguration config)
{
    private const string ApiBase = "https://api.paystack.co/";
    private readonly string _secretKey = config["Paystack:SecretKey"] ?? string.Empty;

    /// <summary>
    /// Automatic payouts run only when Paystack:AutomaticPayouts is true, i.e. once Transfers are
    /// enabled on the Paystack account and OTP for transfers is turned off.
    /// </summary>
    public bool AutomaticPayoutsEnabled =>
        !string.IsNullOrWhiteSpace(_secretKey) &&
        bool.TryParse(config["Paystack:AutomaticPayouts"], out var enabled) && enabled;

    public sealed record Bank(string Name, string Code);
    public sealed record Transfer(string Status, string? TransferCode);

    private static List<Bank>? _banks;
    private static DateTime _banksLoadedAt;

    /// <summary>South African banks Paystack can pay into, cached for half a day.</summary>
    public async Task<IReadOnlyList<Bank>> ListBanksAsync(CancellationToken ct = default)
    {
        if (_banks is not null && DateTime.UtcNow - _banksLoadedAt < TimeSpan.FromHours(12)) return _banks;
        using var json = await SendAsync(HttpMethod.Get, "bank?currency=ZAR&type=basa&perPage=100", null, ct);
        var banks = new List<Bank>();
        foreach (var bank in json.RootElement.GetProperty("data").EnumerateArray())
        {
            var name = bank.TryGetProperty("name", out var n) ? n.GetString() : null;
            var code = bank.TryGetProperty("code", out var c) ? c.GetString() : null;
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(code)) banks.Add(new Bank(name!, code!));
        }
        _banks = banks.OrderBy(b => b.Name).ToList();
        _banksLoadedAt = DateTime.UtcNow;
        return _banks;
    }

    /// <summary>Registers a supplier's bank account with Paystack; returns the recipient code.</summary>
    public async Task<string> CreateRecipientAsync(string name, string accountNumber, string bankCode, CancellationToken ct = default)
    {
        using var json = await SendAsync(HttpMethod.Post, "transferrecipient", new
        {
            type = "basa",
            name,
            account_number = accountNumber,
            bank_code = bankCode,
            currency = "ZAR",
        }, ct);
        return json.RootElement.GetProperty("data").GetProperty("recipient_code").GetString()
            ?? throw new PaystackTransferException("Paystack did not return a recipient code.");
    }

    /// <summary>Sends money from the Paystack balance. Status: pending, success, otp or failed.</summary>
    public async Task<Transfer> InitiateTransferAsync(
        long amountCents, string recipientCode, string reference, string reason, CancellationToken ct = default)
    {
        using var json = await SendAsync(HttpMethod.Post, "transfer", new
        {
            source = "balance",
            amount = amountCents,
            recipient = recipientCode,
            reference,
            reason,
            currency = "ZAR",
        }, ct);
        var data = json.RootElement.GetProperty("data");
        return new Transfer(
            data.TryGetProperty("status", out var s) ? s.GetString() ?? "pending" : "pending",
            data.TryGetProperty("transfer_code", out var t) ? t.GetString() : null);
    }

    /// <summary>The current status of a transfer: pending, success, failed, reversed or otp.</summary>
    public async Task<string> VerifyTransferAsync(string reference, CancellationToken ct = default)
    {
        using var json = await SendAsync(HttpMethod.Get, $"transfer/verify/{Uri.EscapeDataString(reference)}", null, ct);
        return json.RootElement.GetProperty("data").GetProperty("status").GetString() ?? "pending";
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_secretKey)) throw new PaystackTransferException("Paystack is not configured.");
        using var request = new HttpRequestMessage(method, ApiBase + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await httpClient.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        JsonDocument json;
        try { json = JsonDocument.Parse(text); }
        catch (JsonException) { throw new PaystackTransferException($"Paystack returned {(int)response.StatusCode}."); }
        var ok = response.IsSuccessStatusCode &&
                 json.RootElement.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.True;
        if (ok) return json;
        var message = json.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
        json.Dispose();
        throw new PaystackTransferException(message ?? $"Paystack returned {(int)response.StatusCode}.");
    }
}

/// <summary>Paystack refused a transfer request; the message is Paystack's reason.</summary>
public sealed class PaystackTransferException(string message) : Exception(message)
{
    public bool IsInsufficientBalance =>
        Message.Contains("balance", StringComparison.OrdinalIgnoreCase) &&
        Message.Contains("insufficient", StringComparison.OrdinalIgnoreCase);
}

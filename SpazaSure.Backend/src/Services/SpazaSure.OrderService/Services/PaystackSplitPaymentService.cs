using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;

namespace SpazaSure.OrderService.Services;

public sealed class PaystackSplitPaymentService(HttpClient httpClient, IConfiguration config)
{
    private const string ApiBase = "https://api.paystack.co/";
    private readonly string _secretKey = config["Paystack:SecretKey"] ?? string.Empty;

    public async Task<string> CreateSplitAsync(
        IReadOnlyCollection<Order> orders, string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
            throw new InvalidOperationException("Paystack is not configured for order checkout.");
        if (orders.Count == 0)
            throw new InvalidOperationException("An order is required to create a Paystack split.");
        var checkoutTotal = orders.Sum(o => o.TotalAmount);
        var supplierTotal = orders.Sum(o => o.Subtotal);
        if (checkoutTotal <= 0m || orders.Any(o => o.Subtotal < 0m) || supplierTotal > checkoutTotal)
            throw new InvalidOperationException("Supplier shares must be non-negative and cannot exceed the checkout total.");

        var subaccounts = new List<object>();
        foreach (var group in orders.GroupBy(o => o.SupplierId))
        {
            var supplier = group.First().Supplier;
            if (supplier is null || string.IsNullOrWhiteSpace(supplier.BankAccount) ||
                string.IsNullOrWhiteSpace(supplier.BankBranchCode) ||
                SaValidation.Digits(supplier.BankBranchCode).Length != 6 ||
                string.IsNullOrWhiteSpace(SaValidation.Digits(supplier.BankAccount)))
                throw new InvalidOperationException(
                    $"Supplier {group.First().SupplierId} must add valid South African bank details before checkout.");

            var code = supplier.PaystackSubaccountCode;
            if (string.IsNullOrWhiteSpace(code))
            {
                using var subaccount = await SendAsync(HttpMethod.Post, "subaccount", new
                {
                    business_name = supplier.CompanyName,
                    settlement_bank = SaValidation.Digits(supplier.BankBranchCode),
                    account_number = SaValidation.Digits(supplier.BankAccount),
                    percentage_charge = 0,
                    primary_contact_email = supplier.Email,
                    currency = "ZAR"
                }, ct);
                code = ReadRequiredString(subaccount.RootElement, "subaccount_code",
                    "Paystack did not return a supplier subaccount code.");
                supplier.PaystackSubaccountCode = code;
            }

            var share = ToMinorUnits(group.Sum(o => o.Subtotal));
            if (share > 0)
                subaccounts.Add(new { subaccount = code, share });
        }

        if (subaccounts.Count == 0)
            throw new InvalidOperationException("The checkout has no supplier product amounts to split.");

        using var split = await SendAsync(HttpMethod.Post, "split", new
        {
            name = $"SpazaSure {reference}",
            type = "flat",
            currency = "ZAR",
            subaccounts,
            bearer_type = "account"
        }, ct);
        return ReadRequiredString(split.RootElement, "split_code",
            "Paystack did not return a checkout split code.");
    }

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, ApiBase + path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
        using var response = await httpClient.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(responseText);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Paystack returned an invalid response while creating {path}.");
        }

        if (!response.IsSuccessStatusCode ||
            !document.RootElement.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.True)
        {
            var message = document.RootElement.TryGetProperty("message", out var error)
                ? error.GetString()
                : null;
            document.Dispose();
            throw new InvalidOperationException(
                $"Paystack could not create {path}: {message ?? $"HTTP {(int)response.StatusCode}"}");
        }
        return document;
    }

    private static string ReadRequiredString(JsonElement root, string property, string errorMessage)
    {
        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty(property, out var value) &&
            !string.IsNullOrWhiteSpace(value.GetString()))
            return value.GetString()!;
        throw new InvalidOperationException(errorMessage);
    }

    private static long ToMinorUnits(decimal amount) =>
        decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// Admin-configurable platform rules, stored as key/value rows in platform_settings.
/// Every default matches the behaviour the platform had before these became configurable.
/// </summary>
public sealed record PlatformRules
{
    /// <summary>Delivery fee per supplier for "standard" orders (R).</summary>
    public decimal StandardDeliveryFee { get; init; } = 150m;
    /// <summary>Delivery fee per supplier for "express" orders (R). Pickup is always free.</summary>
    public decimal ExpressDeliveryFee { get; init; } = 250m;
    /// <summary>Group buys: base delivery fee (R) plus a rate per km.</summary>
    public decimal GroupBuyBaseDeliveryFee { get; init; } = 50m;
    public decimal GroupBuyPerKmRate { get; init; } = 8m;
    /// <summary>Extra % of the product subtotal charged to the shop, on top of the supplier tier commission. 0 = off.</summary>
    public decimal ShopServiceFeePercent { get; init; } = 0m;
    /// <summary>Days after delivery before a supplier's payout becomes available.</summary>
    public int PayoutDelayDays { get; init; } = 3;
    /// <summary>When on, shops and suppliers see a maintenance message; admins keep working.</summary>
    public bool MaintenanceMode { get; init; } = false;
    public string MaintenanceMessage { get; init; } = DefaultMaintenanceMessage;
    /// <summary>When on, new suppliers stay hidden from shops until an admin verifies them.</summary>
    public bool RequireSupplierApproval { get; init; } = true;
    /// <summary>When on, products from verified suppliers go live without admin review.</summary>
    public bool AutoApproveVerifiedSupplierProducts { get; init; } = false;
    /// <summary>When on, shops can choose to pay in cash on delivery; SpazaSure collects the cash and pays the supplier as usual.</summary>
    public bool CashOnDeliveryEnabled { get; init; } = true;
    /// <summary>Largest checkout total (R) allowed as cash on delivery, to limit cash-handling risk. 0 = no limit.</summary>
    public decimal CashOnDeliveryMaxOrderTotal { get; init; } = 5000m;

    public const string DefaultMaintenanceMessage =
        "SpazaSure is undergoing scheduled maintenance. Please try again shortly.";

    /// <summary>Returns an error message if any value is out of range, otherwise null.</summary>
    public string? Validate()
    {
        if (StandardDeliveryFee is < 0 or > 5000) return "Standard delivery fee must be between R0 and R5,000.";
        if (ExpressDeliveryFee is < 0 or > 5000) return "Express delivery fee must be between R0 and R5,000.";
        if (GroupBuyBaseDeliveryFee is < 0 or > 5000) return "Group-buy base delivery fee must be between R0 and R5,000.";
        if (GroupBuyPerKmRate is < 0 or > 500) return "Group-buy rate per km must be between R0 and R500.";
        if (ShopServiceFeePercent is < 0 or > 20) return "Shop service fee must be between 0% and 20%.";
        if (PayoutDelayDays is < 0 or > 60) return "Payout delay must be between 0 and 60 days.";
        if (CashOnDeliveryMaxOrderTotal is < 0 or > 1000000) return "Cash on delivery limit must be between R0 and R1,000,000.";
        if (string.IsNullOrWhiteSpace(MaintenanceMessage) || MaintenanceMessage.Length > 300)
            return "Maintenance message must be between 1 and 300 characters.";
        return null;
    }
}

public static class PlatformSettingsStore
{
    public const string StandardDeliveryFeeKey = "delivery_fee_standard";
    public const string ExpressDeliveryFeeKey = "delivery_fee_express";
    public const string GroupBuyBaseDeliveryFeeKey = "group_buy_delivery_base_fee";
    public const string GroupBuyPerKmRateKey = "group_buy_delivery_per_km";
    public const string ShopServiceFeePercentKey = "shop_service_fee_percent";
    public const string PayoutDelayDaysKey = "payout_delay_days";
    public const string MaintenanceModeKey = "maintenance_mode";
    public const string MaintenanceMessageKey = "maintenance_message";
    public const string RequireSupplierApprovalKey = "require_supplier_approval";
    public const string AutoApproveProductsKey = "auto_approve_verified_supplier_products";
    public const string CashOnDeliveryEnabledKey = "cash_on_delivery_enabled";
    public const string CashOnDeliveryMaxOrderTotalKey = "cash_on_delivery_max_order_total";

    public static async Task<PlatformRules> LoadAsync(SpazaSureDbContext db, CancellationToken ct = default)
    {
        var values = await db.PlatformSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        var d = new PlatformRules();
        return new PlatformRules
        {
            StandardDeliveryFee = Dec(values, StandardDeliveryFeeKey, d.StandardDeliveryFee),
            ExpressDeliveryFee = Dec(values, ExpressDeliveryFeeKey, d.ExpressDeliveryFee),
            GroupBuyBaseDeliveryFee = Dec(values, GroupBuyBaseDeliveryFeeKey, d.GroupBuyBaseDeliveryFee),
            GroupBuyPerKmRate = Dec(values, GroupBuyPerKmRateKey, d.GroupBuyPerKmRate),
            ShopServiceFeePercent = Dec(values, ShopServiceFeePercentKey, d.ShopServiceFeePercent),
            PayoutDelayDays = values.TryGetValue(PayoutDelayDaysKey, out var days) && int.TryParse(days, out var n) ? n : d.PayoutDelayDays,
            MaintenanceMode = Bool(values, MaintenanceModeKey, d.MaintenanceMode),
            MaintenanceMessage = values.TryGetValue(MaintenanceMessageKey, out var msg) && !string.IsNullOrWhiteSpace(msg) ? msg : d.MaintenanceMessage,
            RequireSupplierApproval = Bool(values, RequireSupplierApprovalKey, d.RequireSupplierApproval),
            AutoApproveVerifiedSupplierProducts = Bool(values, AutoApproveProductsKey, d.AutoApproveVerifiedSupplierProducts),
            CashOnDeliveryEnabled = Bool(values, CashOnDeliveryEnabledKey, d.CashOnDeliveryEnabled),
            CashOnDeliveryMaxOrderTotal = Dec(values, CashOnDeliveryMaxOrderTotalKey, d.CashOnDeliveryMaxOrderTotal),
        };
    }

    public static async Task SaveAsync(SpazaSureDbContext db, PlatformRules rules, CancellationToken ct = default)
    {
        var updates = new Dictionary<string, string>
        {
            [StandardDeliveryFeeKey] = Fmt(rules.StandardDeliveryFee),
            [ExpressDeliveryFeeKey] = Fmt(rules.ExpressDeliveryFee),
            [GroupBuyBaseDeliveryFeeKey] = Fmt(rules.GroupBuyBaseDeliveryFee),
            [GroupBuyPerKmRateKey] = Fmt(rules.GroupBuyPerKmRate),
            [ShopServiceFeePercentKey] = Fmt(rules.ShopServiceFeePercent),
            [PayoutDelayDaysKey] = rules.PayoutDelayDays.ToString(CultureInfo.InvariantCulture),
            [MaintenanceModeKey] = rules.MaintenanceMode ? "true" : "false",
            [MaintenanceMessageKey] = rules.MaintenanceMessage.Trim(),
            [RequireSupplierApprovalKey] = rules.RequireSupplierApproval ? "true" : "false",
            [AutoApproveProductsKey] = rules.AutoApproveVerifiedSupplierProducts ? "true" : "false",
            [CashOnDeliveryEnabledKey] = rules.CashOnDeliveryEnabled ? "true" : "false",
            [CashOnDeliveryMaxOrderTotalKey] = Fmt(rules.CashOnDeliveryMaxOrderTotal),
        };
        var existing = await db.PlatformSettings.Where(s => updates.Keys.Contains(s.Key)).ToListAsync(ct);
        foreach (var (key, value) in updates)
        {
            var row = existing.FirstOrDefault(s => s.Key == key);
            if (row is null) db.PlatformSettings.Add(new PlatformSetting { Key = key, Value = value });
            else { row.Value = value; row.UpdatedAt = DateTime.UtcNow; }
        }
        await db.SaveChangesAsync(ct);
    }

    private static string Fmt(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static decimal Dec(Dictionary<string, string> v, string key, decimal fallback) =>
        v.TryGetValue(key, out var s) && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : fallback;

    private static bool Bool(Dictionary<string, string> v, string key, bool fallback) =>
        v.TryGetValue(key, out var s) && bool.TryParse(s, out var b) ? b : fallback;
}

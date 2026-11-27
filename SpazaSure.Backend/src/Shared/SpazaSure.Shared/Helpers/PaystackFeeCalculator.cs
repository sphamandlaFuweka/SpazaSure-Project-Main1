namespace SpazaSure.Shared.Helpers;

/// <summary>South African Paystack fees confirmed for SpazaSure: 2.9% plus 15% VAT, and R1 plus VAT.</summary>
public static class PaystackFeeCalculator
{
    public const decimal PercentageRate = 0.03335m;
    public const decimal FixedFee = 1.15m;

    public static decimal FeeFor(decimal chargeAmount) =>
        decimal.Round(chargeAmount * PercentageRate + FixedFee, 2, MidpointRounding.AwayFromZero);

    /// <summary>Finds the smallest cent-accurate charge whose net after the Paystack fee meets the target.</summary>
    public static decimal GrossUp(decimal netAmount)
    {
        if (netAmount < 0m)
            throw new ArgumentOutOfRangeException(nameof(netAmount), "The amount cannot be negative.");

        var target = decimal.Round(netAmount, 2, MidpointRounding.AwayFromZero);
        if (target == 0m) return 0m;

        var gross = decimal.Ceiling(
            (target + FixedFee) / (1m - PercentageRate) * 100m) / 100m;
        while (gross - FeeFor(gross) < target)
            gross += 0.01m;
        while (gross >= 0.01m && gross - 0.01m - FeeFor(gross - 0.01m) >= target)
            gross -= 0.01m;
        return gross;
    }

    /// <summary>Accepts the old base-only amount for already-created checkouts and legacy recurring plans.</summary>
    public static bool MatchesPaidAmount(long paidMinorUnits, decimal netAmount)
    {
        var netMinorUnits = ToMinorUnits(netAmount);
        return paidMinorUnits == netMinorUnits ||
               paidMinorUnits == ToMinorUnits(GrossUp(netAmount));
    }

    public static long ToMinorUnits(decimal amount) =>
        decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
}

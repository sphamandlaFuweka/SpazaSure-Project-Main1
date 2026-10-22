using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Entities;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Storage;

namespace SpazaSure.Infrastructure.Data;

/// <summary>
/// POPIA data subject rights: a copy of everything SpazaSure holds about a person (s23), and
/// deleting it on request (s24). Deleting anonymises the account rather than removing rows:
/// orders, payments and wallet records stay, without the person's details, because tax law
/// requires financial records to be kept for five years (POPIA s14(1)(a)).
/// </summary>
public static class PersonalData
{
    public const string DeletedName = "Deleted user";
    private static readonly string[] FinishedOrderStatuses = ["delivered", "completed", "cancelled", "rejected"];

    /// <summary>Everything held about the user, as one JSON-ready object.</summary>
    public static async Task<object> ExportAsync(SpazaSureDbContext db, Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Role).FirstAsync(u => u.Id == userId, ct);
        var shop = await db.SpazaShops.AsNoTracking().Include(s => s.Documents).FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var supplier = await db.Suppliers.AsNoTracking().Include(s => s.Documents).FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var customer = await db.CustomerProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, ct);

        var activity = await db.AuthAuditLogs.AsNoTracking().Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt).Take(500)
            .Select(a => new { a.Event, a.IpAddress, a.CreatedAt }).ToListAsync(ct);
        var messagesSent = await db.ShopMessages.AsNoTracking().Where(m => m.SenderUserId == userId)
            .OrderBy(m => m.CreatedAt).Select(m => new { m.Body, m.CreatedAt }).ToListAsync(ct);

        return new
        {
            exportedAt = DateTime.UtcNow,
            notice = "This is the personal information SpazaSure holds about you. Document links work for 30 minutes.",
            account = new
            {
                user.Id, role = user.Role.Name, user.Email, user.Phone, user.DisplayName, user.Status,
                user.CreatedAt, user.LastLoginAt, profilePhoto = user.ProfilePhotoUrl,
            },
            consents = activity.Where(a => a.Event.StartsWith("consent_")),
            signInActivity = activity.Where(a => !a.Event.StartsWith("consent_")),
            shop = shop is null ? null : await ShopExportAsync(db, shop, ct),
            supplier = supplier is null ? null : await SupplierExportAsync(db, supplier, ct),
            customer = customer is null ? null : await CustomerExportAsync(db, customer, ct),
            messagesSent,
        };
    }

    private static async Task<object> ShopExportAsync(SpazaSureDbContext db, SpazaShop shop, CancellationToken ct) => new
    {
        shop.ShopName, shop.OwnerName, idNumber = shop.OwnerIdNumber, shop.Phone, shop.Email,
        shop.Address, shop.City, shop.Province, shop.PostalCode, shop.Latitude, shop.Longitude,
        registrationDetails = shop.RegistrationDetails, shop.Status, shopNumber = shop.ShopCode,
        identityCheck = new { status = shop.IdentityCheckStatus, updatedAt = shop.IdentityCheckUpdatedAt },
        documents = Documents(shop.Documents.Select(d => (d.DocType, d.Status, (string?)d.DocUrl, d.ExpiryDate, d.RejectionNote, d.CreatedAt))),
        orders = await db.Orders.AsNoTracking().Where(o => o.ShopId == shop.Id).OrderByDescending(o => o.CreatedAt)
            .Select(o => new { o.OrderNumber, o.CreatedAt, o.Status, o.TotalAmount, o.PaymentStatus, o.DeliveryAddress })
            .ToListAsync(ct),
        walletTransactions = await db.ShopWalletTransactions.AsNoTracking().Where(t => t.ShopId == shop.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new { t.CreatedAt, t.Type, t.Amount, t.Status, t.Method, t.Reference, t.BankName, t.AccountHolder, t.AccountNumber })
            .ToListAsync(ct),
    };

    private static async Task<object> SupplierExportAsync(SpazaSureDbContext db, Supplier supplier, CancellationToken ct) => new
    {
        supplier.CompanyName, supplier.RegistrationNumber, supplier.VatNumber, supplier.ContactPerson,
        supplier.Phone, supplier.Email, supplier.Address, supplier.City, supplier.Province, supplier.PostalCode,
        supplier.BankName, supplier.BankAccount, supplier.BankBranchCode, supplier.Status, supplier.Tier,
        businessCheck = new { status = supplier.IdentityCheckStatus, updatedAt = supplier.IdentityCheckUpdatedAt },
        documents = Documents(supplier.Documents.Select(d => (d.DocType, d.Status, (string?)d.DocUrl, d.ExpiryDate, d.RejectionNote, d.CreatedAt))),
        subscriptions = await db.SupplierSubscriptions.AsNoTracking().Where(s => s.SupplierId == supplier.Id)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.CreatedAt, s.Status, s.BillingCycle, s.AmountPaid, s.StartDate, s.EndDate })
            .ToListAsync(ct),
        orders = await db.Orders.AsNoTracking().Where(o => o.SupplierId == supplier.Id).OrderByDescending(o => o.CreatedAt)
            .Select(o => new { o.OrderNumber, o.CreatedAt, o.Status, o.TotalAmount, o.PaymentStatus })
            .ToListAsync(ct),
    };

    private static async Task<object> CustomerExportAsync(SpazaSureDbContext db, CustomerProfile c, CancellationToken ct) => new
    {
        c.FirstName, c.LastName, c.PreferredName, c.Age, c.PreferredLanguage, c.Province, c.City, c.Suburb,
        c.Latitude, c.Longitude, c.Allergies, c.Interests, c.NotificationPreferences, c.MarketingOptIn,
        productScans = await db.CustomerScanEvents.AsNoTracking().Where(s => s.CustomerUserId == c.UserId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.CreatedAt, s.Code, s.Source, s.PointsAwarded, s.Latitude, s.Longitude })
            .ToListAsync(ct),
        rewards = await db.CustomerRewardTransactions.AsNoTracking().Where(r => r.CustomerUserId == c.UserId)
            .OrderByDescending(r => r.CreatedAt).Select(r => new { r.CreatedAt, r.Points, r.Type, r.Reference })
            .ToListAsync(ct),
        vouchers = await db.CustomerVouchers.AsNoTracking().Where(v => v.CustomerUserId == c.UserId)
            .Select(v => new { v.CreatedAt, v.Code, v.Amount, v.Status, v.RedeemedAt }).ToListAsync(ct),
        reports = await db.Reports.AsNoTracking().Where(r => r.ReporterUserId == c.UserId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.CreatedAt, r.ReportType, r.ShopName, r.Description, r.Status, photo = r.PhotoUrl, r.Latitude, r.Longitude })
            .ToListAsync(ct),
        reviews = await db.ShopReviews.AsNoTracking().Where(r => r.ReviewerUserId == c.UserId)
            .Select(r => new { r.CreatedAt, r.Rating, r.Comment }).ToListAsync(ct),
    };

    private static IEnumerable<object> Documents(
        IEnumerable<(string Type, string Status, string? Url, DateOnly? Expiry, string? RejectionNote, DateTime Uploaded)> docs) =>
        docs.Select(d => new { type = d.Type, status = d.Status, link = PrivateFiles.Link(d.Url), expiry = d.Expiry, rejectionNote = d.RejectionNote, uploaded = d.Uploaded });

    /// <summary>
    /// Why the account can't be deleted yet: money or orders still in progress, or a subscription
    /// that would keep charging. Null when it can be.
    /// </summary>
    public static async Task<string?> DeletionBlockerAsync(SpazaSureDbContext db, Guid userId, CancellationToken ct = default)
    {
        var shop = await db.SpazaShops.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (shop is not null)
        {
            if (await ShopWallet.BalanceAsync(db, shop.Id, ct) != 0)
                return "The shop's wallet still has money in it. Withdraw or use the balance first.";
            if (await db.Orders.AnyAsync(o => o.ShopId == shop.Id && !FinishedOrderStatuses.Contains(o.Status), ct))
                return "The shop has orders that aren't finished yet. Wait for them to be delivered or cancelled.";
        }

        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (supplier is not null)
        {
            if (await db.Orders.AnyAsync(o => o.SupplierId == supplier.Id && !FinishedOrderStatuses.Contains(o.Status), ct))
                return "The supplier has orders that aren't finished yet. Deliver or cancel them first.";
            if (await db.Orders.AnyAsync(o => o.SupplierId == supplier.Id && o.Status == "delivered" && o.PaidOutAt == null, ct))
                return "The supplier still has payouts owed for delivered orders. Pay them out first.";
            if (await db.SupplierSubscriptions.AnyAsync(s => s.SupplierId == supplier.Id && s.Status == "active"
                    && s.AmountPaid > 0 && !s.CancelAtPeriodEnd, ct))
                return "The supplier has a paid subscription that renews. Cancel it first.";
        }

        if (await db.CustomerSubscriptions.AnyAsync(s => s.CustomerUserId == userId && s.Status == "active" && !s.CancelAtPeriodEnd, ct))
            return "The customer has a SpazaSure Plus subscription that renews. Cancel it first.";
        return null;
    }

    /// <summary>
    /// Erases the person's details and files and closes the account. Business records (orders,
    /// payments, wallet history) are kept without the personal details. Does not save.
    /// </summary>
    public static async Task AnonymiseAsync(SpazaSureDbContext db, IFileStorageService storage, Guid userId, CancellationToken ct = default)
    {
        var files = new List<string?>();
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        files.Add(user.ProfilePhotoUrl);
        user.Email = null;
        user.Phone = $"deleted-{user.Id:N}"; // unique and can't be used to sign in
        user.PasswordHash = string.Empty;
        user.DisplayName = null;
        user.AdminTitle = null;
        user.MfaSecret = null;
        user.MfaEnabled = false;
        user.ProfilePhotoUrl = null;
        user.Status = "deleted";

        db.RefreshTokens.RemoveRange(db.RefreshTokens.Where(t => t.UserId == userId));
        db.OtpCodes.RemoveRange(db.OtpCodes.Where(o => o.UserId == userId));
        foreach (var message in await db.ShopMessages.Where(m => m.SenderUserId == userId).ToListAsync(ct))
            message.Body = "(message removed)";
        foreach (var log in await db.AuthAuditLogs.Where(a => a.UserId == userId).ToListAsync(ct))
        {
            log.IpAddress = null;
            log.UserAgent = null;
        }

        if (await db.SpazaShops.Include(s => s.Documents).FirstOrDefaultAsync(s => s.UserId == userId, ct) is { } shop)
        {
            files.AddRange(shop.Documents.Select(d => d.DocUrl));
            db.ShopDocuments.RemoveRange(shop.Documents);
            shop.ShopName = "Deleted shop";
            shop.OwnerName = DeletedName;
            shop.OwnerIdNumber = null;
            shop.Phone = string.Empty;
            shop.Email = null;
            shop.Address = string.Empty;
            shop.PostalCode = null;
            shop.Latitude = null;
            shop.Longitude = null;
            shop.RegistrationDetails = null;
            shop.ShopCode = null; // the sticker number no longer looks up this shop
            shop.Status = "deleted";
            IdentityChecks.Reset(shop);
            foreach (var order in await db.Orders.Where(o => o.ShopId == shop.Id).ToListAsync(ct))
            {
                order.DeliveryAddress = null;
                order.DeliveryLatitude = null;
                order.DeliveryLongitude = null;
                order.Notes = null;
            }
            foreach (var t in await db.ShopWalletTransactions.Where(t => t.ShopId == shop.Id).ToListAsync(ct))
            {
                t.AccountHolder = t.AccountHolder is null ? null : DeletedName;
                t.AccountNumber = LastDigits(t.AccountNumber);
            }
        }

        if (await db.Suppliers.Include(s => s.Documents).FirstOrDefaultAsync(s => s.UserId == userId, ct) is { } supplier)
        {
            files.AddRange(supplier.Documents.Select(d => d.DocUrl));
            files.Add(supplier.LogoUrl);
            db.SupplierDocuments.RemoveRange(supplier.Documents);
            supplier.CompanyName = "Deleted supplier";
            supplier.RegistrationNumber = null;
            supplier.VatNumber = null;
            supplier.ContactPerson = DeletedName;
            supplier.Phone = string.Empty;
            supplier.Email = string.Empty;
            supplier.Address = null;
            supplier.PostalCode = null;
            supplier.Latitude = null;
            supplier.Longitude = null;
            supplier.BankName = null;
            supplier.BankAccount = null;
            supplier.BankBranchCode = null;
            supplier.LogoUrl = null;
            supplier.Status = "deleted";
            IdentityChecks.Reset(supplier);
            await db.Products.Where(p => p.SupplierId == supplier.Id)
                .ForEachAsync(p => p.IsAvailable = false, ct);
        }

        if (await db.CustomerProfiles.FirstOrDefaultAsync(c => c.UserId == userId, ct) is { } customer)
        {
            customer.FirstName = "Deleted";
            customer.LastName = "user";
            customer.PreferredName = null;
            customer.Age = null;
            customer.Suburb = null;
            customer.Latitude = null;
            customer.Longitude = null;
            customer.Allergies = "[]";
            customer.Interests = "[]";
            customer.MarketingOptIn = false;
            customer.NearbyRecommendationsEnabled = false;
            foreach (var scan in await db.CustomerScanEvents.Where(s => s.CustomerUserId == userId).ToListAsync(ct))
            {
                scan.Latitude = null;
                scan.Longitude = null;
                scan.DeviceId = null;
                scan.IpAddress = null;
            }
            foreach (var report in await db.Reports.Where(r => r.ReporterUserId == userId).ToListAsync(ct))
            {
                // The report about the shop or product stays; who sent it doesn't.
                report.IsAnonymous = true;
                report.ReporterIp = null;
                report.DeviceId = null;
                report.Latitude = null;
                report.Longitude = null;
            }
        }

        foreach (var file in files.Where(f => !string.IsNullOrEmpty(f)).Distinct())
            await storage.DeleteAsync(file!, ct);
    }

    private static string? LastDigits(string? accountNumber) =>
        string.IsNullOrEmpty(accountNumber) ? accountNumber
        : accountNumber.Length <= 4 ? new string('*', accountNumber.Length)
        : new string('*', accountNumber.Length - 4) + accountNumber[^4..];
}

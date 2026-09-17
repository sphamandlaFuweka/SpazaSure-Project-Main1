using Microsoft.EntityFrameworkCore;
using SpazaSure.Infrastructure.Entities;

namespace SpazaSure.Infrastructure.Data;

public class SpazaSureDbContext(DbContextOptions<SpazaSureDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<AuthAuditLog> AuthAuditLogs => Set<AuthAuditLog>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierDocument> SupplierDocuments => Set<SupplierDocument>();
    public DbSet<SpazaShop> SpazaShops => Set<SpazaShop>();
    public DbSet<ShopDocument> ShopDocuments => Set<ShopDocument>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductQrCode> ProductQrCodes => Set<ProductQrCode>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<SupplierSubscription> SupplierSubscriptions => Set<SupplierSubscription>();
    public DbSet<GroupBuy> GroupBuys => Set<GroupBuy>();
    public DbSet<GroupBuyProduct> GroupBuyProducts => Set<GroupBuyProduct>();
    public DbSet<GroupBuyParticipant> GroupBuyParticipants => Set<GroupBuyParticipant>();
    public DbSet<GroupBuyParticipantItem> GroupBuyParticipantItems => Set<GroupBuyParticipantItem>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<ShopOnboardingPayment> ShopOnboardingPayments => Set<ShopOnboardingPayment>();
    public DbSet<ShopWalletTransaction> ShopWalletTransactions => Set<ShopWalletTransaction>();
    public DbSet<ShopReview> ShopReviews => Set<ShopReview>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<Report> Reports => Set<Report>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Role
        model.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasIndex(r => r.Name).IsUnique();
        });

        // Permission
        model.Entity<Permission>(e =>
        {
            e.ToTable("permissions");
            e.HasIndex(p => p.Name).IsUnique();
        });

        // RolePermission  composite PK, no BaseEntity
        model.Entity<RolePermission>(e =>
        {
            e.ToTable("role_permissions");
            e.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            e.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
            e.HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId);
        });

        // User
        model.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.Phone).IsUnique();
            e.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId);
        });

        // RefreshToken
        model.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
        });

        // OtpCode
        model.Entity<OtpCode>(e =>
        {
            e.ToTable("otp_codes");
        });

        // AuthAuditLog  long PK, no BaseEntity
        model.Entity<AuthAuditLog>(e =>
        {
            e.ToTable("auth_audit_logs");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).ValueGeneratedOnAdd();
        });

        // Supplier  one-to-one with User
        model.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers");
            e.HasOne(s => s.User).WithOne(u => u.Supplier)
             .HasForeignKey<Supplier>(s => s.UserId);
            e.HasIndex(s => s.RegistrationNumber).IsUnique();
            e.Property(s => s.CommissionRate).HasPrecision(4, 2);
        });

        // SupplierDocument
        model.Entity<SupplierDocument>(e =>
        {
            e.ToTable("supplier_documents");
        });

        // CustomerProfile  one-to-one with User
        model.Entity<CustomerProfile>(e =>
        {
            e.ToTable("customer_profiles");
            e.HasOne(cp => cp.User).WithOne(u => u.CustomerProfile)
             .HasForeignKey<CustomerProfile>(cp => cp.UserId);
            e.HasIndex(cp => cp.UserId).IsUnique();
        });

        // Report  filed by any authenticated user (customer or retailer),
        // optionally against a recognized product.
        model.Entity<Report>(e =>
        {
            e.ToTable("reports");
            e.HasOne(r => r.Reporter).WithMany().HasForeignKey(r => r.ReporterUserId);
            e.HasOne(r => r.Product).WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(r => r.ReporterUserId);
            e.HasIndex(r => r.Status);
        });

        // SpazaShop  one-to-one with User
        model.Entity<SpazaShop>(e =>
        {
            e.ToTable("spaza_shops");
            e.HasOne(s => s.User).WithOne(u => u.SpazaShop)
             .HasForeignKey<SpazaShop>(s => s.UserId);
            e.Property(s => s.RatingAvg).HasPrecision(3, 2);
        });

        model.Entity<ShopReview>(e =>
        {
            e.ToTable("shop_reviews");
            e.HasIndex(r => new { r.ShopId, r.ReviewerUserId }).IsUnique();
            e.HasOne(r => r.Shop).WithMany().HasForeignKey(r => r.ShopId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Reviewer).WithMany().HasForeignKey(r => r.ReviewerUserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ShopDocument
        model.Entity<ShopDocument>(e =>
        {
            e.ToTable("shop_documents");
        });

        // Category
        model.Entity<Category>(e =>
        {
            e.ToTable("categories");
        });

        // Product
        model.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasIndex(p => new { p.SupplierId, p.Sku }).IsUnique();
            e.Property(p => p.Price).HasPrecision(10, 2);
        });

        // ProductQrCode
        model.Entity<ProductQrCode>(e =>
        {
            e.ToTable("product_qr_codes");
            e.HasIndex(q => q.QrCode).IsUnique();
            e.HasIndex(q => q.ProductId).IsUnique()
                .HasFilter("order_item_id IS NULL")
                .HasDatabaseName("UX_ProductQrCodes_DefaultProduct");
        });

        // Order
        model.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.HasIndex(o => new { o.GroupBuyId, o.ShopId })
                .IsUnique()
                .HasFilter("group_buy_id IS NOT NULL");
            e.HasIndex(o => o.GroupBuyParticipantId)
                .IsUnique()
                .HasFilter("group_buy_participant_id IS NOT NULL");
            e.Property(o => o.Subtotal).HasPrecision(10, 2);
            e.Property(o => o.DeliveryFee).HasPrecision(10, 2);
            e.Property(o => o.DeliveryMarkup).HasPrecision(10, 2);
            e.Property(o => o.DeliveryDistanceKm).HasPrecision(10, 2);
            e.Property(o => o.PlatformCommission).HasPrecision(10, 2);
            e.Property(o => o.TotalAmount).HasPrecision(10, 2);
            e.HasOne(o => o.Shop).WithMany().HasForeignKey(o => o.ShopId);
            e.HasOne(o => o.Supplier).WithMany().HasForeignKey(o => o.SupplierId);
            e.HasOne(o => o.GroupBuy).WithMany().HasForeignKey(o => o.GroupBuyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.GroupBuyParticipant).WithMany().HasForeignKey(o => o.GroupBuyParticipantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // OrderItem
        model.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");
            e.Property(i => i.OriginalUnitPrice).HasPrecision(10, 2);
            e.Property(i => i.UnitPrice).HasPrecision(10, 2);
            e.Property(i => i.DiscountPct).HasPrecision(5, 2);
            e.Property(i => i.LineTotal).HasPrecision(10, 2);
            e.HasOne(i => i.Order).WithMany(o => o.Items).HasForeignKey(i => i.OrderId);
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId);
        });

        // SubscriptionPlan
        model.Entity<SubscriptionPlan>(e =>
        {
            e.ToTable("subscription_plans");
            e.Property(p => p.MonthlyPrice).HasPrecision(10, 2);
            e.Property(p => p.AnnualPrice).HasPrecision(10, 2);
            e.Property(p => p.CommissionRate).HasPrecision(4, 2);
            e.HasIndex(p => p.Tier).IsUnique();
        });

        // SupplierSubscription
        model.Entity<SupplierSubscription>(e =>
        {
            e.ToTable("supplier_subscriptions");
            e.Property(s => s.AmountPaid).HasPrecision(10, 2);
            e.HasOne(s => s.Supplier).WithMany().HasForeignKey(s => s.SupplierId);
            e.HasOne(s => s.Plan).WithMany(p => p.Subscriptions).HasForeignKey(s => s.PlanId);
        });

        // GroupBuy
        model.Entity<GroupBuy>(e =>
        {
            e.ToTable("group_buys");
            e.HasIndex(g => g.Status);
            e.HasIndex(g => g.ExpiresAt);
            e.HasIndex(g => new { g.Status, g.ExpiresAt });
            e.Property(g => g.OriginalPrice).HasPrecision(10, 2);
            e.Property(g => g.DiscountPrice).HasPrecision(10, 2);
            e.HasOne(g => g.Product).WithMany().HasForeignKey(g => g.ProductId);
            e.HasOne(g => g.Supplier).WithMany().HasForeignKey(g => g.SupplierId);
            e.HasOne(g => g.CreatedByShop).WithMany().HasForeignKey(g => g.CreatedByShopId);
        });

        // GroupBuyProduct
        model.Entity<GroupBuyProduct>(e =>
        {
            e.ToTable("group_buy_products");
            e.HasIndex(p => new { p.GroupBuyId, p.ProductId }).IsUnique();
            e.HasIndex(p => p.Status);
            e.Property(p => p.OriginalPrice).HasPrecision(10, 2);
            e.Property(p => p.DiscountPrice).HasPrecision(10, 2);
            e.HasOne(p => p.GroupBuy).WithMany(g => g.Products).HasForeignKey(p => p.GroupBuyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Product).WithMany().HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // GroupBuyParticipant
        model.Entity<GroupBuyParticipant>(e =>
        {
            e.ToTable("group_buy_participants");
            e.HasIndex(p => new { p.GroupBuyId, p.ShopId }).IsUnique();
            e.HasOne(p => p.GroupBuy).WithMany(g => g.Participants).HasForeignKey(p => p.GroupBuyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Shop).WithMany().HasForeignKey(p => p.ShopId);
        });

        // GroupBuyParticipantItem
        model.Entity<GroupBuyParticipantItem>(e =>
        {
            e.ToTable("group_buy_participant_items");
            e.HasIndex(i => new { i.ParticipantId, i.GroupBuyProductId }).IsUnique();
            e.HasOne(i => i.Participant).WithMany(p => p.Items).HasForeignKey(i => i.ParticipantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.GroupBuyProduct).WithMany(p => p.Items).HasForeignKey(i => i.GroupBuyProductId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<PlatformSetting>(e =>
        {
            e.ToTable("platform_settings");
            e.HasIndex(s => s.Key).IsUnique();
        });

        model.Entity<ShopOnboardingPayment>(e =>
        {
            e.ToTable("shop_onboarding_payments");
            e.Property(p => p.Amount).HasPrecision(10, 2);
            e.HasIndex(p => p.ShopId).IsUnique()
                .HasFilter("status = 'pending'");
            e.HasIndex(p => p.PayFastPaymentId).IsUnique()
                .HasFilter("pay_fast_payment_id IS NOT NULL");
            e.HasIndex(p => p.Status);
            e.HasOne(p => p.Shop).WithMany(s => s.OnboardingPayments)
                .HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Restrict);
        });

        base.OnModelCreating(model);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(ct);
    }

    private void UpdateTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries<SpazaSure.Shared.Models.BaseEntity>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}

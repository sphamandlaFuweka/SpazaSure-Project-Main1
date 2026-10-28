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
    public DbSet<AccountDeletionRequest> AccountDeletionRequests => Set<AccountDeletionRequest>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierBranch> SupplierBranches => Set<SupplierBranch>();
    public DbSet<ProductBranchStock> ProductBranchStock => Set<ProductBranchStock>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DriverDocument> DriverDocuments => Set<DriverDocument>();
    public DbSet<TaxInvoice> TaxInvoices => Set<TaxInvoice>();
    public DbSet<SupplierDocument> SupplierDocuments => Set<SupplierDocument>();
    public DbSet<SpazaShop> SpazaShops => Set<SpazaShop>();
    public DbSet<ShopDocument> ShopDocuments => Set<ShopDocument>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductQrCode> ProductQrCodes => Set<ProductQrCode>();
    public DbSet<ProductUnitCode> ProductUnitCodes => Set<ProductUnitCode>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<SupplierSubscription> SupplierSubscriptions => Set<SupplierSubscription>();
    public DbSet<GroupBuy> GroupBuys => Set<GroupBuy>();
    public DbSet<GroupBuyProduct> GroupBuyProducts => Set<GroupBuyProduct>();
    public DbSet<GroupBuyParticipant> GroupBuyParticipants => Set<GroupBuyParticipant>();
    public DbSet<GroupBuyParticipantItem> GroupBuyParticipantItems => Set<GroupBuyParticipantItem>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<FeedbackItem> FeedbackItems => Set<FeedbackItem>();
    public DbSet<WaitlistSignup> WaitlistSignups => Set<WaitlistSignup>();
    public DbSet<EmailSend> EmailSends => Set<EmailSend>();
    public DbSet<ShopOnboardingPayment> ShopOnboardingPayments => Set<ShopOnboardingPayment>();
    public DbSet<ShopWalletTransaction> ShopWalletTransactions => Set<ShopWalletTransaction>();
    public DbSet<ShopConversation> ShopConversations => Set<ShopConversation>();
    public DbSet<ShopMessage> ShopMessages => Set<ShopMessage>();
    public DbSet<ShopWalletLedgerEntry> ShopWalletLedgerEntries => Set<ShopWalletLedgerEntry>();
    public DbSet<ShopReview> ShopReviews => Set<ShopReview>();
    public DbSet<ShopBusinessProfile> ShopBusinessProfiles => Set<ShopBusinessProfile>();
    public DbSet<ShopStockItem> ShopStockItems => Set<ShopStockItem>();
    public DbSet<ShopStockMovement> ShopStockMovements => Set<ShopStockMovement>();
    public DbSet<ShopCashEntry> ShopCashEntries => Set<ShopCashEntry>();
    public DbSet<ShopLessonProgress> ShopLessonProgress => Set<ShopLessonProgress>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<CustomerSubscription> CustomerSubscriptions => Set<CustomerSubscription>();
    public DbSet<CustomerScanEvent> CustomerScanEvents => Set<CustomerScanEvent>();
    public DbSet<CustomerRewardTransaction> CustomerRewardTransactions => Set<CustomerRewardTransaction>();
    public DbSet<CustomerVoucher> CustomerVouchers => Set<CustomerVoucher>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<PmRecord> PmRecords => Set<PmRecord>();

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
            e.Property(u => u.DisplayName).HasMaxLength(120);
            e.Property(u => u.AdminTitle).HasMaxLength(80);
            e.Property(u => u.AdminAccess).HasMaxLength(500);
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
            e.Property(code => code.FailedAttempts).HasDefaultValue(0);
        });

        model.Entity<AccountDeletionRequest>(e =>
        {
            e.ToTable("account_deletion_requests");
            e.Property(r => r.Role).HasMaxLength(20);
            e.Property(r => r.Reason).HasMaxLength(1000);
            e.Property(r => r.Status).HasMaxLength(20);
            e.Property(r => r.DecisionNote).HasMaxLength(1000);
            e.HasIndex(r => r.UserId);
            e.HasIndex(r => r.Status);
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
            e.Property(s => s.IdentityCheckSessionId).HasMaxLength(64);
            e.Property(s => s.IdentityCheckStatus).HasMaxLength(30);
            e.Property(s => s.IdentityCheckNotes).HasMaxLength(2000);
            e.HasIndex(s => s.IdentityCheckSessionId);
            e.Property(s => s.PaystackRecipientCode).HasMaxLength(64);
            e.Property(s => s.PaystackSubaccountCode).HasMaxLength(64);
        });

        // SupplierBranch: the supplier's warehouses and depots
        model.Entity<SupplierBranch>(e =>
        {
            e.ToTable("supplier_branches");
            e.Property(b => b.Name).HasMaxLength(80);
            e.Property(b => b.Address).HasMaxLength(300);
            e.Property(b => b.City).HasMaxLength(100);
            e.Property(b => b.Province).HasMaxLength(100);
            e.Property(b => b.PostalCode).HasMaxLength(10);
            e.HasIndex(b => new { b.SupplierId, b.IsActive });
            e.HasOne(b => b.Supplier).WithMany(s => s.Branches).HasForeignKey(b => b.SupplierId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ProductBranchStock>(e =>
        {
            e.ToTable("product_branch_stock");
            e.HasIndex(s => new { s.ProductId, s.SupplierBranchId }).IsUnique();
            e.HasOne(s => s.Product).WithMany(p => p.BranchStock).HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.SupplierBranch).WithMany().HasForeignKey(s => s.SupplierBranchId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Driver>(e =>
        {
            e.ToTable("drivers");
            e.Property(d => d.FullName).HasMaxLength(120);
            e.Property(d => d.Phone).HasMaxLength(20);
            e.Property(d => d.VehicleType).HasMaxLength(40);
            e.Property(d => d.VehicleRegistration).HasMaxLength(20);
            e.Property(d => d.Area).HasMaxLength(100);
            e.Property(d => d.AccessToken).HasMaxLength(64);
            e.Property(d => d.BadgeCode).HasMaxLength(12);
            e.HasIndex(d => d.AccessToken).IsUnique();
            e.HasIndex(d => d.BadgeCode).IsUnique();
        });

        model.Entity<DriverDocument>(e =>
        {
            e.ToTable("driver_documents");
            e.Property(d => d.DocType).HasMaxLength(20);
            e.Property(d => d.DocUrl).HasMaxLength(500);
            e.HasIndex(d => new { d.DriverId, d.DocType }).IsUnique();
            e.HasOne(d => d.Driver).WithMany(d => d.Documents).HasForeignKey(d => d.DriverId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<TaxInvoice>(e =>
        {
            e.ToTable("tax_invoices");
            e.HasIndex(i => i.OrderId).IsUnique();
            e.HasIndex(i => new { i.SupplierId, i.SequenceNumber }).IsUnique();
            e.HasIndex(i => i.InvoiceNumber).IsUnique();
            e.Property(i => i.InvoiceNumber).HasMaxLength(40);
            e.Property(i => i.SupplierVatNumber).HasMaxLength(20);
            e.Property(i => i.VerificationCode).HasMaxLength(32);
            e.Property(i => i.GoodsTotalInclVat).HasPrecision(12, 2);
            e.Property(i => i.VatAmount).HasPrecision(12, 2);
            e.HasOne(i => i.Order).WithMany().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(i => i.Supplier).WithMany().HasForeignKey(i => i.SupplierId).OnDelete(DeleteBehavior.Restrict);
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

        model.Entity<CustomerSubscription>(e =>
        {
            e.ToTable("customer_subscriptions");
            e.HasOne<User>().WithMany().HasForeignKey(s => s.CustomerUserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.CustomerUserId, s.CreatedAt });
            e.HasIndex(s => s.CustomerUserId).IsUnique()
                .HasFilter("\"status\" IN ('pending', 'active', 'past_due', 'unpaid', 'incomplete', 'trialing', 'paused')");
            e.HasIndex(s => s.PaymentProviderSubscriptionCode).IsUnique()
                .HasFilter("\"payment_provider_subscription_code\" IS NOT NULL");
            e.HasIndex(s => s.PaymentProviderCheckoutReference).IsUnique()
                .HasFilter("\"payment_provider_checkout_reference\" IS NOT NULL");
        });

        model.Entity<SupplierSubscription>(e =>
        {
            e.HasIndex(s => s.PaymentProviderSubscriptionCode).IsUnique()
                .HasFilter("\"payment_provider_subscription_code\" IS NOT NULL");
            e.HasIndex(s => s.PaymentProviderCheckoutReference).IsUnique()
                .HasFilter("\"payment_provider_checkout_reference\" IS NOT NULL");
        });

        model.Entity<CustomerScanEvent>(e =>
        {
            e.ToTable("customer_scan_events");
            e.HasIndex(s => new { s.CustomerUserId, s.Code, s.CreatedAt });
            e.HasOne(s => s.Customer).WithMany().HasForeignKey(s => s.CustomerUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Product).WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<CustomerRewardTransaction>(e =>
        {
            e.ToTable("customer_reward_transactions");
            e.HasIndex(t => new { t.CustomerUserId, t.CreatedAt });
            e.HasOne(t => t.Customer).WithMany().HasForeignKey(t => t.CustomerUserId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<CustomerVoucher>(e =>
        {
            e.ToTable("customer_vouchers");
            e.HasIndex(v => v.Code).IsUnique();
            e.HasIndex(v => new { v.CustomerUserId, v.Status });
            e.Property(v => v.Amount).HasPrecision(10, 2);
            e.HasOne(v => v.Customer).WithMany().HasForeignKey(v => v.CustomerUserId).OnDelete(DeleteBehavior.Cascade);
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
            e.Property(s => s.ShopCode).HasMaxLength(12);
            e.HasIndex(s => s.ShopCode).IsUnique();
            e.Property(s => s.IdentityCheckSessionId).HasMaxLength(64);
            e.Property(s => s.IdentityCheckStatus).HasMaxLength(30);
            e.Property(s => s.IdentityCheckNotes).HasMaxLength(2000);
            e.HasIndex(s => s.IdentityCheckSessionId);
        });

        model.Entity<ShopReview>(e =>
        {
            e.ToTable("shop_reviews");
            e.Property(r => r.OwnerReply).HasMaxLength(1000);
            e.HasIndex(r => new { r.ShopId, r.ReviewerUserId }).IsUnique();
            e.HasOne(r => r.Shop).WithMany().HasForeignKey(r => r.ShopId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Reviewer).WithMany().HasForeignKey(r => r.ReviewerUserId).OnDelete(DeleteBehavior.Cascade);
        });

        // In-app chat between customers and shops
        model.Entity<ShopConversation>(e =>
        {
            e.ToTable("shop_conversations");
            e.HasIndex(c => new { c.ShopId, c.CustomerUserId }).IsUnique();
            e.HasIndex(c => c.CustomerUserId);
            e.Property(c => c.LastMessagePreview).HasMaxLength(200);
            e.HasOne(c => c.Shop).WithMany().HasForeignKey(c => c.ShopId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.CustomerUser).WithMany().HasForeignKey(c => c.CustomerUserId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ShopMessage>(e =>
        {
            e.ToTable("shop_messages");
            e.HasIndex(m => new { m.ConversationId, m.CreatedAt });
            e.Property(m => m.SenderRole).HasMaxLength(20);
            e.Property(m => m.Body).HasMaxLength(1000);
            e.HasOne(m => m.Conversation).WithMany(c => c.Messages).HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ShopWalletTransaction>(e =>
        {
            e.ToTable("shop_wallet_transactions");
            e.HasIndex(t => t.ShopId);
            e.HasOne(t => t.Shop).WithMany().HasForeignKey(t => t.ShopId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ShopWalletLedgerEntry>(e =>
        {
            e.ToTable("shop_wallet_ledger_entries");
            e.HasIndex(t => new { t.ShopId, t.CreatedAt });
            e.HasIndex(t => t.SourceId);
            e.HasOne(t => t.Shop).WithMany().HasForeignKey(t => t.ShopId).OnDelete(DeleteBehavior.Cascade);
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
            // A product may have no SKU yet; only filled-in SKUs are unique per supplier
            e.HasIndex(p => new { p.SupplierId, p.Sku }).IsUnique()
                .HasFilter("sku <> ''").HasDatabaseName("IX_Products_SupplierId_Sku_Filled");
            e.Property(p => p.Price).HasPrecision(10, 2);
            e.Property(p => p.Brand).HasMaxLength(120);
            e.Property(p => p.PackSize).HasMaxLength(60);
            e.Property(p => p.CountryOfOrigin).HasMaxLength(80);
            e.Property(p => p.Manufacturer).HasMaxLength(160);
            e.Property(p => p.ServingSize).HasMaxLength(60);
            e.Property(p => p.ServingsPerPack).HasMaxLength(60);
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

        // ProductUnitCode
        model.Entity<ProductUnitCode>(e =>
        {
            e.ToTable("product_unit_codes");
            e.HasIndex(u => u.Code).IsUnique();
            e.HasIndex(u => new { u.ProductId, u.BatchNumber });
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
            e.HasIndex(o => o.PayoutReference);
            e.Property(o => o.DeliveryMarkup).HasPrecision(10, 2);
            e.Property(o => o.DeliveryDistanceKm).HasPrecision(10, 2);
            e.Property(o => o.PlatformCommission).HasPrecision(10, 2);
            e.Property(o => o.TotalAmount).HasPrecision(10, 2);
            e.HasIndex(o => o.PaymentCheckoutId);
            e.HasIndex(o => o.PaymentProviderReference);
            e.Property(o => o.PaystackSplitCode).HasMaxLength(64);
            e.HasOne(o => o.Shop).WithMany().HasForeignKey(o => o.ShopId);
            e.HasOne(o => o.Supplier).WithMany().HasForeignKey(o => o.SupplierId);
            e.HasOne(o => o.SupplierBranch).WithMany().HasForeignKey(o => o.SupplierBranchId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(o => o.Driver).WithMany().HasForeignKey(o => o.DriverId).OnDelete(DeleteBehavior.SetNull);
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
            e.HasOne(g => g.SupplierBranch).WithMany().HasForeignKey(g => g.SupplierBranchId).OnDelete(DeleteBehavior.SetNull);
            e.Property(g => g.AreaCity).HasMaxLength(100);
            e.Property(g => g.AreaProvince).HasMaxLength(100);
            e.Property(g => g.AreaRadiusKm).HasPrecision(7, 1);
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

        model.Entity<Faq>(e =>
        {
            e.ToTable("faqs");
            e.HasIndex(f => f.SortOrder);
        });

        // Admin Projects & Impact workspace: one row per record, fields in a jsonb document
        model.Entity<PmRecord>(e =>
        {
            e.ToTable("pm_records");
            e.Property(r => r.Kind).HasMaxLength(40);
            e.Property(r => r.Data).HasColumnType("jsonb");
            e.HasIndex(r => r.Kind);
        });

        model.Entity<ShopOnboardingPayment>(e =>
        {
            e.ToTable("shop_onboarding_payments");
            e.Property(p => p.Amount).HasPrecision(10, 2);
            e.HasIndex(p => p.ShopId).IsUnique()
                .HasFilter("status = 'pending'");
            e.HasIndex(p => p.ProviderPaymentId).IsUnique()
                .HasFilter("provider_payment_id IS NOT NULL");
            e.HasIndex(p => p.Status);
            e.HasOne(p => p.Shop).WithMany(s => s.OnboardingPayments)
                .HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Restrict);
        });

        // Shop Academy: the shop's own business records and lesson progress
        model.Entity<ShopBusinessProfile>(e =>
        {
            e.ToTable("shop_business_profiles");
            e.HasIndex(p => p.ShopId).IsUnique();
            e.Property(p => p.LegalStructure).HasMaxLength(30);
            e.Property(p => p.CipcRegistrationNumber).HasMaxLength(30);
            e.Property(p => p.SarsTaxNumber).HasMaxLength(20);
            e.Property(p => p.VatNumber).HasMaxLength(20);
            e.Property(p => p.TradingLicenceNumber).HasMaxLength(50);
            e.Property(p => p.PremisesType).HasMaxLength(20);
            e.Property(p => p.MonthlyRent).HasPrecision(12, 2);
            e.Property(p => p.TradingHours).HasMaxLength(100);
            e.Property(p => p.BankName).HasMaxLength(60);
            e.Property(p => p.MainSuppliers).HasMaxLength(500);
            e.Property(p => p.Goals).HasMaxLength(1000);
            e.HasOne(p => p.Shop).WithMany().HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ShopStockItem>(e =>
        {
            e.ToTable("shop_stock_items");
            e.HasIndex(i => i.ShopId);
            e.HasIndex(i => new { i.ShopId, i.ProductId });
            e.Property(i => i.Name).HasMaxLength(200);
            e.Property(i => i.Category).HasMaxLength(60);
            e.Property(i => i.Unit).HasMaxLength(30);
            e.Property(i => i.Barcode).HasMaxLength(50);
            e.Property(i => i.CostPrice).HasPrecision(12, 2);
            e.Property(i => i.SellingPrice).HasPrecision(12, 2);
            e.HasOne(i => i.Shop).WithMany().HasForeignKey(i => i.ShopId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ShopStockMovement>(e =>
        {
            e.ToTable("shop_stock_movements");
            e.HasIndex(m => new { m.ShopId, m.OccurredOn });
            e.HasIndex(m => m.StockItemId);
            e.HasIndex(m => m.SourceOrderId);
            e.Property(m => m.Type).HasMaxLength(20);
            e.Property(m => m.UnitCost).HasPrecision(12, 2);
            e.Property(m => m.UnitPrice).HasPrecision(12, 2);
            e.Property(m => m.Note).HasMaxLength(300);
            e.HasOne(m => m.StockItem).WithMany(i => i.Movements).HasForeignKey(m => m.StockItemId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ShopCashEntry>(e =>
        {
            e.ToTable("shop_cash_entries");
            e.HasIndex(c => new { c.ShopId, c.OccurredOn });
            e.Property(c => c.Direction).HasMaxLength(10);
            e.Property(c => c.Category).HasMaxLength(40);
            e.Property(c => c.Amount).HasPrecision(12, 2);
            e.Property(c => c.Note).HasMaxLength(300);
            e.HasOne(c => c.Shop).WithMany().HasForeignKey(c => c.ShopId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ShopLessonProgress>(e =>
        {
            e.ToTable("shop_lesson_progress");
            e.HasIndex(p => new { p.ShopId, p.LessonKey }).IsUnique();
            e.Property(p => p.LessonKey).HasMaxLength(60);
            e.HasOne(p => p.Shop).WithMany().HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Cascade);
        });

        // Testing-phase engagement: feedback, the website waitlist and the automated email log
        model.Entity<FeedbackItem>(e =>
        {
            e.ToTable("feedback_items");
            e.HasIndex(f => new { f.Status, f.CreatedAt });
            e.HasIndex(f => f.UserId);
            e.Property(f => f.Role).HasMaxLength(30);
            e.Property(f => f.Type).HasMaxLength(20);
            e.Property(f => f.Message).HasMaxLength(2000);
            e.Property(f => f.Screen).HasMaxLength(120);
            e.Property(f => f.ScreenshotUrl).HasMaxLength(500);
            e.Property(f => f.Source).HasMaxLength(30);
            e.Property(f => f.AppVersion).HasMaxLength(30);
            e.Property(f => f.Platform).HasMaxLength(60);
            e.Property(f => f.Status).HasMaxLength(20);
            e.Property(f => f.AdminNote).HasMaxLength(1000);
            e.HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<WaitlistSignup>(e =>
        {
            e.ToTable("waitlist_signups");
            e.HasIndex(w => w.Email).IsUnique();
            e.HasIndex(w => w.CreatedAt);
            e.Property(w => w.Email).HasMaxLength(254);
            e.Property(w => w.Name).HasMaxLength(120);
            e.Property(w => w.Phone).HasMaxLength(20);
            e.Property(w => w.Interest).HasMaxLength(20);
            e.Property(w => w.Area).HasMaxLength(120);
            e.Property(w => w.Source).HasMaxLength(80);
            e.Property(w => w.IpAddress).HasMaxLength(64);
        });

        model.Entity<EmailSend>(e =>
        {
            e.ToTable("email_sends");
            e.HasIndex(s => new { s.UserId, s.Key }).IsUnique();
            e.HasIndex(s => s.CreatedAt);
            e.Property(s => s.Key).HasMaxLength(60);
            e.Property(s => s.Email).HasMaxLength(254);
            e.Property(s => s.Subject).HasMaxLength(200);
            e.Property(s => s.Status).HasMaxLength(20);
            e.Property(s => s.Error).HasMaxLength(500);
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

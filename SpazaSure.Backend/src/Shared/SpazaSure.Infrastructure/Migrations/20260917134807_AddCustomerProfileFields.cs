using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Orders_OrderId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_SpazaShops_ShopId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Suppliers_SupplierId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_OtpCodes_Users_UserId",
                table: "OtpCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductQrCodes_Products_ProductId",
                table: "ProductQrCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Suppliers_SupplierId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Permissions_PermissionId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_ShopDocuments_SpazaShops_ShopId",
                table: "ShopDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_SpazaShops_Users_UserId",
                table: "SpazaShops");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierDocuments_Suppliers_SupplierId",
                table: "SupplierDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Users_UserId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Suppliers",
                table: "Suppliers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Roles",
                table: "Roles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Products",
                table: "Products");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Permissions",
                table: "Permissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Orders",
                table: "Orders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Categories",
                table: "Categories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SupplierDocuments",
                table: "SupplierDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SpazaShops",
                table: "SpazaShops");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ShopDocuments",
                table: "ShopDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RefreshTokens",
                table: "RefreshTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductQrCodes",
                table: "ProductQrCodes");

            migrationBuilder.DropIndex(
                name: "IX_ProductQrCodes_ProductId",
                table: "ProductQrCodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OtpCodes",
                table: "OtpCodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrderItems",
                table: "OrderItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AuthAuditLogs",
                table: "AuthAuditLogs");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "Suppliers",
                newName: "suppliers");

            migrationBuilder.RenameTable(
                name: "Roles",
                newName: "roles");

            migrationBuilder.RenameTable(
                name: "Products",
                newName: "products");

            migrationBuilder.RenameTable(
                name: "Permissions",
                newName: "permissions");

            migrationBuilder.RenameTable(
                name: "Orders",
                newName: "orders");

            migrationBuilder.RenameTable(
                name: "Categories",
                newName: "categories");

            migrationBuilder.RenameTable(
                name: "SupplierDocuments",
                newName: "supplier_documents");

            migrationBuilder.RenameTable(
                name: "SpazaShops",
                newName: "spaza_shops");

            migrationBuilder.RenameTable(
                name: "ShopDocuments",
                newName: "shop_documents");

            migrationBuilder.RenameTable(
                name: "RolePermissions",
                newName: "role_permissions");

            migrationBuilder.RenameTable(
                name: "RefreshTokens",
                newName: "refresh_tokens");

            migrationBuilder.RenameTable(
                name: "ProductQrCodes",
                newName: "product_qr_codes");

            migrationBuilder.RenameTable(
                name: "OtpCodes",
                newName: "otp_codes");

            migrationBuilder.RenameTable(
                name: "OrderItems",
                newName: "order_items");

            migrationBuilder.RenameTable(
                name: "AuthAuditLogs",
                newName: "auth_audit_logs");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "users",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "users",
                newName: "phone");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "users",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "users",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "users",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "RoleId",
                table: "users",
                newName: "role_id");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "users",
                newName: "password_hash");

            migrationBuilder.RenameColumn(
                name: "MfaSecret",
                table: "users",
                newName: "mfa_secret");

            migrationBuilder.RenameColumn(
                name: "MfaEnabled",
                table: "users",
                newName: "mfa_enabled");

            migrationBuilder.RenameColumn(
                name: "LockedUntil",
                table: "users",
                newName: "locked_until");

            migrationBuilder.RenameColumn(
                name: "LastLoginAt",
                table: "users",
                newName: "last_login_at");

            migrationBuilder.RenameColumn(
                name: "FailedAttempts",
                table: "users",
                newName: "failed_attempts");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "users",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Phone",
                table: "users",
                newName: "ix_users_phone");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Email",
                table: "users",
                newName: "ix_users_email");

            migrationBuilder.RenameIndex(
                name: "IX_Users_RoleId",
                table: "users",
                newName: "ix_users_role_id");

            migrationBuilder.RenameColumn(
                name: "Tier",
                table: "suppliers",
                newName: "tier");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "suppliers",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Province",
                table: "suppliers",
                newName: "province");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "suppliers",
                newName: "phone");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "suppliers",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "City",
                table: "suppliers",
                newName: "city");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "suppliers",
                newName: "address");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "suppliers",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "VatNumber",
                table: "suppliers",
                newName: "vat_number");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "suppliers",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "suppliers",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "RegistrationNumber",
                table: "suppliers",
                newName: "registration_number");

            migrationBuilder.RenameColumn(
                name: "PostalCode",
                table: "suppliers",
                newName: "postal_code");

            migrationBuilder.RenameColumn(
                name: "LogoUrl",
                table: "suppliers",
                newName: "logo_url");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "suppliers",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ContactPerson",
                table: "suppliers",
                newName: "contact_person");

            migrationBuilder.RenameColumn(
                name: "CompanyName",
                table: "suppliers",
                newName: "company_name");

            migrationBuilder.RenameColumn(
                name: "CommissionRate",
                table: "suppliers",
                newName: "commission_rate");

            migrationBuilder.RenameColumn(
                name: "BankName",
                table: "suppliers",
                newName: "bank_name");

            migrationBuilder.RenameColumn(
                name: "BankBranchCode",
                table: "suppliers",
                newName: "bank_branch_code");

            migrationBuilder.RenameColumn(
                name: "BankAccount",
                table: "suppliers",
                newName: "bank_account");

            migrationBuilder.RenameIndex(
                name: "IX_Suppliers_UserId",
                table: "suppliers",
                newName: "ix_suppliers_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_Suppliers_RegistrationNumber",
                table: "suppliers",
                newName: "ix_suppliers_registration_number");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "roles",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "roles",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "roles",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "roles",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "roles",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "roles",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_Roles_Name",
                table: "roles",
                newName: "ix_roles_name");

            migrationBuilder.RenameColumn(
                name: "Unit",
                table: "products",
                newName: "unit");

            migrationBuilder.RenameColumn(
                name: "Sku",
                table: "products",
                newName: "sku");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "products",
                newName: "price");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "products",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Images",
                table: "products",
                newName: "images");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "products",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Barcode",
                table: "products",
                newName: "barcode");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "products",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "products",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                table: "products",
                newName: "supplier_id");

            migrationBuilder.RenameColumn(
                name: "StockQty",
                table: "products",
                newName: "stock_qty");

            migrationBuilder.RenameColumn(
                name: "MinOrderQty",
                table: "products",
                newName: "min_order_qty");

            migrationBuilder.RenameColumn(
                name: "IsFoodItem",
                table: "products",
                newName: "is_food_item");

            migrationBuilder.RenameColumn(
                name: "IsAvailable",
                table: "products",
                newName: "is_available");

            migrationBuilder.RenameColumn(
                name: "IsApproved",
                table: "products",
                newName: "is_approved");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "products",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CategoryId",
                table: "products",
                newName: "category_id");

            migrationBuilder.RenameColumn(
                name: "ApprovedBy",
                table: "products",
                newName: "approved_by");

            migrationBuilder.RenameColumn(
                name: "ApprovedAt",
                table: "products",
                newName: "approved_at");

            migrationBuilder.RenameIndex(
                name: "IX_Products_SupplierId_Sku",
                table: "products",
                newName: "ix_products_supplier_id_sku");

            migrationBuilder.RenameIndex(
                name: "IX_Products_CategoryId",
                table: "products",
                newName: "ix_products_category_id");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "permissions",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Module",
                table: "permissions",
                newName: "module");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "permissions",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "permissions",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "permissions",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "permissions",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_Permissions_Name",
                table: "permissions",
                newName: "ix_permissions_name");

            migrationBuilder.RenameColumn(
                name: "Subtotal",
                table: "orders",
                newName: "subtotal");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "orders",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "orders",
                newName: "notes");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "orders",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "orders",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                table: "orders",
                newName: "total_amount");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                table: "orders",
                newName: "supplier_id");

            migrationBuilder.RenameColumn(
                name: "ShopId",
                table: "orders",
                newName: "shop_id");

            migrationBuilder.RenameColumn(
                name: "RejectionReason",
                table: "orders",
                newName: "rejection_reason");

            migrationBuilder.RenameColumn(
                name: "PlatformCommission",
                table: "orders",
                newName: "platform_commission");

            migrationBuilder.RenameColumn(
                name: "OrderNumber",
                table: "orders",
                newName: "order_number");

            migrationBuilder.RenameColumn(
                name: "GroupBuyId",
                table: "orders",
                newName: "group_buy_id");

            migrationBuilder.RenameColumn(
                name: "DeliveryType",
                table: "orders",
                newName: "delivery_type");

            migrationBuilder.RenameColumn(
                name: "DeliveryMarkup",
                table: "orders",
                newName: "delivery_markup");

            migrationBuilder.RenameColumn(
                name: "DeliveryFee",
                table: "orders",
                newName: "delivery_fee");

            migrationBuilder.RenameColumn(
                name: "DeliveryAddress",
                table: "orders",
                newName: "delivery_address");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "orders",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_SupplierId",
                table: "orders",
                newName: "ix_orders_supplier_id");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_ShopId",
                table: "orders",
                newName: "ix_orders_shop_id");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_OrderNumber",
                table: "orders",
                newName: "ix_orders_order_number");

            migrationBuilder.RenameColumn(
                name: "Slug",
                table: "categories",
                newName: "slug");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "categories",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "categories",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "categories",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "SortOrder",
                table: "categories",
                newName: "sort_order");

            migrationBuilder.RenameColumn(
                name: "ParentId",
                table: "categories",
                newName: "parent_id");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "categories",
                newName: "is_active");

            migrationBuilder.RenameColumn(
                name: "IconUrl",
                table: "categories",
                newName: "icon_url");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "categories",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_Categories_ParentId",
                table: "categories",
                newName: "ix_categories_parent_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "supplier_documents",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "supplier_documents",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "VerifiedBy",
                table: "supplier_documents",
                newName: "verified_by");

            migrationBuilder.RenameColumn(
                name: "VerifiedAt",
                table: "supplier_documents",
                newName: "verified_at");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "supplier_documents",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                table: "supplier_documents",
                newName: "supplier_id");

            migrationBuilder.RenameColumn(
                name: "RejectionNote",
                table: "supplier_documents",
                newName: "rejection_note");

            migrationBuilder.RenameColumn(
                name: "ExpiryDate",
                table: "supplier_documents",
                newName: "expiry_date");

            migrationBuilder.RenameColumn(
                name: "DocUrl",
                table: "supplier_documents",
                newName: "doc_url");

            migrationBuilder.RenameColumn(
                name: "DocType",
                table: "supplier_documents",
                newName: "doc_type");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "supplier_documents",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_SupplierDocuments_SupplierId",
                table: "supplier_documents",
                newName: "ix_supplier_documents_supplier_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "spaza_shops",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Province",
                table: "spaza_shops",
                newName: "province");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "spaza_shops",
                newName: "phone");

            migrationBuilder.RenameColumn(
                name: "Longitude",
                table: "spaza_shops",
                newName: "longitude");

            migrationBuilder.RenameColumn(
                name: "Latitude",
                table: "spaza_shops",
                newName: "latitude");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "spaza_shops",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "City",
                table: "spaza_shops",
                newName: "city");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "spaza_shops",
                newName: "address");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "spaza_shops",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "spaza_shops",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "spaza_shops",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ShopName",
                table: "spaza_shops",
                newName: "shop_name");

            migrationBuilder.RenameColumn(
                name: "RatingCount",
                table: "spaza_shops",
                newName: "rating_count");

            migrationBuilder.RenameColumn(
                name: "RatingAvg",
                table: "spaza_shops",
                newName: "rating_avg");

            migrationBuilder.RenameColumn(
                name: "PostalCode",
                table: "spaza_shops",
                newName: "postal_code");

            migrationBuilder.RenameColumn(
                name: "OwnerName",
                table: "spaza_shops",
                newName: "owner_name");

            migrationBuilder.RenameColumn(
                name: "OwnerIdNumber",
                table: "spaza_shops",
                newName: "owner_id_number");

            migrationBuilder.RenameColumn(
                name: "OnboardingFeeRef",
                table: "spaza_shops",
                newName: "onboarding_fee_ref");

            migrationBuilder.RenameColumn(
                name: "OnboardingFeePaid",
                table: "spaza_shops",
                newName: "onboarding_fee_paid");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "spaza_shops",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ComplianceStatus",
                table: "spaza_shops",
                newName: "compliance_status");

            migrationBuilder.RenameIndex(
                name: "IX_SpazaShops_UserId",
                table: "spaza_shops",
                newName: "ix_spaza_shops_user_id");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "shop_documents",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "shop_documents",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "VerifiedBy",
                table: "shop_documents",
                newName: "verified_by");

            migrationBuilder.RenameColumn(
                name: "VerifiedAt",
                table: "shop_documents",
                newName: "verified_at");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "shop_documents",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ShopId",
                table: "shop_documents",
                newName: "shop_id");

            migrationBuilder.RenameColumn(
                name: "RejectionNote",
                table: "shop_documents",
                newName: "rejection_note");

            migrationBuilder.RenameColumn(
                name: "GuidanceUrl",
                table: "shop_documents",
                newName: "guidance_url");

            migrationBuilder.RenameColumn(
                name: "ExpiryDate",
                table: "shop_documents",
                newName: "expiry_date");

            migrationBuilder.RenameColumn(
                name: "DocUrl",
                table: "shop_documents",
                newName: "doc_url");

            migrationBuilder.RenameColumn(
                name: "DocType",
                table: "shop_documents",
                newName: "doc_type");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "shop_documents",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_ShopDocuments_ShopId",
                table: "shop_documents",
                newName: "ix_shop_documents_shop_id");

            migrationBuilder.RenameColumn(
                name: "PermissionId",
                table: "role_permissions",
                newName: "permission_id");

            migrationBuilder.RenameColumn(
                name: "RoleId",
                table: "role_permissions",
                newName: "role_id");

            migrationBuilder.RenameIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "role_permissions",
                newName: "ix_role_permissions_permission_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "refresh_tokens",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "refresh_tokens",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "refresh_tokens",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TokenHash",
                table: "refresh_tokens",
                newName: "token_hash");

            migrationBuilder.RenameColumn(
                name: "RevokedAt",
                table: "refresh_tokens",
                newName: "revoked_at");

            migrationBuilder.RenameColumn(
                name: "IpAddress",
                table: "refresh_tokens",
                newName: "ip_address");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "refresh_tokens",
                newName: "expires_at");

            migrationBuilder.RenameColumn(
                name: "DeviceInfo",
                table: "refresh_tokens",
                newName: "device_info");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "refresh_tokens",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshTokens_UserId",
                table: "refresh_tokens",
                newName: "ix_refresh_tokens_user_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "product_qr_codes",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "product_qr_codes",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ScannedAt",
                table: "product_qr_codes",
                newName: "scanned_at");

            migrationBuilder.RenameColumn(
                name: "QrCode",
                table: "product_qr_codes",
                newName: "qr_code");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "product_qr_codes",
                newName: "product_id");

            migrationBuilder.RenameColumn(
                name: "OrderItemId",
                table: "product_qr_codes",
                newName: "order_item_id");

            migrationBuilder.RenameColumn(
                name: "IsScanned",
                table: "product_qr_codes",
                newName: "is_scanned");

            migrationBuilder.RenameColumn(
                name: "IsRecalled",
                table: "product_qr_codes",
                newName: "is_recalled");

            migrationBuilder.RenameColumn(
                name: "ExpiryDate",
                table: "product_qr_codes",
                newName: "expiry_date");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "product_qr_codes",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "BatchNumber",
                table: "product_qr_codes",
                newName: "batch_number");

            migrationBuilder.RenameIndex(
                name: "IX_ProductQrCodes_QrCode",
                table: "product_qr_codes",
                newName: "ix_product_qr_codes_qr_code");

            migrationBuilder.RenameColumn(
                name: "Purpose",
                table: "otp_codes",
                newName: "purpose");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "otp_codes",
                newName: "phone");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "otp_codes",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "otp_codes",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "otp_codes",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "otp_codes",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UsedAt",
                table: "otp_codes",
                newName: "used_at");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "otp_codes",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "otp_codes",
                newName: "expires_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "otp_codes",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_OtpCodes_UserId",
                table: "otp_codes",
                newName: "ix_otp_codes_user_id");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "order_items",
                newName: "quantity");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "order_items",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "order_items",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                table: "order_items",
                newName: "unit_price");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "order_items",
                newName: "product_id");

            migrationBuilder.RenameColumn(
                name: "OrderId",
                table: "order_items",
                newName: "order_id");

            migrationBuilder.RenameColumn(
                name: "LineTotal",
                table: "order_items",
                newName: "line_total");

            migrationBuilder.RenameColumn(
                name: "DiscountPct",
                table: "order_items",
                newName: "discount_pct");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "order_items",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_OrderItems_ProductId",
                table: "order_items",
                newName: "ix_order_items_product_id");

            migrationBuilder.RenameIndex(
                name: "IX_OrderItems_OrderId",
                table: "order_items",
                newName: "ix_order_items_order_id");

            migrationBuilder.RenameColumn(
                name: "Event",
                table: "auth_audit_logs",
                newName: "event");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "auth_audit_logs",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "auth_audit_logs",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "UserAgent",
                table: "auth_audit_logs",
                newName: "user_agent");

            migrationBuilder.RenameColumn(
                name: "IpAddress",
                table: "auth_audit_logs",
                newName: "ip_address");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "auth_audit_logs",
                newName: "created_at");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "suppliers",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "suppliers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "suppliers",
                type: "double precision",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "roles",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "products",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "allergens",
                table: "products",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "permissions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_distance_km",
                table: "orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "delivery_latitude",
                table: "orders",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "delivery_longitude",
                table: "orders",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_buy_participant_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                table: "orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "categories",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "supplier_documents",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "spaza_shops",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "shop_documents",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "product_qr_codes",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "otp_codes",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "order_items",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<decimal>(
                name: "original_unit_price",
                table: "order_items",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "pk_users",
                table: "users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_suppliers",
                table: "suppliers",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_roles",
                table: "roles",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_products",
                table: "products",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_permissions",
                table: "permissions",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_orders",
                table: "orders",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_categories",
                table: "categories",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_supplier_documents",
                table: "supplier_documents",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_spaza_shops",
                table: "spaza_shops",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_shop_documents",
                table: "shop_documents",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_role_permissions",
                table: "role_permissions",
                columns: new[] { "role_id", "permission_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_refresh_tokens",
                table: "refresh_tokens",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_product_qr_codes",
                table: "product_qr_codes",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_otp_codes",
                table: "otp_codes",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_order_items",
                table: "order_items",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_auth_audit_logs",
                table: "auth_audit_logs",
                column: "id");

            migrationBuilder.CreateTable(
                name: "customer_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    age = table.Column<int>(type: "integer", nullable: true),
                    allergies = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_reward_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_reward_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_reward_transactions_users_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_scan_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    points_awarded = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_scan_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_scan_events_product_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_customer_scan_events_users_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    redeemed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    redeemed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_vouchers", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_vouchers_users_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_buys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_qty = table.Column<int>(type: "integer", nullable: false),
                    current_qty = table.Column<int>(type: "integer", nullable: false),
                    original_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    discount_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    discount_pct = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_by_shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_buys", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_buys_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_buys_spaza_shops_created_by_shop_id",
                        column: x => x.created_by_shop_id,
                        principalTable: "spaza_shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_buys_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "platform_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    barcode = table.Column<string>(type: "text", nullable: true),
                    report_type = table.Column<string>(type: "text", nullable: true),
                    shop_name = table.Column<string>(type: "text", nullable: true),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    photo_url = table.Column<string>(type: "text", nullable: true),
                    batch_number = table.Column<string>(type: "text", nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    purchase_location = table.Column<string>(type: "text", nullable: true),
                    supplier_name = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    escalated_to = table.Column<string>(type: "text", nullable: true),
                    escalated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_reports_product_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_reports_users_reporter_user_id",
                        column: x => x.reporter_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_onboarding_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    pay_fast_payment_id = table.Column<string>(type: "text", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_onboarding_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_shop_onboarding_payments_spaza_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "spaza_shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_reviews", x => x.id);
                    table.ForeignKey(
                        name: "fk_shop_reviews_spaza_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "spaza_shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shop_reviews_users_reviewer_user_id",
                        column: x => x.reviewer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_wallet_transaction",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_wallet_transaction", x => x.id);
                    table.ForeignKey(
                        name: "fk_shop_wallet_transaction_spaza_shop_shop_id",
                        column: x => x.shop_id,
                        principalTable: "spaza_shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    monthly_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    annual_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    commission_rate = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    max_listings = table.Column<int>(type: "integer", nullable: false),
                    max_orders = table.Column<int>(type: "integer", nullable: false),
                    has_analytics = table.Column<bool>(type: "boolean", nullable: false),
                    has_priority_support = table.Column<bool>(type: "boolean", nullable: false),
                    has_bulk_pricing = table.Column<bool>(type: "boolean", nullable: false),
                    has_api_access = table.Column<bool>(type: "boolean", nullable: false),
                    has_custom_branding = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_buy_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_buy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_buy_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_buy_participants_group_buys_group_buy_id",
                        column: x => x.group_buy_id,
                        principalTable: "group_buys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_buy_participants_spaza_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "spaza_shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_buy_products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_buy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    discount_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    discount_pct = table.Column<int>(type: "integer", nullable: false),
                    target_qty = table.Column<int>(type: "integer", nullable: false),
                    current_qty = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_buy_products", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_buy_products_group_buys_group_buy_id",
                        column: x => x.group_buy_id,
                        principalTable: "group_buys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_buy_products_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_cycle = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_billing_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    amount_paid = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    payment_reference = table.Column<string>(type: "text", nullable: true),
                    payment_method = table.Column<string>(type: "text", nullable: true),
                    cancelled_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_subscriptions_subscription_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "subscription_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_supplier_subscriptions_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_buy_participant_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_buy_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_buy_participant_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_buy_participant_items_group_buy_participants_particip",
                        column: x => x.participant_id,
                        principalTable: "group_buy_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_buy_participant_items_group_buy_products_group_buy_pr",
                        column: x => x.group_buy_product_id,
                        principalTable: "group_buy_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_group_buy_id_shop_id",
                table: "orders",
                columns: new[] { "group_buy_id", "shop_id" },
                unique: true,
                filter: "group_buy_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_orders_group_buy_participant_id",
                table: "orders",
                column: "group_buy_participant_id",
                unique: true,
                filter: "group_buy_participant_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ProductQrCodes_DefaultProduct",
                table: "product_qr_codes",
                column: "product_id",
                unique: true,
                filter: "order_item_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_customer_profiles_user_id",
                table: "customer_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_reward_transactions_customer_user_id_created_at",
                table: "customer_reward_transactions",
                columns: new[] { "customer_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_scan_events_customer_user_id_code_created_at",
                table: "customer_scan_events",
                columns: new[] { "customer_user_id", "code", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_customer_scan_events_product_id",
                table: "customer_scan_events",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_vouchers_code",
                table: "customer_vouchers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_vouchers_customer_user_id_status",
                table: "customer_vouchers",
                columns: new[] { "customer_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_group_buy_participant_items_group_buy_product_id",
                table: "group_buy_participant_items",
                column: "group_buy_product_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_buy_participant_items_participant_id_group_buy_produc",
                table: "group_buy_participant_items",
                columns: new[] { "participant_id", "group_buy_product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_buy_participants_group_buy_id_shop_id",
                table: "group_buy_participants",
                columns: new[] { "group_buy_id", "shop_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_buy_participants_shop_id",
                table: "group_buy_participants",
                column: "shop_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_buy_products_group_buy_id_product_id",
                table: "group_buy_products",
                columns: new[] { "group_buy_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_buy_products_product_id",
                table: "group_buy_products",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_buy_products_status",
                table: "group_buy_products",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_group_buys_created_by_shop_id",
                table: "group_buys",
                column: "created_by_shop_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_buys_expires_at",
                table: "group_buys",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_group_buys_product_id",
                table: "group_buys",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_buys_status",
                table: "group_buys",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_group_buys_status_expires_at",
                table: "group_buys",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_group_buys_supplier_id",
                table: "group_buys",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_settings_key",
                table: "platform_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reports_product_id",
                table: "reports",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_user_id",
                table: "reports",
                column: "reporter_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_status",
                table: "reports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_shop_onboarding_payments_pay_fast_payment_id",
                table: "shop_onboarding_payments",
                column: "pay_fast_payment_id",
                unique: true,
                filter: "pay_fast_payment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_shop_onboarding_payments_shop_id",
                table: "shop_onboarding_payments",
                column: "shop_id",
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_shop_onboarding_payments_status",
                table: "shop_onboarding_payments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_shop_reviews_reviewer_user_id",
                table: "shop_reviews",
                column: "reviewer_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_shop_reviews_shop_id_reviewer_user_id",
                table: "shop_reviews",
                columns: new[] { "shop_id", "reviewer_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shop_wallet_transaction_shop_id",
                table: "shop_wallet_transaction",
                column: "shop_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscription_plans_tier",
                table: "subscription_plans",
                column: "tier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_subscriptions_plan_id",
                table: "supplier_subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_subscriptions_supplier_id",
                table: "supplier_subscriptions",
                column: "supplier_id");

            migrationBuilder.AddForeignKey(
                name: "fk_categories_categories_parent_id",
                table: "categories",
                column: "parent_id",
                principalTable: "categories",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_order_items_orders_order_id",
                table: "order_items",
                column: "order_id",
                principalTable: "orders",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_order_items_products_product_id",
                table: "order_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_orders_group_buy_group_buy_id",
                table: "orders",
                column: "group_buy_id",
                principalTable: "group_buys",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_orders_group_buy_participant_group_buy_participant_id",
                table: "orders",
                column: "group_buy_participant_id",
                principalTable: "group_buy_participants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_orders_spaza_shops_shop_id",
                table: "orders",
                column: "shop_id",
                principalTable: "spaza_shops",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_orders_suppliers_supplier_id",
                table: "orders",
                column: "supplier_id",
                principalTable: "suppliers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_otp_codes_users_user_id",
                table: "otp_codes",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_product_qr_codes_products_product_id",
                table: "product_qr_codes",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_products_categories_category_id",
                table: "products",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_products_suppliers_supplier_id",
                table: "products",
                column: "supplier_id",
                principalTable: "suppliers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_refresh_tokens_users_user_id",
                table: "refresh_tokens",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_role_permissions_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id",
                principalTable: "permissions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_role_permissions_roles_role_id",
                table: "role_permissions",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_shop_documents_spaza_shops_shop_id",
                table: "shop_documents",
                column: "shop_id",
                principalTable: "spaza_shops",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_spaza_shops_users_user_id",
                table: "spaza_shops",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_supplier_documents_suppliers_supplier_id",
                table: "supplier_documents",
                column: "supplier_id",
                principalTable: "suppliers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_suppliers_users_user_id",
                table: "suppliers",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_users_roles_role_id",
                table: "users",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_categories_categories_parent_id",
                table: "categories");

            migrationBuilder.DropForeignKey(
                name: "fk_order_items_orders_order_id",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "fk_order_items_products_product_id",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "fk_orders_group_buy_group_buy_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_orders_group_buy_participant_group_buy_participant_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_orders_spaza_shops_shop_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_orders_suppliers_supplier_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_otp_codes_users_user_id",
                table: "otp_codes");

            migrationBuilder.DropForeignKey(
                name: "fk_product_qr_codes_products_product_id",
                table: "product_qr_codes");

            migrationBuilder.DropForeignKey(
                name: "fk_products_categories_category_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "fk_products_suppliers_supplier_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "fk_refresh_tokens_users_user_id",
                table: "refresh_tokens");

            migrationBuilder.DropForeignKey(
                name: "fk_role_permissions_permissions_permission_id",
                table: "role_permissions");

            migrationBuilder.DropForeignKey(
                name: "fk_role_permissions_roles_role_id",
                table: "role_permissions");

            migrationBuilder.DropForeignKey(
                name: "fk_shop_documents_spaza_shops_shop_id",
                table: "shop_documents");

            migrationBuilder.DropForeignKey(
                name: "fk_spaza_shops_users_user_id",
                table: "spaza_shops");

            migrationBuilder.DropForeignKey(
                name: "fk_supplier_documents_suppliers_supplier_id",
                table: "supplier_documents");

            migrationBuilder.DropForeignKey(
                name: "fk_suppliers_users_user_id",
                table: "suppliers");

            migrationBuilder.DropForeignKey(
                name: "fk_users_roles_role_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "customer_profiles");

            migrationBuilder.DropTable(
                name: "customer_reward_transactions");

            migrationBuilder.DropTable(
                name: "customer_scan_events");

            migrationBuilder.DropTable(
                name: "customer_vouchers");

            migrationBuilder.DropTable(
                name: "group_buy_participant_items");

            migrationBuilder.DropTable(
                name: "platform_settings");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "shop_onboarding_payments");

            migrationBuilder.DropTable(
                name: "shop_reviews");

            migrationBuilder.DropTable(
                name: "shop_wallet_transaction");

            migrationBuilder.DropTable(
                name: "supplier_subscriptions");

            migrationBuilder.DropTable(
                name: "group_buy_participants");

            migrationBuilder.DropTable(
                name: "group_buy_products");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropTable(
                name: "group_buys");

            migrationBuilder.DropPrimaryKey(
                name: "pk_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "pk_suppliers",
                table: "suppliers");

            migrationBuilder.DropPrimaryKey(
                name: "pk_roles",
                table: "roles");

            migrationBuilder.DropPrimaryKey(
                name: "pk_products",
                table: "products");

            migrationBuilder.DropPrimaryKey(
                name: "pk_permissions",
                table: "permissions");

            migrationBuilder.DropPrimaryKey(
                name: "pk_orders",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_group_buy_id_shop_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_group_buy_participant_id",
                table: "orders");

            migrationBuilder.DropPrimaryKey(
                name: "pk_categories",
                table: "categories");

            migrationBuilder.DropPrimaryKey(
                name: "pk_supplier_documents",
                table: "supplier_documents");

            migrationBuilder.DropPrimaryKey(
                name: "pk_spaza_shops",
                table: "spaza_shops");

            migrationBuilder.DropPrimaryKey(
                name: "pk_shop_documents",
                table: "shop_documents");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role_permissions",
                table: "role_permissions");

            migrationBuilder.DropPrimaryKey(
                name: "pk_refresh_tokens",
                table: "refresh_tokens");

            migrationBuilder.DropPrimaryKey(
                name: "pk_product_qr_codes",
                table: "product_qr_codes");

            migrationBuilder.DropIndex(
                name: "UX_ProductQrCodes_DefaultProduct",
                table: "product_qr_codes");

            migrationBuilder.DropPrimaryKey(
                name: "pk_otp_codes",
                table: "otp_codes");

            migrationBuilder.DropPrimaryKey(
                name: "pk_order_items",
                table: "order_items");

            migrationBuilder.DropPrimaryKey(
                name: "pk_auth_audit_logs",
                table: "auth_audit_logs");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "allergens",
                table: "products");

            migrationBuilder.DropColumn(
                name: "delivery_distance_km",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "delivery_latitude",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "delivery_longitude",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "group_buy_participant_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "original_unit_price",
                table: "order_items");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "suppliers",
                newName: "Suppliers");

            migrationBuilder.RenameTable(
                name: "roles",
                newName: "Roles");

            migrationBuilder.RenameTable(
                name: "products",
                newName: "Products");

            migrationBuilder.RenameTable(
                name: "permissions",
                newName: "Permissions");

            migrationBuilder.RenameTable(
                name: "orders",
                newName: "Orders");

            migrationBuilder.RenameTable(
                name: "categories",
                newName: "Categories");

            migrationBuilder.RenameTable(
                name: "supplier_documents",
                newName: "SupplierDocuments");

            migrationBuilder.RenameTable(
                name: "spaza_shops",
                newName: "SpazaShops");

            migrationBuilder.RenameTable(
                name: "shop_documents",
                newName: "ShopDocuments");

            migrationBuilder.RenameTable(
                name: "role_permissions",
                newName: "RolePermissions");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                newName: "RefreshTokens");

            migrationBuilder.RenameTable(
                name: "product_qr_codes",
                newName: "ProductQrCodes");

            migrationBuilder.RenameTable(
                name: "otp_codes",
                newName: "OtpCodes");

            migrationBuilder.RenameTable(
                name: "order_items",
                newName: "OrderItems");

            migrationBuilder.RenameTable(
                name: "auth_audit_logs",
                newName: "AuthAuditLogs");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "Users",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "Users",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "Users",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Users",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "role_id",
                table: "Users",
                newName: "RoleId");

            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "Users",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "mfa_secret",
                table: "Users",
                newName: "MfaSecret");

            migrationBuilder.RenameColumn(
                name: "mfa_enabled",
                table: "Users",
                newName: "MfaEnabled");

            migrationBuilder.RenameColumn(
                name: "locked_until",
                table: "Users",
                newName: "LockedUntil");

            migrationBuilder.RenameColumn(
                name: "last_login_at",
                table: "Users",
                newName: "LastLoginAt");

            migrationBuilder.RenameColumn(
                name: "failed_attempts",
                table: "Users",
                newName: "FailedAttempts");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Users",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_users_phone",
                table: "Users",
                newName: "IX_Users_Phone");

            migrationBuilder.RenameIndex(
                name: "ix_users_email",
                table: "Users",
                newName: "IX_Users_Email");

            migrationBuilder.RenameIndex(
                name: "ix_users_role_id",
                table: "Users",
                newName: "IX_Users_RoleId");

            migrationBuilder.RenameColumn(
                name: "tier",
                table: "Suppliers",
                newName: "Tier");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "Suppliers",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "province",
                table: "Suppliers",
                newName: "Province");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "Suppliers",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "Suppliers",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "city",
                table: "Suppliers",
                newName: "City");

            migrationBuilder.RenameColumn(
                name: "address",
                table: "Suppliers",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Suppliers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "vat_number",
                table: "Suppliers",
                newName: "VatNumber");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "Suppliers",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Suppliers",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "registration_number",
                table: "Suppliers",
                newName: "RegistrationNumber");

            migrationBuilder.RenameColumn(
                name: "postal_code",
                table: "Suppliers",
                newName: "PostalCode");

            migrationBuilder.RenameColumn(
                name: "logo_url",
                table: "Suppliers",
                newName: "LogoUrl");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Suppliers",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "contact_person",
                table: "Suppliers",
                newName: "ContactPerson");

            migrationBuilder.RenameColumn(
                name: "company_name",
                table: "Suppliers",
                newName: "CompanyName");

            migrationBuilder.RenameColumn(
                name: "commission_rate",
                table: "Suppliers",
                newName: "CommissionRate");

            migrationBuilder.RenameColumn(
                name: "bank_name",
                table: "Suppliers",
                newName: "BankName");

            migrationBuilder.RenameColumn(
                name: "bank_branch_code",
                table: "Suppliers",
                newName: "BankBranchCode");

            migrationBuilder.RenameColumn(
                name: "bank_account",
                table: "Suppliers",
                newName: "BankAccount");

            migrationBuilder.RenameIndex(
                name: "ix_suppliers_user_id",
                table: "Suppliers",
                newName: "IX_Suppliers_UserId");

            migrationBuilder.RenameIndex(
                name: "ix_suppliers_registration_number",
                table: "Suppliers",
                newName: "IX_Suppliers_RegistrationNumber");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "Roles",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "Roles",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Roles",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Roles",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "Roles",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Roles",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_roles_name",
                table: "Roles",
                newName: "IX_Roles_Name");

            migrationBuilder.RenameColumn(
                name: "unit",
                table: "Products",
                newName: "Unit");

            migrationBuilder.RenameColumn(
                name: "sku",
                table: "Products",
                newName: "Sku");

            migrationBuilder.RenameColumn(
                name: "price",
                table: "Products",
                newName: "Price");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "Products",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "images",
                table: "Products",
                newName: "Images");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "Products",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "barcode",
                table: "Products",
                newName: "Barcode");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Products",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Products",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "Products",
                newName: "SupplierId");

            migrationBuilder.RenameColumn(
                name: "stock_qty",
                table: "Products",
                newName: "StockQty");

            migrationBuilder.RenameColumn(
                name: "min_order_qty",
                table: "Products",
                newName: "MinOrderQty");

            migrationBuilder.RenameColumn(
                name: "is_food_item",
                table: "Products",
                newName: "IsFoodItem");

            migrationBuilder.RenameColumn(
                name: "is_available",
                table: "Products",
                newName: "IsAvailable");

            migrationBuilder.RenameColumn(
                name: "is_approved",
                table: "Products",
                newName: "IsApproved");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Products",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "category_id",
                table: "Products",
                newName: "CategoryId");

            migrationBuilder.RenameColumn(
                name: "approved_by",
                table: "Products",
                newName: "ApprovedBy");

            migrationBuilder.RenameColumn(
                name: "approved_at",
                table: "Products",
                newName: "ApprovedAt");

            migrationBuilder.RenameIndex(
                name: "ix_products_supplier_id_sku",
                table: "Products",
                newName: "IX_Products_SupplierId_Sku");

            migrationBuilder.RenameIndex(
                name: "ix_products_category_id",
                table: "Products",
                newName: "IX_Products_CategoryId");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "Permissions",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "module",
                table: "Permissions",
                newName: "Module");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "Permissions",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Permissions",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Permissions",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Permissions",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_permissions_name",
                table: "Permissions",
                newName: "IX_Permissions_Name");

            migrationBuilder.RenameColumn(
                name: "subtotal",
                table: "Orders",
                newName: "Subtotal");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "Orders",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "notes",
                table: "Orders",
                newName: "Notes");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Orders",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Orders",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "total_amount",
                table: "Orders",
                newName: "TotalAmount");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "Orders",
                newName: "SupplierId");

            migrationBuilder.RenameColumn(
                name: "shop_id",
                table: "Orders",
                newName: "ShopId");

            migrationBuilder.RenameColumn(
                name: "rejection_reason",
                table: "Orders",
                newName: "RejectionReason");

            migrationBuilder.RenameColumn(
                name: "platform_commission",
                table: "Orders",
                newName: "PlatformCommission");

            migrationBuilder.RenameColumn(
                name: "order_number",
                table: "Orders",
                newName: "OrderNumber");

            migrationBuilder.RenameColumn(
                name: "group_buy_id",
                table: "Orders",
                newName: "GroupBuyId");

            migrationBuilder.RenameColumn(
                name: "delivery_type",
                table: "Orders",
                newName: "DeliveryType");

            migrationBuilder.RenameColumn(
                name: "delivery_markup",
                table: "Orders",
                newName: "DeliveryMarkup");

            migrationBuilder.RenameColumn(
                name: "delivery_fee",
                table: "Orders",
                newName: "DeliveryFee");

            migrationBuilder.RenameColumn(
                name: "delivery_address",
                table: "Orders",
                newName: "DeliveryAddress");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Orders",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_orders_supplier_id",
                table: "Orders",
                newName: "IX_Orders_SupplierId");

            migrationBuilder.RenameIndex(
                name: "ix_orders_shop_id",
                table: "Orders",
                newName: "IX_Orders_ShopId");

            migrationBuilder.RenameIndex(
                name: "ix_orders_order_number",
                table: "Orders",
                newName: "IX_Orders_OrderNumber");

            migrationBuilder.RenameColumn(
                name: "slug",
                table: "Categories",
                newName: "Slug");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "Categories",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Categories",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Categories",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "sort_order",
                table: "Categories",
                newName: "SortOrder");

            migrationBuilder.RenameColumn(
                name: "parent_id",
                table: "Categories",
                newName: "ParentId");

            migrationBuilder.RenameColumn(
                name: "is_active",
                table: "Categories",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "icon_url",
                table: "Categories",
                newName: "IconUrl");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Categories",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_categories_parent_id",
                table: "Categories",
                newName: "IX_Categories_ParentId");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "SupplierDocuments",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "SupplierDocuments",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "verified_by",
                table: "SupplierDocuments",
                newName: "VerifiedBy");

            migrationBuilder.RenameColumn(
                name: "verified_at",
                table: "SupplierDocuments",
                newName: "VerifiedAt");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "SupplierDocuments",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "supplier_id",
                table: "SupplierDocuments",
                newName: "SupplierId");

            migrationBuilder.RenameColumn(
                name: "rejection_note",
                table: "SupplierDocuments",
                newName: "RejectionNote");

            migrationBuilder.RenameColumn(
                name: "expiry_date",
                table: "SupplierDocuments",
                newName: "ExpiryDate");

            migrationBuilder.RenameColumn(
                name: "doc_url",
                table: "SupplierDocuments",
                newName: "DocUrl");

            migrationBuilder.RenameColumn(
                name: "doc_type",
                table: "SupplierDocuments",
                newName: "DocType");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "SupplierDocuments",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_supplier_documents_supplier_id",
                table: "SupplierDocuments",
                newName: "IX_SupplierDocuments_SupplierId");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "SpazaShops",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "province",
                table: "SpazaShops",
                newName: "Province");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "SpazaShops",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "longitude",
                table: "SpazaShops",
                newName: "Longitude");

            migrationBuilder.RenameColumn(
                name: "latitude",
                table: "SpazaShops",
                newName: "Latitude");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "SpazaShops",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "city",
                table: "SpazaShops",
                newName: "City");

            migrationBuilder.RenameColumn(
                name: "address",
                table: "SpazaShops",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "SpazaShops",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "SpazaShops",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "SpazaShops",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "shop_name",
                table: "SpazaShops",
                newName: "ShopName");

            migrationBuilder.RenameColumn(
                name: "rating_count",
                table: "SpazaShops",
                newName: "RatingCount");

            migrationBuilder.RenameColumn(
                name: "rating_avg",
                table: "SpazaShops",
                newName: "RatingAvg");

            migrationBuilder.RenameColumn(
                name: "postal_code",
                table: "SpazaShops",
                newName: "PostalCode");

            migrationBuilder.RenameColumn(
                name: "owner_name",
                table: "SpazaShops",
                newName: "OwnerName");

            migrationBuilder.RenameColumn(
                name: "owner_id_number",
                table: "SpazaShops",
                newName: "OwnerIdNumber");

            migrationBuilder.RenameColumn(
                name: "onboarding_fee_ref",
                table: "SpazaShops",
                newName: "OnboardingFeeRef");

            migrationBuilder.RenameColumn(
                name: "onboarding_fee_paid",
                table: "SpazaShops",
                newName: "OnboardingFeePaid");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "SpazaShops",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "compliance_status",
                table: "SpazaShops",
                newName: "ComplianceStatus");

            migrationBuilder.RenameIndex(
                name: "ix_spaza_shops_user_id",
                table: "SpazaShops",
                newName: "IX_SpazaShops_UserId");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "ShopDocuments",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ShopDocuments",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "verified_by",
                table: "ShopDocuments",
                newName: "VerifiedBy");

            migrationBuilder.RenameColumn(
                name: "verified_at",
                table: "ShopDocuments",
                newName: "VerifiedAt");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "ShopDocuments",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "shop_id",
                table: "ShopDocuments",
                newName: "ShopId");

            migrationBuilder.RenameColumn(
                name: "rejection_note",
                table: "ShopDocuments",
                newName: "RejectionNote");

            migrationBuilder.RenameColumn(
                name: "guidance_url",
                table: "ShopDocuments",
                newName: "GuidanceUrl");

            migrationBuilder.RenameColumn(
                name: "expiry_date",
                table: "ShopDocuments",
                newName: "ExpiryDate");

            migrationBuilder.RenameColumn(
                name: "doc_url",
                table: "ShopDocuments",
                newName: "DocUrl");

            migrationBuilder.RenameColumn(
                name: "doc_type",
                table: "ShopDocuments",
                newName: "DocType");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "ShopDocuments",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_shop_documents_shop_id",
                table: "ShopDocuments",
                newName: "IX_ShopDocuments_ShopId");

            migrationBuilder.RenameColumn(
                name: "permission_id",
                table: "RolePermissions",
                newName: "PermissionId");

            migrationBuilder.RenameColumn(
                name: "role_id",
                table: "RolePermissions",
                newName: "RoleId");

            migrationBuilder.RenameIndex(
                name: "ix_role_permissions_permission_id",
                table: "RolePermissions",
                newName: "IX_RolePermissions_PermissionId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "RefreshTokens",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "RefreshTokens",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "RefreshTokens",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "token_hash",
                table: "RefreshTokens",
                newName: "TokenHash");

            migrationBuilder.RenameColumn(
                name: "revoked_at",
                table: "RefreshTokens",
                newName: "RevokedAt");

            migrationBuilder.RenameColumn(
                name: "ip_address",
                table: "RefreshTokens",
                newName: "IpAddress");

            migrationBuilder.RenameColumn(
                name: "expires_at",
                table: "RefreshTokens",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "device_info",
                table: "RefreshTokens",
                newName: "DeviceInfo");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "RefreshTokens",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_refresh_tokens_user_id",
                table: "RefreshTokens",
                newName: "IX_RefreshTokens_UserId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ProductQrCodes",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "ProductQrCodes",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "scanned_at",
                table: "ProductQrCodes",
                newName: "ScannedAt");

            migrationBuilder.RenameColumn(
                name: "qr_code",
                table: "ProductQrCodes",
                newName: "QrCode");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "ProductQrCodes",
                newName: "ProductId");

            migrationBuilder.RenameColumn(
                name: "order_item_id",
                table: "ProductQrCodes",
                newName: "OrderItemId");

            migrationBuilder.RenameColumn(
                name: "is_scanned",
                table: "ProductQrCodes",
                newName: "IsScanned");

            migrationBuilder.RenameColumn(
                name: "is_recalled",
                table: "ProductQrCodes",
                newName: "IsRecalled");

            migrationBuilder.RenameColumn(
                name: "expiry_date",
                table: "ProductQrCodes",
                newName: "ExpiryDate");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "ProductQrCodes",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "batch_number",
                table: "ProductQrCodes",
                newName: "BatchNumber");

            migrationBuilder.RenameIndex(
                name: "ix_product_qr_codes_qr_code",
                table: "ProductQrCodes",
                newName: "IX_ProductQrCodes_QrCode");

            migrationBuilder.RenameColumn(
                name: "purpose",
                table: "OtpCodes",
                newName: "Purpose");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "OtpCodes",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "OtpCodes",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "code",
                table: "OtpCodes",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "OtpCodes",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "OtpCodes",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "used_at",
                table: "OtpCodes",
                newName: "UsedAt");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "OtpCodes",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "expires_at",
                table: "OtpCodes",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "OtpCodes",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_otp_codes_user_id",
                table: "OtpCodes",
                newName: "IX_OtpCodes_UserId");

            migrationBuilder.RenameColumn(
                name: "quantity",
                table: "OrderItems",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "OrderItems",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "OrderItems",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "unit_price",
                table: "OrderItems",
                newName: "UnitPrice");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "OrderItems",
                newName: "ProductId");

            migrationBuilder.RenameColumn(
                name: "order_id",
                table: "OrderItems",
                newName: "OrderId");

            migrationBuilder.RenameColumn(
                name: "line_total",
                table: "OrderItems",
                newName: "LineTotal");

            migrationBuilder.RenameColumn(
                name: "discount_pct",
                table: "OrderItems",
                newName: "DiscountPct");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "OrderItems",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_order_items_product_id",
                table: "OrderItems",
                newName: "IX_OrderItems_ProductId");

            migrationBuilder.RenameIndex(
                name: "ix_order_items_order_id",
                table: "OrderItems",
                newName: "IX_OrderItems_OrderId");

            migrationBuilder.RenameColumn(
                name: "event",
                table: "AuthAuditLogs",
                newName: "Event");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "AuthAuditLogs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "AuthAuditLogs",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "user_agent",
                table: "AuthAuditLogs",
                newName: "UserAgent");

            migrationBuilder.RenameColumn(
                name: "ip_address",
                table: "AuthAuditLogs",
                newName: "IpAddress");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "AuthAuditLogs",
                newName: "CreatedAt");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Suppliers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Products",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Permissions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Categories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "SupplierDocuments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "SpazaShops",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ShopDocuments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ProductQrCodes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "OtpCodes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "OrderItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Suppliers",
                table: "Suppliers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Roles",
                table: "Roles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Products",
                table: "Products",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Permissions",
                table: "Permissions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Orders",
                table: "Orders",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Categories",
                table: "Categories",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SupplierDocuments",
                table: "SupplierDocuments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SpazaShops",
                table: "SpazaShops",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShopDocuments",
                table: "ShopDocuments",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RolePermissions",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_RefreshTokens",
                table: "RefreshTokens",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductQrCodes",
                table: "ProductQrCodes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OtpCodes",
                table: "OtpCodes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrderItems",
                table: "OrderItems",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuthAuditLogs",
                table: "AuthAuditLogs",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ProductQrCodes_ProductId",
                table: "ProductQrCodes",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentId",
                table: "Categories",
                column: "ParentId",
                principalTable: "Categories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Orders_OrderId",
                table: "OrderItems",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_SpazaShops_ShopId",
                table: "Orders",
                column: "ShopId",
                principalTable: "SpazaShops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Suppliers_SupplierId",
                table: "Orders",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OtpCodes_Users_UserId",
                table: "OtpCodes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductQrCodes_Products_ProductId",
                table: "ProductQrCodes",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Suppliers_SupplierId",
                table: "Products",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Users_UserId",
                table: "RefreshTokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Permissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId",
                principalTable: "Permissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Roles_RoleId",
                table: "RolePermissions",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ShopDocuments_SpazaShops_ShopId",
                table: "ShopDocuments",
                column: "ShopId",
                principalTable: "SpazaShops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SpazaShops_Users_UserId",
                table: "SpazaShops",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierDocuments_Suppliers_SupplierId",
                table: "SupplierDocuments",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Users_UserId",
                table: "Suppliers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

public partial class ReplaceProviderSpecificPaymentReferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_orders_stripe_checkout_session_id",
            table: "orders");
        migrationBuilder.DropIndex(
            name: "ix_supplier_subscriptions_stripe_checkout_session_id",
            table: "supplier_subscriptions");
        migrationBuilder.DropIndex(
            name: "ix_supplier_subscriptions_stripe_subscription_id",
            table: "supplier_subscriptions");
        migrationBuilder.DropIndex(
            name: "ix_customer_subscriptions_stripe_checkout_session_id",
            table: "customer_subscriptions");
        migrationBuilder.DropIndex(
            name: "ix_customer_subscriptions_stripe_subscription_id",
            table: "customer_subscriptions");

        migrationBuilder.RenameColumn(
            name: "stripe_checkout_id",
            table: "orders",
            newName: "payment_checkout_id");
        migrationBuilder.RenameIndex(
            name: "ix_orders_stripe_checkout_id",
            table: "orders",
            newName: "ix_orders_payment_checkout_id");
        migrationBuilder.DropColumn(
            name: "stripe_checkout_session_id",
            table: "orders");
        migrationBuilder.AddColumn<string>(
            name: "payment_provider_reference",
            table: "orders",
            type: "text",
            nullable: true);
        migrationBuilder.CreateIndex(
            name: "ix_orders_payment_provider_reference",
            table: "orders",
            column: "payment_provider_reference");

        DropLegacySubscriptionColumns(migrationBuilder, "customer_subscriptions");
        DropLegacySubscriptionColumns(migrationBuilder, "supplier_subscriptions");
        AddProviderSubscriptionColumns(migrationBuilder, "customer_subscriptions");
        AddProviderSubscriptionColumns(migrationBuilder, "supplier_subscriptions");

        CreateProviderSubscriptionIndexes(migrationBuilder, "customer_subscriptions");
        CreateProviderSubscriptionIndexes(migrationBuilder, "supplier_subscriptions");

        migrationBuilder.Sql("""
            UPDATE orders SET payment_method = 'legacy' WHERE lower(payment_method) = 'stripe';
            UPDATE shop_wallet_transactions SET method = 'legacy' WHERE lower(method) = 'stripe';
            UPDATE supplier_subscriptions SET payment_method = 'legacy' WHERE lower(payment_method) = 'stripe';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_orders_payment_provider_reference",
            table: "orders");
        migrationBuilder.DropColumn(
            name: "payment_provider_reference",
            table: "orders");
        migrationBuilder.RenameIndex(
            name: "ix_orders_payment_checkout_id",
            table: "orders",
            newName: "ix_orders_stripe_checkout_id");
        migrationBuilder.RenameColumn(
            name: "payment_checkout_id",
            table: "orders",
            newName: "stripe_checkout_id");
        migrationBuilder.AddColumn<string>(
            name: "stripe_checkout_session_id",
            table: "orders",
            type: "text",
            nullable: true);
        migrationBuilder.CreateIndex(
            name: "ix_orders_stripe_checkout_session_id",
            table: "orders",
            column: "stripe_checkout_session_id");

        DropProviderSubscriptionIndexes(migrationBuilder, "customer_subscriptions");
        DropProviderSubscriptionIndexes(migrationBuilder, "supplier_subscriptions");
        DropProviderSubscriptionColumns(migrationBuilder, "customer_subscriptions");
        DropProviderSubscriptionColumns(migrationBuilder, "supplier_subscriptions");
        AddLegacySubscriptionColumns(migrationBuilder, "customer_subscriptions");
        AddLegacySubscriptionColumns(migrationBuilder, "supplier_subscriptions");
        CreateLegacySubscriptionIndexes(migrationBuilder, "customer_subscriptions");
        CreateLegacySubscriptionIndexes(migrationBuilder, "supplier_subscriptions");
    }

    private static void DropLegacySubscriptionColumns(
        MigrationBuilder migrationBuilder,
        string table)
    {
        migrationBuilder.DropColumn("stripe_customer_id", table);
        migrationBuilder.DropColumn("stripe_subscription_id", table);
        migrationBuilder.DropColumn("stripe_checkout_session_id", table);
    }

    private static void AddProviderSubscriptionColumns(
        MigrationBuilder migrationBuilder,
        string table)
    {
        migrationBuilder.AddColumn<string>("payment_provider_customer_code", table, "text", nullable: true);
        migrationBuilder.AddColumn<string>("payment_provider_subscription_code", table, "text", nullable: true);
        migrationBuilder.AddColumn<string>("payment_provider_checkout_reference", table, "text", nullable: true);
        migrationBuilder.AddColumn<string>("payment_provider_checkout_url", table, "text", nullable: true);
        migrationBuilder.AddColumn<string>("payment_provider_plan_code", table, "text", nullable: true);
        migrationBuilder.AddColumn<string>("payment_provider_email_token", table, "text", nullable: true);
    }

    private static void CreateProviderSubscriptionIndexes(
        MigrationBuilder migrationBuilder,
        string table)
    {
        migrationBuilder.CreateIndex(
            $"ix_{table}_payment_provider_subscription_code",
            table,
            "payment_provider_subscription_code",
            unique: true,
            filter: "\"payment_provider_subscription_code\" IS NOT NULL");
        migrationBuilder.CreateIndex(
            $"ix_{table}_payment_provider_checkout_reference",
            table,
            "payment_provider_checkout_reference",
            unique: true,
            filter: "\"payment_provider_checkout_reference\" IS NOT NULL");
    }

    private static void DropProviderSubscriptionIndexes(
        MigrationBuilder migrationBuilder,
        string table)
    {
        migrationBuilder.DropIndex($"ix_{table}_payment_provider_checkout_reference", table);
        migrationBuilder.DropIndex($"ix_{table}_payment_provider_subscription_code", table);
    }

    private static void DropProviderSubscriptionColumns(
        MigrationBuilder migrationBuilder,
        string table)
    {
        migrationBuilder.DropColumn("payment_provider_customer_code", table);
        migrationBuilder.DropColumn("payment_provider_subscription_code", table);
        migrationBuilder.DropColumn("payment_provider_checkout_reference", table);
        migrationBuilder.DropColumn("payment_provider_checkout_url", table);
        migrationBuilder.DropColumn("payment_provider_plan_code", table);
        migrationBuilder.DropColumn("payment_provider_email_token", table);
    }

    private static void AddLegacySubscriptionColumns(
        MigrationBuilder migrationBuilder,
        string table)
    {
        migrationBuilder.AddColumn<string>("stripe_customer_id", table, "text", nullable: true);
        migrationBuilder.AddColumn<string>("stripe_subscription_id", table, "text", nullable: true);
        migrationBuilder.AddColumn<string>("stripe_checkout_session_id", table, "text", nullable: true);
    }

    private static void CreateLegacySubscriptionIndexes(
        MigrationBuilder migrationBuilder,
        string table)
    {
        migrationBuilder.CreateIndex(
            $"ix_{table}_stripe_subscription_id",
            table,
            "stripe_subscription_id",
            unique: true,
            filter: "\"stripe_subscription_id\" IS NOT NULL");
        migrationBuilder.CreateIndex(
            $"ix_{table}_stripe_checkout_session_id",
            table,
            "stripe_checkout_session_id",
            unique: true,
            filter: "\"stripe_checkout_session_id\" IS NOT NULL");
    }
}

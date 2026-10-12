using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameOnboardingPaymentProviderReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "UX_ShopOnboardingPayments_PayFastPaymentId";
                DROP INDEX IF EXISTS ix_shop_onboarding_payments_pay_fast_payment_id;
                """);

            migrationBuilder.RenameColumn(
                name: "pay_fast_payment_id",
                table: "shop_onboarding_payments",
                newName: "provider_payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_shop_onboarding_payments_provider_payment_id",
                table: "shop_onboarding_payments",
                column: "provider_payment_id",
                unique: true,
                filter: "provider_payment_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_shop_onboarding_payments_provider_payment_id",
                table: "shop_onboarding_payments");

            migrationBuilder.RenameColumn(
                name: "provider_payment_id",
                table: "shop_onboarding_payments",
                newName: "pay_fast_payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_shop_onboarding_payments_pay_fast_payment_id",
                table: "shop_onboarding_payments",
                column: "pay_fast_payment_id",
                unique: true,
                filter: "pay_fast_payment_id IS NOT NULL");
        }
    }
}

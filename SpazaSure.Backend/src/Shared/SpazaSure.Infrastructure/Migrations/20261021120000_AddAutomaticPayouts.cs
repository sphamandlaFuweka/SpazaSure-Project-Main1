using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpazaSure.Infrastructure.Data;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

/// <summary>Automatic supplier payouts through Paystack Transfers.</summary>
[DbContext(typeof(SpazaSureDbContext))]
[Migration("20261021120000_AddAutomaticPayouts")]
public partial class AddAutomaticPayouts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "payout_initiated_at", table: "orders", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "paystack_recipient_code", table: "suppliers", type: "character varying(64)", maxLength: 64, nullable: true);
        migrationBuilder.CreateIndex(name: "ix_orders_payout_reference", table: "orders", column: "payout_reference");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "ix_orders_payout_reference", table: "orders");
        migrationBuilder.DropColumn(name: "payout_initiated_at", table: "orders");
        migrationBuilder.DropColumn(name: "paystack_recipient_code", table: "suppliers");
    }
}

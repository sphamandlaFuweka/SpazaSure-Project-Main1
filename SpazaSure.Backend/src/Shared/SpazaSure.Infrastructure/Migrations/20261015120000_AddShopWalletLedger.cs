using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpazaSure.Infrastructure.Data;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

[DbContext(typeof(SpazaSureDbContext))]
[Migration("20261015120000_AddShopWalletLedger")]
public partial class AddShopWalletLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "account_holder", table: "shop_wallet_transactions", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "account_number", table: "shop_wallet_transactions", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "bank_name", table: "shop_wallet_transactions", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "branch_code", table: "shop_wallet_transactions", type: "text", nullable: true);

        migrationBuilder.CreateTable(
            name: "shop_wallet_ledger_entries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                amount = table.Column<decimal>(type: "numeric", nullable: false),
                type = table.Column<string>(type: "text", nullable: false),
                source_id = table.Column<Guid>(type: "uuid", nullable: true),
                description = table.Column<string>(type: "text", nullable: false),
                reference = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_shop_wallet_ledger_entries", x => x.id);
                table.ForeignKey(
                    name: "fk_shop_wallet_ledger_entries_spaza_shops_shop_id",
                    column: x => x.shop_id,
                    principalTable: "spaza_shops",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_shop_wallet_ledger_entries_source_id",
            table: "shop_wallet_ledger_entries",
            column: "source_id");
        migrationBuilder.CreateIndex(
            name: "ix_shop_wallet_ledger_entries_shop_id_created_at",
            table: "shop_wallet_ledger_entries",
            columns: new[] { "shop_id", "created_at" });

        // Replay the history the old balance formula used (approved top-ups minus non-cancelled
        // wallet orders), so every shop's balance is exactly the same after the switch.
        migrationBuilder.Sql(@"
INSERT INTO shop_wallet_ledger_entries (id, shop_id, amount, type, source_id, description, reference, created_at)
SELECT gen_random_uuid(), t.shop_id, t.amount, 'top_up', t.id,
       CASE WHEN t.method = 'paystack' THEN 'Wallet top-up (Paystack)'
            WHEN t.method = 'eft' THEN 'Direct deposit'
            ELSE 'Wallet top-up (' || t.method || ')' END,
       t.reference, COALESCE(t.approved_at, t.created_at)
FROM shop_wallet_transactions t
WHERE t.type = 'top_up' AND t.status = 'approved';

INSERT INTO shop_wallet_ledger_entries (id, shop_id, amount, type, source_id, description, reference, created_at)
SELECT gen_random_uuid(), o.shop_id, -o.total_amount, 'order_payment', o.id,
       'Order ' || o.order_number, o.order_number, o.created_at
FROM orders o
WHERE o.payment_method = 'wallet';

INSERT INTO shop_wallet_ledger_entries (id, shop_id, amount, type, source_id, description, reference, created_at)
SELECT gen_random_uuid(), o.shop_id, o.total_amount, 'order_refund', o.id,
       'Refund for cancelled order ' || o.order_number, o.order_number, COALESCE(o.updated_at, o.created_at)
FROM orders o
WHERE o.payment_method = 'wallet' AND o.status = 'cancelled';
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "shop_wallet_ledger_entries");
        migrationBuilder.DropColumn(name: "account_holder", table: "shop_wallet_transactions");
        migrationBuilder.DropColumn(name: "account_number", table: "shop_wallet_transactions");
        migrationBuilder.DropColumn(name: "bank_name", table: "shop_wallet_transactions");
        migrationBuilder.DropColumn(name: "branch_code", table: "shop_wallet_transactions");
    }
}

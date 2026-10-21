using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpazaSure.Infrastructure.Data;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

[DbContext(typeof(SpazaSureDbContext))]
[Migration("20261021130000_AddPaystackOrderSplits")]
public partial class AddPaystackOrderSplits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "paystack_split_code",
            table: "orders",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "paystack_subaccount_code",
            table: "suppliers",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "paystack_split_code", table: "orders");
        migrationBuilder.DropColumn(name: "paystack_subaccount_code", table: "suppliers");
    }
}

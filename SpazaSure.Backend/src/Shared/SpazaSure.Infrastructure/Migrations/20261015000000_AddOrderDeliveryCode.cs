using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpazaSure.Infrastructure.Data;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

[DbContext(typeof(SpazaSureDbContext))]
[Migration("20261015000000_AddOrderDeliveryCode")]
public partial class AddOrderDeliveryCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "delivery_code",
            table: "orders",
            type: "text",
            nullable: true);

        // Cash orders already out for delivery need a code too, or the shop could never confirm them
        migrationBuilder.Sql(
            "UPDATE orders SET delivery_code = lpad(floor(random() * 1000000)::int::text, 6, '0') " +
            "WHERE payment_method = 'cash_on_delivery' AND status = 'dispatched' AND delivery_code IS NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "delivery_code", table: "orders");
    }
}

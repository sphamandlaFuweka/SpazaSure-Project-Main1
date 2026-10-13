using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpazaSure.Infrastructure.Data;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

[DbContext(typeof(SpazaSureDbContext))]
[Migration("20261014120000_AddShopRegistrationDetails")]
public partial class AddShopRegistrationDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "registration_details",
            table: "spaza_shops",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "registration_details",
            table: "spaza_shops");
    }
}

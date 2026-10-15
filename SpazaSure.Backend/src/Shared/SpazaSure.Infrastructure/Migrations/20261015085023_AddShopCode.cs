using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShopCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "shop_code",
                table: "spaza_shops",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_spaza_shops_shop_code",
                table: "spaza_shops",
                column: "shop_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_spaza_shops_shop_code",
                table: "spaza_shops");

            migrationBuilder.DropColumn(
                name: "shop_code",
                table: "spaza_shops");
        }
    }
}

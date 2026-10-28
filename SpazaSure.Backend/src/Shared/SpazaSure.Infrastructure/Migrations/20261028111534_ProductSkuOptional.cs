using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductSkuOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_products_supplier_id_sku",
                table: "products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SupplierId_Sku_Filled",
                table: "products",
                columns: new[] { "supplier_id", "sku" },
                unique: true,
                filter: "sku <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_SupplierId_Sku_Filled",
                table: "products");

            migrationBuilder.CreateIndex(
                name: "ix_products_supplier_id_sku",
                table: "products",
                columns: new[] { "supplier_id", "sku" },
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductUnitCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_unit_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    pin_hash = table.Column<string>(type: "text", nullable: false),
                    batch_number = table.Column<string>(type: "text", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    failed_pin_attempts = table.Column<int>(type: "integer", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consumed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    consumed_latitude = table.Column<double>(type: "double precision", nullable: true),
                    consumed_longitude = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_unit_codes", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_unit_codes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_unit_codes_code",
                table: "product_unit_codes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_unit_codes_product_id_batch_number",
                table: "product_unit_codes",
                columns: new[] { "product_id", "batch_number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_unit_codes");
        }
    }
}

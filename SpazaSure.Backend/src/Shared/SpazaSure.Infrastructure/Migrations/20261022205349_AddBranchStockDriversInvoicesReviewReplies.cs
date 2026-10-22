using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchStockDriversInvoicesReviewReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "owner_replied_at",
                table: "shop_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_reply",
                table: "shop_reviews",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "driver_assigned_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "driver_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_branch_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "drivers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vehicle_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    vehicle_registration = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    area = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    access_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    badge_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    last_latitude = table.Column<double>(type: "double precision", nullable: true),
                    last_longitude = table.Column<double>(type: "double precision", nullable: true),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_drivers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_branch_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_qty = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_branch_stock", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_branch_stock_product_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_branch_stock_supplier_branch_supplier_branch_id",
                        column: x => x.supplier_branch_id,
                        principalTable: "supplier_branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tax_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_tax_invoice = table.Column<bool>(type: "boolean", nullable: false),
                    supplier_vat_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    goods_total_incl_vat = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    verification_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tax_invoices", x => x.id);
                    table.ForeignKey(
                        name: "fk_tax_invoices_order_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tax_invoices_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_driver_id",
                table: "orders",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_supplier_branch_id",
                table: "orders",
                column: "supplier_branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_drivers_access_token",
                table: "drivers",
                column: "access_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_drivers_badge_code",
                table: "drivers",
                column: "badge_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_branch_stock_product_id_supplier_branch_id",
                table: "product_branch_stock",
                columns: new[] { "product_id", "supplier_branch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_branch_stock_supplier_branch_id",
                table: "product_branch_stock",
                column: "supplier_branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_invoices_invoice_number",
                table: "tax_invoices",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_invoices_order_id",
                table: "tax_invoices",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_invoices_supplier_id_sequence_number",
                table: "tax_invoices",
                columns: new[] { "supplier_id", "sequence_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_orders_drivers_driver_id",
                table: "orders",
                column: "driver_id",
                principalTable: "drivers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_orders_supplier_branches_supplier_branch_id",
                table: "orders",
                column: "supplier_branch_id",
                principalTable: "supplier_branches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_orders_drivers_driver_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "fk_orders_supplier_branches_supplier_branch_id",
                table: "orders");

            migrationBuilder.DropTable(
                name: "drivers");

            migrationBuilder.DropTable(
                name: "product_branch_stock");

            migrationBuilder.DropTable(
                name: "tax_invoices");

            migrationBuilder.DropIndex(
                name: "ix_orders_driver_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_supplier_branch_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "owner_replied_at",
                table: "shop_reviews");

            migrationBuilder.DropColumn(
                name: "owner_reply",
                table: "shop_reviews");

            migrationBuilder.DropColumn(
                name: "driver_assigned_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "driver_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "supplier_branch_id",
                table: "orders");
        }
    }
}

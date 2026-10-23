using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShopAcademy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shop_business_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_structure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cipc_registration_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    sars_tax_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    vat_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    trading_licence_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    started_trading_on = table.Column<DateOnly>(type: "date", nullable: true),
                    premises_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    monthly_rent = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    full_time_staff = table.Column<int>(type: "integer", nullable: false),
                    part_time_staff = table.Column<int>(type: "integer", nullable: false),
                    trading_hours = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    trading_days_per_week = table.Column<int>(type: "integer", nullable: false),
                    bank_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    has_business_bank_account = table.Column<bool>(type: "boolean", nullable: false),
                    accepts_card_payments = table.Column<bool>(type: "boolean", nullable: false),
                    main_suppliers = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    goals = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_business_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_shop_business_profiles_spaza_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "spaza_shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_cash_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_cash_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_shop_cash_entries_spaza_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "spaza_shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_lesson_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    quiz_score = table.Column<int>(type: "integer", nullable: false),
                    quiz_total = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_lesson_progress", x => x.id);
                    table.ForeignKey(
                        name: "fk_shop_lesson_progress_spaza_shop_shop_id",
                        column: x => x.shop_id,
                        principalTable: "spaza_shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_stock_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cost_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    selling_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    reorder_level = table.Column<int>(type: "integer", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_stock_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_shop_stock_items_spaza_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "spaza_shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    source_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_stock_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_shop_stock_movements_shop_stock_items_stock_item_id",
                        column: x => x.stock_item_id,
                        principalTable: "shop_stock_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_shop_business_profiles_shop_id",
                table: "shop_business_profiles",
                column: "shop_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shop_cash_entries_shop_id_occurred_on",
                table: "shop_cash_entries",
                columns: new[] { "shop_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_lesson_progress_shop_id_lesson_key",
                table: "shop_lesson_progress",
                columns: new[] { "shop_id", "lesson_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shop_stock_items_shop_id",
                table: "shop_stock_items",
                column: "shop_id");

            migrationBuilder.CreateIndex(
                name: "ix_shop_stock_items_shop_id_product_id",
                table: "shop_stock_items",
                columns: new[] { "shop_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_stock_movements_shop_id_occurred_on",
                table: "shop_stock_movements",
                columns: new[] { "shop_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_shop_stock_movements_source_order_id",
                table: "shop_stock_movements",
                column: "source_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_shop_stock_movements_stock_item_id",
                table: "shop_stock_movements",
                column: "stock_item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_business_profiles");

            migrationBuilder.DropTable(
                name: "shop_cash_entries");

            migrationBuilder.DropTable(
                name: "shop_lesson_progress");

            migrationBuilder.DropTable(
                name: "shop_stock_movements");

            migrationBuilder.DropTable(
                name: "shop_stock_items");
        }
    }
}

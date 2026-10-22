using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierBranchesAndGroupBuyAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "area_city",
                table: "group_buys",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "area_latitude",
                table: "group_buys",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "area_longitude",
                table: "group_buys",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "area_province",
                table: "group_buys",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "area_radius_km",
                table: "group_buys",
                type: "numeric(7,1)",
                precision: 7,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_branch_id",
                table: "group_buys",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "supplier_branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    province = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_branches", x => x.id);
                    table.ForeignKey(
                        name: "fk_supplier_branches_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_group_buys_supplier_branch_id",
                table: "group_buys",
                column: "supplier_branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_branches_supplier_id_is_active",
                table: "supplier_branches",
                columns: new[] { "supplier_id", "is_active" });

            migrationBuilder.AddForeignKey(
                name: "fk_group_buys_supplier_branches_supplier_branch_id",
                table: "group_buys",
                column: "supplier_branch_id",
                principalTable: "supplier_branches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_group_buys_supplier_branches_supplier_branch_id",
                table: "group_buys");

            migrationBuilder.DropTable(
                name: "supplier_branches");

            migrationBuilder.DropIndex(
                name: "ix_group_buys_supplier_branch_id",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "area_city",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "area_latitude",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "area_longitude",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "area_province",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "area_radius_km",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "supplier_branch_id",
                table: "group_buys");
        }
    }
}

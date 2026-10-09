using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportEvidenceAndScanLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<string>(
                name: "device_id",
                table: "reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "reports",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "reports",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "purchase_price",
                table: "reports",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reporter_ip",
                table: "reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "scratch_panel_intact",
                table: "reports",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "seal_tampered",
                table: "reports",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "serial_code",
                table: "reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shop_address",
                table: "reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "device_id",
                table: "customer_scan_events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                table: "customer_scan_events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "customer_scan_events",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "customer_scan_events",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropColumn(
                name: "device_id",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "purchase_price",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "reporter_ip",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "scratch_panel_intact",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "seal_tampered",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "serial_code",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "shop_address",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "device_id",
                table: "customer_scan_events");

            migrationBuilder.DropColumn(
                name: "ip_address",
                table: "customer_scan_events");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "customer_scan_events");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "customer_scan_events");
        }
    }
}

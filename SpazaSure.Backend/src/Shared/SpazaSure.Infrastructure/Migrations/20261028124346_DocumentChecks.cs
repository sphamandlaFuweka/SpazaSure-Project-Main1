using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "check_details",
                table: "supplier_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "check_status",
                table: "supplier_documents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "check_summary",
                table: "supplier_documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "checked_at",
                table: "supplier_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_number",
                table: "supplier_documents",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "check_details",
                table: "shop_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "check_status",
                table: "shop_documents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "check_summary",
                table: "shop_documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "checked_at",
                table: "shop_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_number",
                table: "shop_documents",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "check_details",
                table: "driver_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "check_status",
                table: "driver_documents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "check_summary",
                table: "driver_documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "checked_at",
                table: "driver_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_number",
                table: "driver_documents",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplier_documents_document_number",
                table: "supplier_documents",
                column: "document_number");

            migrationBuilder.CreateIndex(
                name: "ix_shop_documents_document_number",
                table: "shop_documents",
                column: "document_number");

            migrationBuilder.CreateIndex(
                name: "ix_driver_documents_document_number",
                table: "driver_documents",
                column: "document_number");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_supplier_documents_document_number",
                table: "supplier_documents");

            migrationBuilder.DropIndex(
                name: "ix_shop_documents_document_number",
                table: "shop_documents");

            migrationBuilder.DropIndex(
                name: "ix_driver_documents_document_number",
                table: "driver_documents");

            migrationBuilder.DropColumn(
                name: "check_details",
                table: "supplier_documents");

            migrationBuilder.DropColumn(
                name: "check_status",
                table: "supplier_documents");

            migrationBuilder.DropColumn(
                name: "check_summary",
                table: "supplier_documents");

            migrationBuilder.DropColumn(
                name: "checked_at",
                table: "supplier_documents");

            migrationBuilder.DropColumn(
                name: "document_number",
                table: "supplier_documents");

            migrationBuilder.DropColumn(
                name: "check_details",
                table: "shop_documents");

            migrationBuilder.DropColumn(
                name: "check_status",
                table: "shop_documents");

            migrationBuilder.DropColumn(
                name: "check_summary",
                table: "shop_documents");

            migrationBuilder.DropColumn(
                name: "checked_at",
                table: "shop_documents");

            migrationBuilder.DropColumn(
                name: "document_number",
                table: "shop_documents");

            migrationBuilder.DropColumn(
                name: "check_details",
                table: "driver_documents");

            migrationBuilder.DropColumn(
                name: "check_status",
                table: "driver_documents");

            migrationBuilder.DropColumn(
                name: "check_summary",
                table: "driver_documents");

            migrationBuilder.DropColumn(
                name: "checked_at",
                table: "driver_documents");

            migrationBuilder.DropColumn(
                name: "document_number",
                table: "driver_documents");
        }
    }
}

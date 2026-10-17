using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpazaSure.Infrastructure.Data;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

/// <summary>
/// Richer product information, like a retail product page: brand and pack details, specifications,
/// nutrition and serving information, ingredients, usage and storage, warnings, and the supplier's
/// tips for spotting a counterfeit.
/// </summary>
[DbContext(typeof(SpazaSureDbContext))]
[Migration("20261017120000_AddProductInformation")]
public partial class AddProductInformation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "brand", table: "products", type: "character varying(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "pack_size", table: "products", type: "character varying(60)", maxLength: 60, nullable: true);
        migrationBuilder.AddColumn<string>(name: "country_of_origin", table: "products", type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(name: "manufacturer", table: "products", type: "character varying(160)", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<string>(name: "serving_size", table: "products", type: "character varying(60)", maxLength: 60, nullable: true);
        migrationBuilder.AddColumn<string>(name: "servings_per_pack", table: "products", type: "character varying(60)", maxLength: 60, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ingredients", table: "products", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "usage_instructions", table: "products", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "storage_instructions", table: "products", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "warnings", table: "products", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "nutrition_info", table: "products", type: "text", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>(name: "specifications", table: "products", type: "text", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>(name: "authenticity_tips", table: "products", type: "text", nullable: false, defaultValue: "[]");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "brand", table: "products");
        migrationBuilder.DropColumn(name: "pack_size", table: "products");
        migrationBuilder.DropColumn(name: "country_of_origin", table: "products");
        migrationBuilder.DropColumn(name: "manufacturer", table: "products");
        migrationBuilder.DropColumn(name: "serving_size", table: "products");
        migrationBuilder.DropColumn(name: "servings_per_pack", table: "products");
        migrationBuilder.DropColumn(name: "ingredients", table: "products");
        migrationBuilder.DropColumn(name: "usage_instructions", table: "products");
        migrationBuilder.DropColumn(name: "storage_instructions", table: "products");
        migrationBuilder.DropColumn(name: "warnings", table: "products");
        migrationBuilder.DropColumn(name: "nutrition_info", table: "products");
        migrationBuilder.DropColumn(name: "specifications", table: "products");
        migrationBuilder.DropColumn(name: "authenticity_tips", table: "products");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SpazaSure.Infrastructure.Data;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

[DbContext(typeof(SpazaSureDbContext))]
[Migration("20261013130000_AddCustomerRegistrationPreferences")]
public partial class AddCustomerRegistrationPreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "preferred_name",
            table: "customer_profiles",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "preferred_language",
            table: "customer_profiles",
            type: "text",
            nullable: false,
            defaultValue: "English");

        migrationBuilder.AddColumn<string>(
            name: "province",
            table: "customer_profiles",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "city",
            table: "customer_profiles",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "suburb",
            table: "customer_profiles",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "nearby_recommendations_enabled",
            table: "customer_profiles",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<double>(
            name: "latitude",
            table: "customer_profiles",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<double>(
            name: "longitude",
            table: "customer_profiles",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "interests",
            table: "customer_profiles",
            type: "text",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "notification_preferences",
            table: "customer_profiles",
            type: "text",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<bool>(
            name: "marketing_opt_in",
            table: "customer_profiles",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "preferred_name", table: "customer_profiles");
        migrationBuilder.DropColumn(name: "preferred_language", table: "customer_profiles");
        migrationBuilder.DropColumn(name: "province", table: "customer_profiles");
        migrationBuilder.DropColumn(name: "city", table: "customer_profiles");
        migrationBuilder.DropColumn(name: "suburb", table: "customer_profiles");
        migrationBuilder.DropColumn(name: "nearby_recommendations_enabled", table: "customer_profiles");
        migrationBuilder.DropColumn(name: "latitude", table: "customer_profiles");
        migrationBuilder.DropColumn(name: "longitude", table: "customer_profiles");
        migrationBuilder.DropColumn(name: "interests", table: "customer_profiles");
        migrationBuilder.DropColumn(name: "notification_preferences", table: "customer_profiles");
        migrationBuilder.DropColumn(name: "marketing_opt_in", table: "customer_profiles");
    }
}

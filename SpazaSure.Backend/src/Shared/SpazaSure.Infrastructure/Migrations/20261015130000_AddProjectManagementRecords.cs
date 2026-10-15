using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpazaSure.Infrastructure.Data;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

/// <summary>Adds the admin Projects &amp; Impact (project management, MEL and reporting) workspace table.</summary>
[DbContext(typeof(SpazaSureDbContext))]
[Migration("20261015130000_AddProjectManagementRecords")]
public partial class AddProjectManagementRecords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "pm_records",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                data = table.Column<string>(type: "jsonb", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pm_records", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_pm_records_kind",
            table: "pm_records",
            column: "kind");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "pm_records");
    }
}

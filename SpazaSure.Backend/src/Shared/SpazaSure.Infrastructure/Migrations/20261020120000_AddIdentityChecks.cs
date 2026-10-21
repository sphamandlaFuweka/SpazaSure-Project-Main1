using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpazaSure.Infrastructure.Data;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

/// <summary>
/// Didit identity checks: shop owners (ID document, selfie, Home Affairs, sanctions) and suppliers
/// (CIPC registration, directors, sanctions). Stores the Didit session, its latest status and warnings.
/// </summary>
[DbContext(typeof(SpazaSureDbContext))]
[Migration("20261020120000_AddIdentityChecks")]
public partial class AddIdentityChecks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "spaza_shops", "suppliers" })
        {
            migrationBuilder.AddColumn<string>(name: "identity_check_session_id", table: table, type: "character varying(64)", maxLength: 64, nullable: true);
            migrationBuilder.AddColumn<string>(name: "identity_check_status", table: table, type: "character varying(30)", maxLength: 30, nullable: true);
            migrationBuilder.AddColumn<string>(name: "identity_check_notes", table: table, type: "character varying(2000)", maxLength: 2000, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "identity_check_updated_at", table: table, type: "timestamp with time zone", nullable: true);
            migrationBuilder.CreateIndex(name: $"ix_{table}_identity_check_session_id", table: table, column: "identity_check_session_id");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "spaza_shops", "suppliers" })
        {
            migrationBuilder.DropIndex(name: $"ix_{table}_identity_check_session_id", table: table);
            migrationBuilder.DropColumn(name: "identity_check_session_id", table: table);
            migrationBuilder.DropColumn(name: "identity_check_status", table: table);
            migrationBuilder.DropColumn(name: "identity_check_notes", table: table);
            migrationBuilder.DropColumn(name: "identity_check_updated_at", table: table);
        }
    }
}

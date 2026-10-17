using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpazaSure.Infrastructure.Data;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

/// <summary>
/// Dashboard staff roles: a master admin registers staff and decides which admin areas each one can
/// view or manage. Existing admin accounts become master admins so nobody loses access.
/// </summary>
[DbContext(typeof(SpazaSureDbContext))]
[Migration("20261017090000_AddAdminStaffAccess")]
public partial class AddAdminStaffAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "display_name",
            table: "users",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "admin_title",
            table: "users",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "admin_access",
            table: "users",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE users SET admin_access = 'master', admin_title = COALESCE(admin_title, 'Master admin')
            WHERE admin_access IS NULL AND role_id IN (SELECT id FROM roles WHERE name = 'admin');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "display_name", table: "users");
        migrationBuilder.DropColumn(name: "admin_title", table: "users");
        migrationBuilder.DropColumn(name: "admin_access", table: "users");
    }
}

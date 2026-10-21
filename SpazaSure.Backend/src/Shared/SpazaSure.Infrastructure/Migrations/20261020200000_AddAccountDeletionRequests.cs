using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpazaSure.Infrastructure.Data;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations;

/// <summary>Requests from users to delete their account and personal information (POPIA s24).</summary>
[DbContext(typeof(SpazaSureDbContext))]
[Migration("20261020200000_AddAccountDeletionRequests")]
public partial class AddAccountDeletionRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "account_deletion_requests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_account_deletion_requests", x => x.id));

        migrationBuilder.CreateIndex(name: "ix_account_deletion_requests_status", table: "account_deletion_requests", column: "status");
        migrationBuilder.CreateIndex(name: "ix_account_deletion_requests_user_id", table: "account_deletion_requests", column: "user_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "account_deletion_requests");
    }
}

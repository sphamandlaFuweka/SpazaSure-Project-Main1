using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GroupBuyBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "group_buy_branch_id",
                table: "group_buy_participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "group_buy_branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_buy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: true),
                    province = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_buy_branches", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_buy_branches_group_buys_group_buy_id",
                        column: x => x.group_buy_id,
                        principalTable: "group_buys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_group_buy_branches_group_buy_id",
                table: "group_buy_branches",
                column: "group_buy_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_buy_branches");

            migrationBuilder.DropColumn(
                name: "group_buy_branch_id",
                table: "group_buy_participants");
        }
    }
}

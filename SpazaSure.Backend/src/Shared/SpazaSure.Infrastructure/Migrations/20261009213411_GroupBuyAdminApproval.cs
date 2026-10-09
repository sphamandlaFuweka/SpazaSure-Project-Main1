using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GroupBuyAdminApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_group_buys_spaza_shops_created_by_shop_id",
                table: "group_buys");

            migrationBuilder.AlterColumn<Guid>(
                name: "created_by_shop_id",
                table: "group_buys",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "group_buys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by_user_id",
                table: "group_buys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_role",
                table: "group_buys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "group_buys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_note",
                table: "group_buys",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE group_buys SET created_by_role = 'shop' WHERE created_by_role = ''");

            migrationBuilder.AddForeignKey(
                name: "fk_group_buys_spaza_shops_created_by_shop_id",
                table: "group_buys",
                column: "created_by_shop_id",
                principalTable: "spaza_shops",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_group_buys_spaza_shops_created_by_shop_id",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "approved_by_user_id",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "created_by_role",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "group_buys");

            migrationBuilder.DropColumn(
                name: "rejection_note",
                table: "group_buys");

            migrationBuilder.AlterColumn<Guid>(
                name: "created_by_shop_id",
                table: "group_buys",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_group_buys_spaza_shops_created_by_shop_id",
                table: "group_buys",
                column: "created_by_shop_id",
                principalTable: "spaza_shops",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

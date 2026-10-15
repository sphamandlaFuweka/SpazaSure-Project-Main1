using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpazaSure.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShopChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shop_conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_message_preview = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    customer_unread = table.Column<int>(type: "integer", nullable: false),
                    shop_unread = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_conversations", x => x.id);
                    table.ForeignKey(
                        name: "fk_shop_conversations_spaza_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "spaza_shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shop_conversations_users_customer_user_id",
                        column: x => x.customer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_shop_messages_shop_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "shop_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_shop_conversations_customer_user_id",
                table: "shop_conversations",
                column: "customer_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_shop_conversations_shop_id_customer_user_id",
                table: "shop_conversations",
                columns: new[] { "shop_id", "customer_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shop_messages_conversation_id_created_at",
                table: "shop_messages",
                columns: new[] { "conversation_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_messages");

            migrationBuilder.DropTable(
                name: "shop_conversations");
        }
    }
}

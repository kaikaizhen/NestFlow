using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NestFlow_Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Module14_FamilyAccountSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "settled_at",
                table: "account_entries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "settled_by_user_id",
                table: "account_entries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "account_entry_shares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    account_entry_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    participant_name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_entry_shares", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_entry_shares_account_entries_account_entry_id",
                        column: x => x.account_entry_id,
                        principalTable: "account_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_account_entry_shares_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_entries_settled_by_user_id",
                table: "account_entries",
                column: "settled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_account_entry_shares_entry",
                table: "account_entry_shares",
                column: "account_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_account_entry_shares_user_id",
                table: "account_entry_shares",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_account_entries_users_settled_by_user_id",
                table: "account_entries",
                column: "settled_by_user_id",
                principalTable: "users",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_account_entries_users_settled_by_user_id",
                table: "account_entries");

            migrationBuilder.DropTable(
                name: "account_entry_shares");

            migrationBuilder.DropIndex(
                name: "IX_account_entries_settled_by_user_id",
                table: "account_entries");

            migrationBuilder.DropColumn(
                name: "settled_at",
                table: "account_entries");

            migrationBuilder.DropColumn(
                name: "settled_by_user_id",
                table: "account_entries");
        }
    }
}

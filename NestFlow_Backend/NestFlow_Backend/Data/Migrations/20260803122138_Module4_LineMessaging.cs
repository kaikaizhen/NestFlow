using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NestFlow_Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Module4_LineMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "binding_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    code_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_binding_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_binding_codes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pending_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    action_type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    payload_json = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    schema_version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    workflow_version = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_actions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    external_event_id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_binding_codes_user_id",
                table: "binding_codes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_binding_codes_code_hash",
                table: "binding_codes",
                column: "code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pending_actions_user_provider_status",
                table: "pending_actions",
                columns: new[] { "user_id", "provider", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_processed_events_provider_event",
                table: "processed_events",
                columns: new[] { "provider", "external_event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "binding_codes");

            migrationBuilder.DropTable(
                name: "pending_actions");

            migrationBuilder.DropTable(
                name: "processed_events");
        }
    }
}

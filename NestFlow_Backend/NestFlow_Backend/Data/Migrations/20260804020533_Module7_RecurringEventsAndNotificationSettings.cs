using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NestFlow_Backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Module7_RecurringEventsAndNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "notifications_enabled",
                table: "users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "calendar_event_id",
                table: "reminders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recurrence_group_id",
                table: "calendar_events",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_reminders_calendar_event_id",
                table: "reminders",
                column: "calendar_event_id",
                unique: true,
                filter: "[calendar_event_id] IS NOT NULL AND [status] <> 'Cancelled'");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_recurrence_group",
                table: "calendar_events",
                column: "recurrence_group_id");

            migrationBuilder.AddForeignKey(
                name: "FK_reminders_calendar_events_calendar_event_id",
                table: "reminders",
                column: "calendar_event_id",
                principalTable: "calendar_events",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reminders_calendar_events_calendar_event_id",
                table: "reminders");

            migrationBuilder.DropIndex(
                name: "ux_reminders_calendar_event_id",
                table: "reminders");

            migrationBuilder.DropIndex(
                name: "ix_calendar_events_recurrence_group",
                table: "calendar_events");

            migrationBuilder.DropColumn(
                name: "notifications_enabled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "calendar_event_id",
                table: "reminders");

            migrationBuilder.DropColumn(
                name: "recurrence_group_id",
                table: "calendar_events");
        }
    }
}

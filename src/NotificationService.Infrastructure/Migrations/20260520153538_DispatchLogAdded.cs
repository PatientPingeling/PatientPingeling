using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DispatchLogAdded : Migration
    {
        private static readonly string[] s_notificationLogs_TenantId_SentAt = new[] { "TenantId", "SentAt" };
        private static readonly string[] s_dispatchLogs_ScheduledNotificationId_AttemptedAt = new[] { "ScheduledNotificationId", "AttemptedAt" };
        private static readonly string[] s_scheduledNotifications_StatusSendAt = new[] { "Status", "SendAt" };
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScheduledNotifications_Status_SendAt",
                table: "ScheduledNotifications");

            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_TenantId",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ScheduledNotifications");

            migrationBuilder.CreateTable(
                name: "DispatchLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ScheduledNotificationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispatchLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DispatchLogs_ScheduledNotifications_ScheduledNotificationId",
                        column: x => x.ScheduledNotificationId,
                        principalTable: "ScheduledNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_SendAt",
                table: "ScheduledNotifications",
                column: "SendAt");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_TenantId_SentAt",
                table: "NotificationLogs",
                columns: s_notificationLogs_TenantId_SentAt);

            migrationBuilder.CreateIndex(
                name: "IX_DispatchLogs_ScheduledNotificationId_AttemptedAt",
                table: "DispatchLogs",
                columns: s_dispatchLogs_ScheduledNotificationId_AttemptedAt);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispatchLogs");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledNotifications_SendAt",
                table: "ScheduledNotifications");

            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_TenantId_SentAt",
                table: "NotificationLogs");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ScheduledNotifications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_Status_SendAt",
                table: "ScheduledNotifications",
                columns: s_scheduledNotifications_StatusSendAt);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_TenantId",
                table: "NotificationLogs",
                column: "TenantId");
        }
    }
}

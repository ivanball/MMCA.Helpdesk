using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMCA.Helpdesk.Migrations.SqlServer.Tickets.Migrations
{
    /// <inheritdoc />
    public partial class CommonV1150AuditTrailSchedulerAndTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "Tickets",
                table: "TicketComment",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "Tickets",
                table: "Ticket",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AuditTrailEntries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: false),
                    EntityKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PropertyName = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Operation = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    ChangedBy = table.Column<int>(type: "int", nullable: true),
                    ChangedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    TenantId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrailEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJobs",
                schema: "dbo",
                columns: table => new
                {
                    JobName = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    CronExpression = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false),
                    NextRunOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastRunOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutcome = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    LastDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobs", x => x.JobName);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketComment_TenantId",
                schema: "Tickets",
                table: "TicketComment",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TenantId",
                schema: "Tickets",
                table: "Ticket",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailEntries_ChangedOn",
                schema: "dbo",
                table: "AuditTrailEntries",
                column: "ChangedOn");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailEntries_Entity",
                schema: "dbo",
                table: "AuditTrailEntries",
                columns: new[] { "EntityType", "EntityKey", "ChangedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_NextRunOn",
                schema: "dbo",
                table: "ScheduledJobs",
                column: "NextRunOn")
                .Annotation("SqlServer:Include", new[] { "LockedUntil", "LockToken" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditTrailEntries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ScheduledJobs",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_TicketComment_TenantId",
                schema: "Tickets",
                table: "TicketComment");

            migrationBuilder.DropIndex(
                name: "IX_Ticket_TenantId",
                schema: "Tickets",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Tickets",
                table: "TicketComment");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "Tickets",
                table: "Ticket");
        }
    }
}

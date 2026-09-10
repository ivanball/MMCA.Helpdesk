using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMCA.Helpdesk.Migrations.SqlServer.Tickets.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InternalCommands",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandType = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduledOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DeadLetteredOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClaimedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    TraceId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    SpanId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserRoles = table.Column<string>(type: "varchar(512)", unicode: false, maxLength: 512, nullable: true),
                    TenantId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalCommands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InternalCommands_DeadLettered",
                schema: "dbo",
                table: "InternalCommands",
                column: "DeadLetteredOn",
                filter: "[DeadLetteredOn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InternalCommands_Pending",
                schema: "dbo",
                table: "InternalCommands",
                column: "ScheduledOn",
                filter: "[ProcessedOn] IS NULL AND [DeadLetteredOn] IS NULL")
                .Annotation("SqlServer:Include", new[] { "Attempts", "ClaimedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalCommands_Processed",
                schema: "dbo",
                table: "InternalCommands",
                column: "ProcessedOn",
                filter: "[ProcessedOn] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InternalCommands",
                schema: "dbo");
        }
    }
}

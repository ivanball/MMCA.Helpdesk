using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMCA.Helpdesk.Migrations.SqlServer.Tickets.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxInboxRetentionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "dbo",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "dbo",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOn", "OccurredOn" },
                filter: "[ProcessedOn] IS NULL")
                .Annotation("SqlServer:Include", new[] { "RetryCount", "LockedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Processed",
                schema: "dbo",
                table: "OutboxMessages",
                column: "ProcessedOn",
                filter: "[ProcessedOn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ProcessedOn",
                schema: "dbo",
                table: "InboxMessages",
                column: "ProcessedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "dbo",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Processed",
                schema: "dbo",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_ProcessedOn",
                schema: "dbo",
                table: "InboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "dbo",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOn", "OccurredOn" },
                filter: "[ProcessedOn] IS NULL");
        }
    }
}

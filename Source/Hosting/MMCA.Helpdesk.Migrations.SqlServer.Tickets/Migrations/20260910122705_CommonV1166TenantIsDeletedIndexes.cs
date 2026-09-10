using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMCA.Helpdesk.Migrations.SqlServer.Tickets.Migrations
{
    /// <inheritdoc />
    public partial class CommonV1166TenantIsDeletedIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketComment_TenantId",
                schema: "Tickets",
                table: "TicketComment");

            migrationBuilder.DropIndex(
                name: "IX_Ticket_TenantId",
                schema: "Tickets",
                table: "Ticket");

            migrationBuilder.CreateIndex(
                name: "IX_TicketComment_TenantId_IsDeleted",
                schema: "Tickets",
                table: "TicketComment",
                columns: new[] { "TenantId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TenantId_IsDeleted",
                schema: "Tickets",
                table: "Ticket",
                columns: new[] { "TenantId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TicketComment_TenantId_IsDeleted",
                schema: "Tickets",
                table: "TicketComment");

            migrationBuilder.DropIndex(
                name: "IX_Ticket_TenantId_IsDeleted",
                schema: "Tickets",
                table: "Ticket");

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
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMCA.Helpdesk.Migrations.SqlServer.Tickets.Migrations
{
    /// <inheritdoc />
    public partial class RestrictDeleteByDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketComment_Ticket_TicketId",
                schema: "Tickets",
                table: "TicketComment");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketComment_Ticket_TicketId",
                schema: "Tickets",
                table: "TicketComment",
                column: "TicketId",
                principalSchema: "Tickets",
                principalTable: "Ticket",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketComment_Ticket_TicketId",
                schema: "Tickets",
                table: "TicketComment");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketComment_Ticket_TicketId",
                schema: "Tickets",
                table: "TicketComment",
                column: "TicketId",
                principalSchema: "Tickets",
                principalTable: "Ticket",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

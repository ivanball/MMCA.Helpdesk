using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMCA.Helpdesk.Migrations.SqlServer.Tickets.Migrations
{
    /// <inheritdoc />
    public partial class CommonAuditDeletionColumnsAndOutboxOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeletedBy",
                schema: "Tickets",
                table: "TicketComment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                schema: "Tickets",
                table: "TicketComment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedBy",
                schema: "Tickets",
                table: "Ticket",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOn",
                schema: "Tickets",
                table: "Ticket",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderingKey",
                schema: "dbo",
                table: "OutboxMessages",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Ordering",
                schema: "dbo",
                table: "OutboxMessages",
                columns: new[] { "OrderingKey", "OccurredOn" },
                filter: "[OrderingKey] IS NOT NULL AND [ProcessedOn] IS NULL")
                .Annotation("SqlServer:Include", new[] { "RetryCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Ordering",
                schema: "dbo",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "Tickets",
                table: "TicketComment");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                schema: "Tickets",
                table: "TicketComment");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "Tickets",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "DeletedOn",
                schema: "Tickets",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "OrderingKey",
                schema: "dbo",
                table: "OutboxMessages");
        }
    }
}

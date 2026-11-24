using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MMCA.Helpdesk.Migrations.SqlServer.Tickets.Migrations
{
    /// <inheritdoc />
    public partial class AddTraceFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "TraceFlags",
                schema: "dbo",
                table: "OutboxMessages",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "TraceFlags",
                schema: "dbo",
                table: "InternalCommands",
                type: "tinyint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TraceFlags",
                schema: "dbo",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "TraceFlags",
                schema: "dbo",
                table: "InternalCommands");
        }
    }
}

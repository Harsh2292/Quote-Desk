using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteDesk.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRunTrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TraceJson",
                table: "AgentRuns",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TraceJson",
                table: "AgentRuns");
        }
    }
}

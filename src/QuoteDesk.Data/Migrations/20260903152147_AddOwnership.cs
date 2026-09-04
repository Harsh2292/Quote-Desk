using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuoteDesk.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "Enquiries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "AgentRuns",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enquiries_OwnerUserId",
                table: "Enquiries",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_OwnerUserId",
                table: "AgentRuns",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentRuns_Users_OwnerUserId",
                table: "AgentRuns",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Enquiries_Users_OwnerUserId",
                table: "Enquiries",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentRuns_Users_OwnerUserId",
                table: "AgentRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Enquiries_Users_OwnerUserId",
                table: "Enquiries");

            migrationBuilder.DropIndex(
                name: "IX_Enquiries_OwnerUserId",
                table: "Enquiries");

            migrationBuilder.DropIndex(
                name: "IX_AgentRuns_OwnerUserId",
                table: "AgentRuns");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Enquiries");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "AgentRuns");
        }
    }
}

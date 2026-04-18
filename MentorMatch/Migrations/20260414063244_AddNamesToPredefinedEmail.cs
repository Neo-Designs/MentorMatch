using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MentorMatch.Migrations
{
    /// <inheritdoc />
    public partial class AddNamesToPredefinedEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Proposals_ProposalId",
                table: "Matches");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "PredefinedEmails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "PredefinedEmails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Proposals_ProposalId",
                table: "Matches",
                column: "ProposalId",
                principalTable: "Proposals",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matches_Proposals_ProposalId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "PredefinedEmails");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "PredefinedEmails");

            migrationBuilder.AddForeignKey(
                name: "FK_Matches_Proposals_ProposalId",
                table: "Matches",
                column: "ProposalId",
                principalTable: "Proposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

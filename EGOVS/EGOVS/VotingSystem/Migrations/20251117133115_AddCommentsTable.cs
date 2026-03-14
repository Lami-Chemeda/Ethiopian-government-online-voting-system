using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VotingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Voters_VoterNationalId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_VoterNationalId",
                table: "Comments");

            migrationBuilder.RenameColumn(
                name: "VoterNationalId",
                table: "Comments",
                newName: "SenderNationalId");

            migrationBuilder.AddColumn<string>(
                name: "CommentType",
                table: "Comments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Comments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReceiverName",
                table: "Comments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverNationalId",
                table: "Comments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverType",
                table: "Comments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                table: "Comments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderType",
                table: "Comments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Comments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommentType",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ReceiverName",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ReceiverNationalId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ReceiverType",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "SenderName",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "SenderType",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Comments");

            migrationBuilder.RenameColumn(
                name: "SenderNationalId",
                table: "Comments",
                newName: "VoterNationalId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_VoterNationalId",
                table: "Comments",
                column: "VoterNationalId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Voters_VoterNationalId",
                table: "Comments",
                column: "VoterNationalId",
                principalTable: "Voters",
                principalColumn: "NationalId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

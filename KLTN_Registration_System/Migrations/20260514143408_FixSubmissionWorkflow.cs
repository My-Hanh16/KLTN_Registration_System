using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    /// <inheritdoc />
    public partial class FixSubmissionWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StatusTemp",
                table: "TimelineSubmissions",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
        UPDATE TimelineSubmissions
        SET StatusTemp =
            CASE Status
                WHEN 'Pending' THEN 0
                WHEN 'Approved' THEN 1
                WHEN 'Rejected' THEN 2
                ELSE 0
            END
    ");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "TimelineSubmissions");

            migrationBuilder.RenameColumn(
                name: "StatusTemp",
                table: "TimelineSubmissions",
                newName: "Status");

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "TimelineSubmissions",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "TimelineSubmissions");

            migrationBuilder.AddColumn<string>(
                name: "StatusOld",
                table: "TimelineSubmissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.Sql(@"
        UPDATE TimelineSubmissions
        SET StatusOld =
            CASE Status
                WHEN 0 THEN 'Pending'
                WHEN 1 THEN 'Approved'
                WHEN 2 THEN 'Rejected'
                ELSE 'Pending'
            END
    ");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "TimelineSubmissions");

            migrationBuilder.RenameColumn(
                name: "StatusOld",
                table: "TimelineSubmissions",
                newName: "Status");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    /// <inheritdoc />
    public partial class AddLecturerCommentToTimelineSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LecturerComment",
                table: "TimelineSubmissions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LecturerComment",
                table: "TimelineSubmissions");
        }
    }
}

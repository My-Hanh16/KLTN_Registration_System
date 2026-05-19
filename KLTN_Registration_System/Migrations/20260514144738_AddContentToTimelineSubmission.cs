using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    /// <inheritdoc />
    public partial class AddContentToTimelineSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "TimelineSubmissions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                table: "TimelineSubmissions");
        }
    }
}

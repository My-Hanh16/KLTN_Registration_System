using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    /// <inheritdoc />
    public partial class AddFacultyToTopic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Faculty",
                table: "Topics",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Faculty",
                table: "Topics");
        }
    }
}

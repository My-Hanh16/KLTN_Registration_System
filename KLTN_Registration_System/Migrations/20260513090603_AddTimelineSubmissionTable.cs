using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    /// <inheritdoc />
    public partial class AddTimelineSubmissionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TimelineSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimelineId = table.Column<int>(type: "int", nullable: false),
                    StudentId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimelineSubmissions_Timelines_TimelineId",
                        column: x => x.TimelineId,
                        principalTable: "Timelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimelineSubmissions_TimelineId",
                table: "TimelineSubmissions",
                column: "TimelineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimelineSubmissions");
        }
    }
}

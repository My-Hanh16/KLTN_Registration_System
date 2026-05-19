using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    /// <inheritdoc />
    public partial class RefactorTimelineSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimelineSubmissions_AspNetUsers_ReviewedById",
                table: "TimelineSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_TimelineSubmissions_AspNetUsers_StudentId",
                table: "TimelineSubmissions");

            migrationBuilder.DropTable(
                name: "StudentProgresses");

            migrationBuilder.DropColumn(
                name: "IsReviewed",
                table: "TimelineSubmissions");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "TimelineSubmissions",
                newName: "ProgressDescription");

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercent",
                table: "TimelineSubmissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TimelineId1",
                table: "TimelineSubmissions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimelineSubmissions_TimelineId1",
                table: "TimelineSubmissions",
                column: "TimelineId1");

            migrationBuilder.AddForeignKey(
                name: "FK_TimelineSubmissions_AspNetUsers_ReviewedById",
                table: "TimelineSubmissions",
                column: "ReviewedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TimelineSubmissions_AspNetUsers_StudentId",
                table: "TimelineSubmissions",
                column: "StudentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TimelineSubmissions_Timelines_TimelineId1",
                table: "TimelineSubmissions",
                column: "TimelineId1",
                principalTable: "Timelines",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimelineSubmissions_AspNetUsers_ReviewedById",
                table: "TimelineSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_TimelineSubmissions_AspNetUsers_StudentId",
                table: "TimelineSubmissions");

            migrationBuilder.DropForeignKey(
                name: "FK_TimelineSubmissions_Timelines_TimelineId1",
                table: "TimelineSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_TimelineSubmissions_TimelineId1",
                table: "TimelineSubmissions");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "TimelineSubmissions");

            migrationBuilder.DropColumn(
                name: "TimelineId1",
                table: "TimelineSubmissions");

            migrationBuilder.RenameColumn(
                name: "ProgressDescription",
                table: "TimelineSubmissions",
                newName: "Content");

            migrationBuilder.AddColumn<bool>(
                name: "IsReviewed",
                table: "TimelineSubmissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "StudentProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimelineId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    StudentId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentProgresses_Timelines_TimelineId",
                        column: x => x.TimelineId,
                        principalTable: "Timelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentProgresses_TimelineId",
                table: "StudentProgresses",
                column: "TimelineId");

            migrationBuilder.AddForeignKey(
                name: "FK_TimelineSubmissions_AspNetUsers_ReviewedById",
                table: "TimelineSubmissions",
                column: "ReviewedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TimelineSubmissions_AspNetUsers_StudentId",
                table: "TimelineSubmissions",
                column: "StudentId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}

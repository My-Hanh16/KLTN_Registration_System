using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    /// <inheritdoc />
    public partial class AddTimelineDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimelineSubmissions_Timelines_TimelineId1",
                table: "TimelineSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_TimelineSubmissions_TimelineId1",
                table: "TimelineSubmissions");

            migrationBuilder.DropColumn(
                name: "TimelineId1",
                table: "TimelineSubmissions");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Timelines",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Timelines",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewDeadline",
                table: "Timelines",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmissionDeadline",
                table: "Timelines",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewDeadline",
                table: "Timelines");

            migrationBuilder.DropColumn(
                name: "SubmissionDeadline",
                table: "Timelines");

            migrationBuilder.AddColumn<int>(
                name: "TimelineId1",
                table: "TimelineSubmissions",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Timelines",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Timelines",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimelineSubmissions_TimelineId1",
                table: "TimelineSubmissions",
                column: "TimelineId1");

            migrationBuilder.AddForeignKey(
                name: "FK_TimelineSubmissions_Timelines_TimelineId1",
                table: "TimelineSubmissions",
                column: "TimelineId1",
                principalTable: "Timelines",
                principalColumn: "Id");
        }
    }
}

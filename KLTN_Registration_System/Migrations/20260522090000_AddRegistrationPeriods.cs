using System;
using KLTN_Registration_System.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260522090000_AddRegistrationPeriods")]
    public partial class AddRegistrationPeriods : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrationPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AcademicYear = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SemesterCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SemesterStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SemesterEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegistrationOpenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegistrationCloseAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationPeriods", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "RegistrationPeriodId",
                table: "Topics",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegistrationPeriodId",
                table: "Timelines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegistrationPeriodId",
                table: "Registrations",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
                DECLARE @DefaultName nvarchar(80) = N'HK2-2025-2026';
                DECLARE @SemesterStart datetime2 = COALESCE(TRY_CONVERT(datetime2, (SELECT TOP 1 [Value] FROM Settings WHERE [Name] = N'Semester_Start')), SYSUTCDATETIME());
                DECLARE @SemesterEnd datetime2 = COALESCE(TRY_CONVERT(datetime2, (SELECT TOP 1 [Value] FROM Settings WHERE [Name] = N'Semester_End')), DATEADD(month, 4, @SemesterStart));
                DECLARE @RegistrationOpen datetime2 = COALESCE(TRY_CONVERT(datetime2, (SELECT TOP 1 [Value] FROM Settings WHERE [Name] = N'Registration_Start')), @SemesterStart);
                DECLARE @RegistrationClose datetime2 = COALESCE(TRY_CONVERT(datetime2, (SELECT TOP 1 [Value] FROM Settings WHERE [Name] = N'Registration_End')), DATEADD(day, 30, @RegistrationOpen));

                INSERT INTO RegistrationPeriods ([Name], AcademicYear, SemesterCode, SemesterStart, SemesterEnd, RegistrationOpenAt, RegistrationCloseAt, IsActive, CreatedAt)
                SELECT DISTINCT
                    LTRIM(RTRIM(Semester)) AS [Name],
                    CASE
                        WHEN LEN(LTRIM(RTRIM(Semester))) >= 9 THEN RIGHT(LTRIM(RTRIM(Semester)), 9)
                        ELSE N'2025-2026'
                    END AS AcademicYear,
                    CASE
                        WHEN CHARINDEX(N'-', LTRIM(RTRIM(Semester))) > 0 THEN LEFT(LTRIM(RTRIM(Semester)), CHARINDEX(N'-', LTRIM(RTRIM(Semester))) - 1)
                        ELSE N'HK2'
                    END AS SemesterCode,
                    @SemesterStart,
                    @SemesterEnd,
                    @RegistrationOpen,
                    @RegistrationClose,
                    CASE WHEN LTRIM(RTRIM(Semester)) = @DefaultName THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                    SYSUTCDATETIME()
                FROM Topics
                WHERE Semester IS NOT NULL
                  AND LTRIM(RTRIM(Semester)) <> N''
                  AND NOT EXISTS (
                      SELECT 1 FROM RegistrationPeriods p
                      WHERE p.[Name] = LTRIM(RTRIM(Topics.Semester))
                  );

                IF NOT EXISTS (SELECT 1 FROM RegistrationPeriods WHERE [Name] = @DefaultName)
                BEGIN
                    INSERT INTO RegistrationPeriods ([Name], AcademicYear, SemesterCode, SemesterStart, SemesterEnd, RegistrationOpenAt, RegistrationCloseAt, IsActive, CreatedAt)
                    VALUES (@DefaultName, N'2025-2026', N'HK2', @SemesterStart, @SemesterEnd, @RegistrationOpen, @RegistrationClose, 1, SYSUTCDATETIME());
                END

                IF NOT EXISTS (SELECT 1 FROM RegistrationPeriods WHERE IsActive = 1)
                BEGIN
                    UPDATE RegistrationPeriods
                    SET IsActive = 1
                    WHERE Id = (SELECT TOP 1 Id FROM RegistrationPeriods ORDER BY Id DESC);
                END

                DECLARE @ActivePeriodId int = (SELECT TOP 1 Id FROM RegistrationPeriods WHERE IsActive = 1 ORDER BY Id DESC);

                UPDATE t
                SET RegistrationPeriodId = p.Id
                FROM Topics t
                INNER JOIN RegistrationPeriods p ON p.[Name] = LTRIM(RTRIM(t.Semester))
                WHERE t.RegistrationPeriodId IS NULL;

                UPDATE Topics
                SET RegistrationPeriodId = @ActivePeriodId
                WHERE RegistrationPeriodId IS NULL;

                UPDATE r
                SET RegistrationPeriodId = t.RegistrationPeriodId
                FROM Registrations r
                INNER JOIN Topics t ON t.Id = r.TopicId
                WHERE r.RegistrationPeriodId IS NULL;

                UPDATE Timelines
                SET RegistrationPeriodId = @ActivePeriodId
                WHERE RegistrationPeriodId IS NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_RegistrationPeriodId",
                table: "Topics",
                column: "RegistrationPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Timelines_RegistrationPeriodId",
                table: "Timelines",
                column: "RegistrationPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_RegistrationPeriodId",
                table: "Registrations",
                column: "RegistrationPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPeriods_IsActive",
                table: "RegistrationPeriods",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPeriods_Name",
                table: "RegistrationPeriods",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Registrations_RegistrationPeriods_RegistrationPeriodId",
                table: "Registrations",
                column: "RegistrationPeriodId",
                principalTable: "RegistrationPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Timelines_RegistrationPeriods_RegistrationPeriodId",
                table: "Timelines",
                column: "RegistrationPeriodId",
                principalTable: "RegistrationPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Topics_RegistrationPeriods_RegistrationPeriodId",
                table: "Topics",
                column: "RegistrationPeriodId",
                principalTable: "RegistrationPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Registrations_RegistrationPeriods_RegistrationPeriodId",
                table: "Registrations");

            migrationBuilder.DropForeignKey(
                name: "FK_Timelines_RegistrationPeriods_RegistrationPeriodId",
                table: "Timelines");

            migrationBuilder.DropForeignKey(
                name: "FK_Topics_RegistrationPeriods_RegistrationPeriodId",
                table: "Topics");

            migrationBuilder.DropTable(
                name: "RegistrationPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Topics_RegistrationPeriodId",
                table: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_Timelines_RegistrationPeriodId",
                table: "Timelines");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_RegistrationPeriodId",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "RegistrationPeriodId",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "RegistrationPeriodId",
                table: "Timelines");

            migrationBuilder.DropColumn(
                name: "RegistrationPeriodId",
                table: "Registrations");
        }
    }
}

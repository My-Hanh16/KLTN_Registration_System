using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicTerm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[RegistrationPeriods]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RegistrationPeriods] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(80) NOT NULL,
        [AcademicYear] nvarchar(20) NOT NULL,
        [SemesterCode] nvarchar(20) NOT NULL,
        [SemesterStart] datetime2 NOT NULL,
        [SemesterEnd] datetime2 NOT NULL,
        [RegistrationOpenAt] datetime2 NOT NULL,
        [RegistrationCloseAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_RegistrationPeriods_IsActive] DEFAULT CAST(0 AS bit),
        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_RegistrationPeriods_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_RegistrationPeriods] PRIMARY KEY ([Id])
    );
END;

IF COL_LENGTH(N'[dbo].[Topics]', N'RegistrationPeriodId') IS NULL
    ALTER TABLE [dbo].[Topics] ADD [RegistrationPeriodId] int NULL;

IF COL_LENGTH(N'[dbo].[Timelines]', N'RegistrationPeriodId') IS NULL
    ALTER TABLE [dbo].[Timelines] ADD [RegistrationPeriodId] int NULL;

IF COL_LENGTH(N'[dbo].[Registrations]', N'RegistrationPeriodId') IS NULL
    ALTER TABLE [dbo].[Registrations] ADD [RegistrationPeriodId] int NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_RegistrationPeriods_Name' AND object_id = OBJECT_ID(N'[dbo].[RegistrationPeriods]'))
    CREATE UNIQUE INDEX [IX_RegistrationPeriods_Name] ON [dbo].[RegistrationPeriods] ([Name]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_RegistrationPeriods_IsActive' AND object_id = OBJECT_ID(N'[dbo].[RegistrationPeriods]'))
    CREATE INDEX [IX_RegistrationPeriods_IsActive] ON [dbo].[RegistrationPeriods] ([IsActive]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Topics_RegistrationPeriodId' AND object_id = OBJECT_ID(N'[dbo].[Topics]'))
    CREATE INDEX [IX_Topics_RegistrationPeriodId] ON [dbo].[Topics] ([RegistrationPeriodId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Timelines_RegistrationPeriodId' AND object_id = OBJECT_ID(N'[dbo].[Timelines]'))
    CREATE INDEX [IX_Timelines_RegistrationPeriodId] ON [dbo].[Timelines] ([RegistrationPeriodId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Registrations_RegistrationPeriodId' AND object_id = OBJECT_ID(N'[dbo].[Registrations]'))
    CREATE INDEX [IX_Registrations_RegistrationPeriodId] ON [dbo].[Registrations] ([RegistrationPeriodId]);

DECLARE @DefaultName nvarchar(80) = N'HK2-2025-2026';
DECLARE @SemesterStart datetime2 = COALESCE(TRY_CONVERT(datetime2, (SELECT TOP 1 [Value] FROM [dbo].[Settings] WHERE [Name] = N'Semester_Start')), SYSUTCDATETIME());
DECLARE @SemesterEnd datetime2 = COALESCE(TRY_CONVERT(datetime2, (SELECT TOP 1 [Value] FROM [dbo].[Settings] WHERE [Name] = N'Semester_End')), DATEADD(month, 4, @SemesterStart));
DECLARE @RegistrationOpen datetime2 = COALESCE(TRY_CONVERT(datetime2, (SELECT TOP 1 [Value] FROM [dbo].[Settings] WHERE [Name] = N'Registration_Start')), @SemesterStart);
DECLARE @RegistrationClose datetime2 = COALESCE(TRY_CONVERT(datetime2, (SELECT TOP 1 [Value] FROM [dbo].[Settings] WHERE [Name] = N'Registration_End')), DATEADD(day, 30, @RegistrationOpen));

INSERT INTO [dbo].[RegistrationPeriods] ([Name], [AcademicYear], [SemesterCode], [SemesterStart], [SemesterEnd], [RegistrationOpenAt], [RegistrationCloseAt], [IsActive], [CreatedAt])
SELECT DISTINCT
    LTRIM(RTRIM([Semester])) AS [Name],
    CASE WHEN LEN(LTRIM(RTRIM([Semester]))) >= 9 THEN RIGHT(LTRIM(RTRIM([Semester])), 9) ELSE N'2025-2026' END AS [AcademicYear],
    CASE WHEN CHARINDEX(N'-', LTRIM(RTRIM([Semester]))) > 0 THEN LEFT(LTRIM(RTRIM([Semester])), CHARINDEX(N'-', LTRIM(RTRIM([Semester]))) - 1) ELSE N'HK2' END AS [SemesterCode],
    @SemesterStart,
    @SemesterEnd,
    @RegistrationOpen,
    @RegistrationClose,
    CASE WHEN LTRIM(RTRIM([Semester])) = @DefaultName THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
    SYSUTCDATETIME()
FROM [dbo].[Topics]
WHERE [Semester] IS NOT NULL
  AND LTRIM(RTRIM([Semester])) <> N''
  AND NOT EXISTS (
      SELECT 1 FROM [dbo].[RegistrationPeriods] p
      WHERE p.[Name] = LTRIM(RTRIM([dbo].[Topics].[Semester]))
  );

IF NOT EXISTS (SELECT 1 FROM [dbo].[RegistrationPeriods] WHERE [Name] = @DefaultName)
BEGIN
    INSERT INTO [dbo].[RegistrationPeriods] ([Name], [AcademicYear], [SemesterCode], [SemesterStart], [SemesterEnd], [RegistrationOpenAt], [RegistrationCloseAt], [IsActive], [CreatedAt])
    VALUES (@DefaultName, N'2025-2026', N'HK2', @SemesterStart, @SemesterEnd, @RegistrationOpen, @RegistrationClose, 1, SYSUTCDATETIME());
END;

IF NOT EXISTS (SELECT 1 FROM [dbo].[RegistrationPeriods] WHERE [IsActive] = 1)
BEGIN
    UPDATE [dbo].[RegistrationPeriods]
    SET [IsActive] = 1
    WHERE [Id] = (SELECT TOP 1 [Id] FROM [dbo].[RegistrationPeriods] ORDER BY [Id] DESC);
END;

DECLARE @ActivePeriodId int = (SELECT TOP 1 [Id] FROM [dbo].[RegistrationPeriods] WHERE [IsActive] = 1 ORDER BY [Id] DESC);

UPDATE t
SET [RegistrationPeriodId] = p.[Id]
FROM [dbo].[Topics] t
INNER JOIN [dbo].[RegistrationPeriods] p ON p.[Name] = LTRIM(RTRIM(t.[Semester]))
WHERE t.[RegistrationPeriodId] IS NULL;

UPDATE [dbo].[Topics]
SET [RegistrationPeriodId] = @ActivePeriodId
WHERE [RegistrationPeriodId] IS NULL;

UPDATE r
SET [RegistrationPeriodId] = t.[RegistrationPeriodId]
FROM [dbo].[Registrations] r
INNER JOIN [dbo].[Topics] t ON t.[Id] = r.[TopicId]
WHERE r.[RegistrationPeriodId] IS NULL;

UPDATE [dbo].[Timelines]
SET [RegistrationPeriodId] = @ActivePeriodId
WHERE [RegistrationPeriodId] IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Topics_RegistrationPeriods_RegistrationPeriodId')
    ALTER TABLE [dbo].[Topics] ADD CONSTRAINT [FK_Topics_RegistrationPeriods_RegistrationPeriodId]
        FOREIGN KEY ([RegistrationPeriodId]) REFERENCES [dbo].[RegistrationPeriods] ([Id]) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Timelines_RegistrationPeriods_RegistrationPeriodId')
    ALTER TABLE [dbo].[Timelines] ADD CONSTRAINT [FK_Timelines_RegistrationPeriods_RegistrationPeriodId]
        FOREIGN KEY ([RegistrationPeriodId]) REFERENCES [dbo].[RegistrationPeriods] ([Id]) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_Registrations_RegistrationPeriods_RegistrationPeriodId')
    ALTER TABLE [dbo].[Registrations] ADD CONSTRAINT [FK_Registrations_RegistrationPeriods_RegistrationPeriodId]
        FOREIGN KEY ([RegistrationPeriodId]) REFERENCES [dbo].[RegistrationPeriods] ([Id]) ON DELETE SET NULL;

IF OBJECT_ID(N'[dbo].[PeriodStudents]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PeriodStudents] (
        [RegistrationPeriodId] int NOT NULL,
        [StudentId] nvarchar(450) NOT NULL,
        [ImportedAt] datetime2 NOT NULL CONSTRAINT [DF_PeriodStudents_ImportedAt] DEFAULT (GETDATE()),
        [IsEligible] bit NOT NULL CONSTRAINT [DF_PeriodStudents_IsEligible] DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_PeriodStudents] PRIMARY KEY ([RegistrationPeriodId], [StudentId]),
        CONSTRAINT [FK_PeriodStudents_RegistrationPeriods_RegistrationPeriodId] FOREIGN KEY ([RegistrationPeriodId]) REFERENCES [dbo].[RegistrationPeriods] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PeriodStudents_AspNetUsers_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PeriodStudents_StudentId' AND object_id = OBJECT_ID(N'[dbo].[PeriodStudents]'))
    CREATE INDEX [IX_PeriodStudents_StudentId] ON [dbo].[PeriodStudents] ([StudentId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PeriodStudents_IsEligible' AND object_id = OBJECT_ID(N'[dbo].[PeriodStudents]'))
    CREATE INDEX [IX_PeriodStudents_IsEligible] ON [dbo].[PeriodStudents] ([IsEligible]);

INSERT INTO [dbo].[PeriodStudents] ([RegistrationPeriodId], [StudentId], [ImportedAt], [IsEligible])
SELECT DISTINCT
    r.[RegistrationPeriodId],
    r.[StudentId],
    SYSUTCDATETIME(),
    CAST(1 AS bit)
FROM [dbo].[Registrations] r
WHERE r.[RegistrationPeriodId] IS NOT NULL
  AND r.[StudentId] IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM [dbo].[PeriodStudents] ps
      WHERE ps.[RegistrationPeriodId] = r.[RegistrationPeriodId]
        AND ps.[StudentId] = r.[StudentId]
  );

DECLARE @ActivePeriodForStudents int = (SELECT TOP 1 [Id] FROM [dbo].[RegistrationPeriods] WHERE [IsActive] = 1 ORDER BY [Id] DESC);

IF @ActivePeriodForStudents IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [dbo].[PeriodStudents])
BEGIN
    INSERT INTO [dbo].[PeriodStudents] ([RegistrationPeriodId], [StudentId], [ImportedAt], [IsEligible])
    SELECT
        @ActivePeriodForStudents,
        u.[Id],
        SYSUTCDATETIME(),
        CAST(1 AS bit)
    FROM [dbo].[AspNetUsers] u
    INNER JOIN [dbo].[AspNetUserRoles] ur ON ur.[UserId] = u.[Id]
    INNER JOIN [dbo].[AspNetRoles] ar ON ar.[Id] = ur.[RoleId]
    WHERE ar.[Name] = N'Student'
      AND NOT EXISTS (
          SELECT 1
          FROM [dbo].[PeriodStudents] ps
          WHERE ps.[RegistrationPeriodId] = @ActivePeriodForStudents
            AND ps.[StudentId] = u.[Id]
      );
END;
");
        }

        /// <inheritdoc />
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

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    /// <inheritdoc />
    public partial class PeriodStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PeriodStudents_IsEligible' AND object_id = OBJECT_ID(N'[dbo].[PeriodStudents]'))
    CREATE INDEX [IX_PeriodStudents_IsEligible] ON [dbo].[PeriodStudents] ([IsEligible]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PeriodStudents_StudentId' AND object_id = OBJECT_ID(N'[dbo].[PeriodStudents]'))
    CREATE INDEX [IX_PeriodStudents_StudentId] ON [dbo].[PeriodStudents] ([StudentId]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[PeriodStudents]', N'U') IS NOT NULL
    DROP TABLE [dbo].[PeriodStudents];
");
        }
    }
}

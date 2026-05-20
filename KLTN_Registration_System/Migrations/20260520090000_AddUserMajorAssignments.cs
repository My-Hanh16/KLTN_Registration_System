using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    public partial class AddUserMajorAssignments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMajors",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MajorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMajors", x => new { x.UserId, x.MajorId });
                    table.ForeignKey(
                        name: "FK_UserMajors_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMajors_Majors_MajorId",
                        column: x => x.MajorId,
                        principalTable: "Majors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMajors_MajorId",
                table: "UserMajors",
                column: "MajorId");

            migrationBuilder.Sql(@"
                INSERT INTO UserMajors (UserId, MajorId)
                SELECT Id, MajorId
                FROM AspNetUsers
                WHERE MajorId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM UserMajors um
                      WHERE um.UserId = AspNetUsers.Id
                        AND um.MajorId = AspNetUsers.MajorId
                  )
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMajors");
        }
    }
}

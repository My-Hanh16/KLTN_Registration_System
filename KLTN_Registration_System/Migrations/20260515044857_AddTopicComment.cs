using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KLTN_Registration_System.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TopicComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    SenderId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AttachmentUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    SenderRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParentCommentId = table.Column<int>(type: "int", nullable: true),
                    TopicId1 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicComments_AspNetUsers_SenderId",
                        column: x => x.SenderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TopicComments_TopicComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "TopicComments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TopicComments_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TopicComments_Topics_TopicId1",
                        column: x => x.TopicId1,
                        principalTable: "Topics",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TopicComments_ParentCommentId",
                table: "TopicComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicComments_SenderId",
                table: "TopicComments",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicComments_TopicId",
                table: "TopicComments",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_TopicComments_TopicId1",
                table: "TopicComments",
                column: "TopicId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TopicComments");
        }
    }
}

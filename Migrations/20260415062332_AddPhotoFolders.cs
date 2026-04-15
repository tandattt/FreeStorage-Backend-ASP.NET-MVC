using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageUploadApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "Images",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "PhotoFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoFolders_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Images_FolderId",
                table: "Images",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_UserId_FolderId",
                table: "Images",
                columns: new[] { "UserId", "FolderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoFolders_UserId",
                table: "PhotoFolders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoFolders_UserId_Name",
                table: "PhotoFolders",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_PhotoFolders_FolderId",
                table: "Images",
                column: "FolderId",
                principalTable: "PhotoFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_PhotoFolders_FolderId",
                table: "Images");

            migrationBuilder.DropTable(
                name: "PhotoFolders");

            migrationBuilder.DropIndex(
                name: "IX_Images_FolderId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_UserId_FolderId",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Images");
        }
    }
}

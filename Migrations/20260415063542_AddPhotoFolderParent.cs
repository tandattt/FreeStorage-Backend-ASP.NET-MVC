using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageUploadApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoFolderParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhotoFolders_UserId_Name",
                table: "PhotoFolders");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentFolderId",
                table: "PhotoFolders",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoFolders_ParentFolderId",
                table: "PhotoFolders",
                column: "ParentFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoFolders_UserId_ParentFolderId_Name",
                table: "PhotoFolders",
                columns: new[] { "UserId", "ParentFolderId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_PhotoFolders_PhotoFolders_ParentFolderId",
                table: "PhotoFolders",
                column: "ParentFolderId",
                principalTable: "PhotoFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PhotoFolders_PhotoFolders_ParentFolderId",
                table: "PhotoFolders");

            migrationBuilder.DropIndex(
                name: "IX_PhotoFolders_ParentFolderId",
                table: "PhotoFolders");

            migrationBuilder.DropIndex(
                name: "IX_PhotoFolders_UserId_ParentFolderId_Name",
                table: "PhotoFolders");

            migrationBuilder.DropColumn(
                name: "ParentFolderId",
                table: "PhotoFolders");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoFolders_UserId_Name",
                table: "PhotoFolders",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageUploadApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineFailedToImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PipelineFailed",
                table: "Images",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PipelineFailed",
                table: "Images");
        }
    }
}

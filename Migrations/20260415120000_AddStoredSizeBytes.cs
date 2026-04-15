using ImageUploadApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageUploadApp.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260415120000_AddStoredSizeBytes")]
public partial class AddStoredSizeBytes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "StoredSizeBytes",
            table: "Images",
            type: "bigint",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "StoredSizeBytes",
            table: "Images");
    }
}

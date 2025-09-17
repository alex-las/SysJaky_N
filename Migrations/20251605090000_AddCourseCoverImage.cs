using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Migrations
{
    public partial class AddCourseCoverImage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Courses",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Courses");
        }
    }
}

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Data.Migrations;

public partial class AddCourseGroup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CourseGroups",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                Name = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CourseGroups", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<int>(
            name: "CourseGroupId",
            table: "Courses",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Courses_CourseGroupId",
            table: "Courses",
            column: "CourseGroupId");

        migrationBuilder.AddForeignKey(
            name: "FK_Courses_CourseGroups_CourseGroupId",
            table: "Courses",
            column: "CourseGroupId",
            principalTable: "CourseGroups",
            principalColumn: "Id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Courses_CourseGroups_CourseGroupId",
            table: "Courses");

        migrationBuilder.DropIndex(
            name: "IX_Courses_CourseGroupId",
            table: "Courses");

        migrationBuilder.DropColumn(
            name: "CourseGroupId",
            table: "Courses");

        migrationBuilder.DropTable(
            name: "CourseGroups");
    }
}

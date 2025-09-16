using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Migrations
{
    public partial class AddAttendance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    CheckedInAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EnrollmentId",
                table: "Attendances",
                column: "EnrollmentId",
                unique: true);

            migrationBuilder.Sql(
                @"INSERT INTO `Attendances` (`EnrollmentId`, `CheckedInAtUtc`)
                  SELECT `Id`, `CheckedInAtUtc` FROM `Enrollments` WHERE `CheckedInAtUtc` IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "CheckedInAtUtc",
                table: "Enrollments");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedInAtUtc",
                table: "Enrollments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.Sql(
                @"UPDATE `Enrollments` e
                  JOIN `Attendances` a ON e.`Id` = a.`EnrollmentId`
                  SET e.`CheckedInAtUtc` = a.`CheckedInAtUtc`;");

            migrationBuilder.DropTable(
                name: "Attendances");
        }
    }
}

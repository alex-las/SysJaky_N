using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Migrations
{
    public partial class AddWaitlistEntries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WaitlistEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CourseTermId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReservationToken = table.Column<Guid>(type: "char(36)", nullable: true),
                    ReservationExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReservationConsumed = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_CourseTerms_CourseTermId",
                        column: x => x.CourseTermId,
                        principalTable: "CourseTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_CourseTermId_CreatedAtUtc",
                table: "WaitlistEntries",
                columns: new[] { "CourseTermId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_UserId_CourseTermId",
                table: "WaitlistEntries",
                columns: new[] { "UserId", "CourseTermId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaitlistEntries");
        }
    }
}

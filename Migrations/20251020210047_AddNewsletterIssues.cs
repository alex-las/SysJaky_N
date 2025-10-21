using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsletterIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "coursecategory_translations",
                keyColumn: "Locale",
                keyValue: null,
                column: "Locale",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Locale",
                table: "coursecategory_translations",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NewsletterIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Subject = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Preheader = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IntroHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OutroHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ScheduledForUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterIssues", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NewsletterSectionCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CourseCategoryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterSectionCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsletterSectionCategories_coursecategories_CourseCategoryId",
                        column: x => x.CourseCategoryId,
                        principalTable: "coursecategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NewsletterIssueCategories",
                columns: table => new
                {
                    NewsletterIssueId = table.Column<int>(type: "int", nullable: false),
                    NewsletterSectionCategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterIssueCategories", x => new { x.NewsletterIssueId, x.NewsletterSectionCategoryId });
                    table.ForeignKey(
                        name: "FK_NewsletterIssueCategories_NewsletterIssues_NewsletterIssueId",
                        column: x => x.NewsletterIssueId,
                        principalTable: "NewsletterIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NewsletterIssueCategories_NewsletterSectionCategories_Newsle~",
                        column: x => x.NewsletterSectionCategoryId,
                        principalTable: "NewsletterSectionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NewsletterSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Title = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HtmlContent = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPublished = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    NewsletterSectionCategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsletterSections_NewsletterSectionCategories_NewsletterSec~",
                        column: x => x.NewsletterSectionCategoryId,
                        principalTable: "NewsletterSectionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NewsletterIssueSections",
                columns: table => new
                {
                    NewsletterIssueId = table.Column<int>(type: "int", nullable: false),
                    NewsletterSectionId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterIssueSections", x => new { x.NewsletterIssueId, x.NewsletterSectionId });
                    table.ForeignKey(
                        name: "FK_NewsletterIssueSections_NewsletterIssues_NewsletterIssueId",
                        column: x => x.NewsletterIssueId,
                        principalTable: "NewsletterIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NewsletterIssueSections_NewsletterSections_NewsletterSection~",
                        column: x => x.NewsletterSectionId,
                        principalTable: "NewsletterSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterIssueCategories_NewsletterSectionCategoryId",
                table: "NewsletterIssueCategories",
                column: "NewsletterSectionCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterIssues_CreatedAtUtc",
                table: "NewsletterIssues",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterIssueSections_NewsletterSectionId",
                table: "NewsletterIssueSections",
                column: "NewsletterSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSectionCategories_CourseCategoryId",
                table: "NewsletterSectionCategories",
                column: "CourseCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSectionCategories_Name",
                table: "NewsletterSectionCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSections_NewsletterSectionCategoryId_SortOrder",
                table: "NewsletterSections",
                columns: new[] { "NewsletterSectionCategoryId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsletterIssueCategories");

            migrationBuilder.DropTable(
                name: "NewsletterIssueSections");

            migrationBuilder.DropTable(
                name: "NewsletterIssues");

            migrationBuilder.DropTable(
                name: "NewsletterSections");

            migrationBuilder.DropTable(
                name: "NewsletterSectionCategories");

            migrationBuilder.AlterColumn<string>(
                name: "Locale",
                table: "coursecategory_translations",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}

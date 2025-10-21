using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsletterTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NewsletterTemplateRegionId",
                table: "NewsletterIssueSections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewsletterTemplateId",
                table: "NewsletterIssues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "NewsletterTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PrimaryColor = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SecondaryColor = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BackgroundColor = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BaseLayoutHtml = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterTemplates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NewsletterTemplateRegions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    NewsletterTemplateId = table.Column<int>(type: "int", nullable: false),
                    NewsletterSectionCategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterTemplateRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsletterTemplateRegions_NewsletterSectionCategories_Newsle~",
                        column: x => x.NewsletterSectionCategoryId,
                        principalTable: "NewsletterSectionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NewsletterTemplateRegions_NewsletterTemplates_NewsletterTemp~",
                        column: x => x.NewsletterTemplateId,
                        principalTable: "NewsletterTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            var now = DateTime.UtcNow;
            var defaultLayout = @"<!DOCTYPE html>
<html>
<body style=""font-family: Arial, sans-serif; color: #111827; background-color: {BACKGROUND}; margin: 0; padding: 0;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" role=""presentation"">
        <tr>
            <td align=""center"" style=""padding: 24px 0;"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" role=""presentation"" style=""background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 30px rgba(15, 23, 42, 0.1);"">
                    <tr>
                        <td style=""padding: 32px 40px;"">{{BODY}}</td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            migrationBuilder.InsertData(
                table: "NewsletterTemplates",
                columns: new[]
                {
                    "Id",
                    "Name",
                    "PrimaryColor",
                    "SecondaryColor",
                    "BackgroundColor",
                    "BaseLayoutHtml",
                    "CreatedAtUtc",
                    "UpdatedAtUtc"
                },
                values: new object[]
                {
                    1,
                    "Výchozí šablona",
                    "#2563eb",
                    "#facc15",
                    "#f9fafb",
                    defaultLayout,
                    now,
                    now
                });

            migrationBuilder.Sql(
                """
                INSERT INTO NewsletterTemplateRegions (Name, SortOrder, NewsletterTemplateId, NewsletterSectionCategoryId)
                SELECT c.Name, c.Id, 1, c.Id
                FROM NewsletterSectionCategories AS c
                ORDER BY c.Id;
                """);

            migrationBuilder.Sql(
                """
                UPDATE NewsletterIssues
                SET NewsletterTemplateId = 1
                WHERE NewsletterTemplateId = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterIssueSections_NewsletterTemplateRegionId",
                table: "NewsletterIssueSections",
                column: "NewsletterTemplateRegionId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterIssues_NewsletterTemplateId",
                table: "NewsletterIssues",
                column: "NewsletterTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterTemplateRegions_NewsletterSectionCategoryId",
                table: "NewsletterTemplateRegions",
                column: "NewsletterSectionCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterTemplateRegions_NewsletterTemplateId",
                table: "NewsletterTemplateRegions",
                column: "NewsletterTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_NewsletterIssues_NewsletterTemplates_NewsletterTemplateId",
                table: "NewsletterIssues",
                column: "NewsletterTemplateId",
                principalTable: "NewsletterTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NewsletterIssueSections_NewsletterTemplateRegions_Newsletter~",
                table: "NewsletterIssueSections",
                column: "NewsletterTemplateRegionId",
                principalTable: "NewsletterTemplateRegions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NewsletterIssues_NewsletterTemplates_NewsletterTemplateId",
                table: "NewsletterIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_NewsletterIssueSections_NewsletterTemplateRegions_Newsletter~",
                table: "NewsletterIssueSections");

            migrationBuilder.DropTable(
                name: "NewsletterTemplateRegions");

            migrationBuilder.DropTable(
                name: "NewsletterTemplates");

            migrationBuilder.DropIndex(
                name: "IX_NewsletterIssueSections_NewsletterTemplateRegionId",
                table: "NewsletterIssueSections");

            migrationBuilder.DropIndex(
                name: "IX_NewsletterIssues_NewsletterTemplateId",
                table: "NewsletterIssues");

            migrationBuilder.DropColumn(
                name: "NewsletterTemplateRegionId",
                table: "NewsletterIssueSections");

            migrationBuilder.DropColumn(
                name: "NewsletterTemplateId",
                table: "NewsletterIssues");
        }
    }
}

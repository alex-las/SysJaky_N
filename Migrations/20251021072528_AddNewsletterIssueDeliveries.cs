using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsletterIssueDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RenderedHtml",
                table: "EmailLogs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NewsletterIssueDeliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    NewsletterIssueId = table.Column<int>(type: "int", nullable: false),
                    NewsletterSubscriberId = table.Column<int>(type: "int", nullable: false),
                    RecipientEmail = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RenderedHtml = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SentUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmailLogId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterIssueDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsletterIssueDeliveries_EmailLogs_EmailLogId",
                        column: x => x.EmailLogId,
                        principalTable: "EmailLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NewsletterIssueDeliveries_NewsletterIssues_NewsletterIssueId",
                        column: x => x.NewsletterIssueId,
                        principalTable: "NewsletterIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NewsletterIssueDeliveries_NewsletterSubscribers_NewsletterSu~",
                        column: x => x.NewsletterSubscriberId,
                        principalTable: "NewsletterSubscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterIssueDeliveries_EmailLogId",
                table: "NewsletterIssueDeliveries",
                column: "EmailLogId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterIssueDeliveries_NewsletterIssueId",
                table: "NewsletterIssueDeliveries",
                column: "NewsletterIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterIssueDeliveries_NewsletterSubscriberId",
                table: "NewsletterIssueDeliveries",
                column: "NewsletterSubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterIssueDeliveries_SentUtc",
                table: "NewsletterIssueDeliveries",
                column: "SentUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsletterIssueDeliveries");

            migrationBuilder.DropColumn(
                name: "RenderedHtml",
                table: "EmailLogs");
        }
    }
}

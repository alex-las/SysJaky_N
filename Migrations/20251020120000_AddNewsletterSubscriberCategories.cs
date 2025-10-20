using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsletterSubscriberCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsletterSubscriberCategories",
                columns: table => new
                {
                    NewsletterSubscriberId = table.Column<int>(type: "int", nullable: false),
                    CourseCategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterSubscriberCategories", x => new { x.NewsletterSubscriberId, x.CourseCategoryId });
                    table.ForeignKey(
                        name: "FK_NewsletterSubscriberCategories_coursecategories_CourseCategoryId",
                        column: x => x.CourseCategoryId,
                        principalTable: "coursecategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NewsletterSubscriberCategories_NewsletterSubscribers_NewsletterSubscriberId",
                        column: x => x.NewsletterSubscriberId,
                        principalTable: "NewsletterSubscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscriberCategories_CourseCategoryId",
                table: "NewsletterSubscriberCategories",
                column: "CourseCategoryId");

            migrationBuilder.Sql(@"
                INSERT INTO `NewsletterSubscriberCategories` (NewsletterSubscriberId, CourseCategoryId)
                SELECT ns.Id, cc.Id
                FROM `NewsletterSubscribers` ns
                CROSS JOIN `coursecategories` cc;
            ");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS `trg_coursecategories_after_insert_subscriber_categories`;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER `trg_coursecategories_after_insert_subscriber_categories`
                AFTER INSERT ON `coursecategories`
                FOR EACH ROW
                INSERT INTO `NewsletterSubscriberCategories` (NewsletterSubscriberId, CourseCategoryId)
                SELECT ns.Id, NEW.Id FROM `NewsletterSubscribers` ns;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS `trg_coursecategories_after_insert_subscriber_categories`;
            ");

            migrationBuilder.DropTable(
                name: "NewsletterSubscriberCategories");
        }
    }
}

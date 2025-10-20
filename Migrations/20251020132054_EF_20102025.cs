using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Migrations
{
    /// <inheritdoc />
    public partial class EF_20102025 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseCategory_Translations_CourseCategories_CategoryId",
                table: "CourseCategory_Translations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CourseCategory_Translations",
                table: "CourseCategory_Translations");

            migrationBuilder.RenameTable(
                name: "CourseCategory_Translations",
                newName: "coursecategory_translations");

            migrationBuilder.RenameIndex(
                name: "IX_CourseCategory_Translations_Locale_Slug",
                table: "coursecategory_translations",
                newName: "IX_coursecategory_translations_Locale_Slug");

            migrationBuilder.AddPrimaryKey(
                name: "PK_coursecategory_translations",
                table: "coursecategory_translations",
                columns: new[] { "CategoryId", "Locale" });

            migrationBuilder.AddForeignKey(
                name: "FK_coursecategory_translations_CourseCategories_CategoryId",
                table: "coursecategory_translations",
                column: "CategoryId",
                principalTable: "CourseCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_coursecategory_translations_CourseCategories_CategoryId",
                table: "coursecategory_translations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_coursecategory_translations",
                table: "coursecategory_translations");

            migrationBuilder.RenameTable(
                name: "coursecategory_translations",
                newName: "CourseCategory_Translations");

            migrationBuilder.RenameIndex(
                name: "IX_coursecategory_translations_Locale_Slug",
                table: "CourseCategory_Translations",
                newName: "IX_CourseCategory_Translations_Locale_Slug");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CourseCategory_Translations",
                table: "CourseCategory_Translations",
                columns: new[] { "CategoryId", "Locale" });

            migrationBuilder.AddForeignKey(
                name: "FK_CourseCategory_Translations_CourseCategories_CategoryId",
                table: "CourseCategory_Translations",
                column: "CategoryId",
                principalTable: "CourseCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

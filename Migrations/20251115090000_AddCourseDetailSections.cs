using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseDetailSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaseStudies",
                table: "Courses",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CertificateInfo",
                table: "Courses",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Certifications",
                table: "Courses",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CourseProgram",
                table: "Courses",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryForm",
                table: "Courses",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DurationText",
                table: "Courses",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FollowUpCourses",
                table: "Courses",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "InstructorBio",
                table: "Courses",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "InstructorName",
                table: "Courses",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IsoStandard",
                table: "Courses",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LearningOutcomes",
                table: "Courses",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OrganizationalNotes",
                table: "Courses",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TargetAudience",
                table: "Courses",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaseStudies",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "CertificateInfo",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "Certifications",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "CourseProgram",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "DeliveryForm",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "DurationText",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "FollowUpCourses",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "InstructorBio",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "InstructorName",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "IsoStandard",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "LearningOutcomes",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "OrganizationalNotes",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "TargetAudience",
                table: "Courses");
        }
    }
}

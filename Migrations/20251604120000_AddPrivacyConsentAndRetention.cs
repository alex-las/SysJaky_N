using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Migrations
{
    public partial class AddPrivacyConsentAndRetention : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MarketingEmailsEnabled",
                table: "AspNetUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MarketingConsentUpdatedAtUtc",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PersonalDataProcessingConsent",
                table: "AspNetUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PersonalDataProcessingConsentUpdatedAtUtc",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarketingEmailsEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MarketingConsentUpdatedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PersonalDataProcessingConsent",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PersonalDataProcessingConsentUpdatedAtUtc",
                table: "AspNetUsers");
        }
    }
}

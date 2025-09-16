using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Data.Migrations;

public partial class AddSalesStats : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SalesStats",
            columns: table => new
            {
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Revenue = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                OrderCount = table.Column<int>(type: "int", nullable: false),
                AverageOrderValue = table.Column<decimal>(type: "decimal(65,30)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SalesStats", x => x.Date);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SalesStats");
    }
}

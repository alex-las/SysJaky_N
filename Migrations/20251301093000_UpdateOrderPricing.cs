using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Migrations
{
    public partial class UpdateOrderPricing : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PriceExclVat",
                table: "Orders",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Orders",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Vat",
                table: "Orders",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "OrderItems",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPriceExclVat",
                table: "OrderItems",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Vat",
                table: "OrderItems",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(@"
                UPDATE `Orders`
                SET
                    `Total` = `TotalPrice`,
                    `PriceExclVat` = ROUND(`TotalPrice` / 1.21, 2),
                    `Vat` = `TotalPrice` - ROUND(`TotalPrice` / 1.21, 2);
            ");

            migrationBuilder.Sql(@"
                UPDATE `OrderItems` AS oi
                LEFT JOIN `Courses` AS c ON c.`Id` = oi.`CourseId`
                SET
                    oi.`Total` = ROUND(COALESCE(c.`Price`, 0) * oi.`Quantity`, 2),
                    oi.`UnitPriceExclVat` = ROUND(COALESCE(c.`Price`, 0) / 1.21, 2),
                    oi.`Vat` = ROUND(COALESCE(c.`Price`, 0) * oi.`Quantity`, 2) - ROUND(COALESCE(c.`Price`, 0) * oi.`Quantity` / 1.21, 2);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceExclVat",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Vat",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "UnitPriceExclVat",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Vat",
                table: "OrderItems");
        }
    }
}

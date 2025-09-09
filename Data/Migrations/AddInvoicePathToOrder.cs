using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SysJaky_N.Data.Migrations;

public partial class AddInvoicePathToOrder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "InvoicePath",
            table: "Orders",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InvoicePath",
            table: "Orders");
    }
}


using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting_System.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotMasterWithholdingRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cw_vat_percent",
                table: "service_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0.05m);

            migrationBuilder.AddColumn<decimal>(
                name: "cwt_percent",
                table: "service_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0.01m);

            migrationBuilder.AddColumn<decimal>(
                name: "cw_vat_percent",
                table: "sales_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0.05m);

            migrationBuilder.AddColumn<decimal>(
                name: "cwt_percent",
                table: "sales_invoices",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0.01m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cw_vat_percent",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "cwt_percent",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "cw_vat_percent",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "cwt_percent",
                table: "sales_invoices");
        }
    }
}

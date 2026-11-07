using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting_System.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDocumentClassificationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "type",
                table: "service_invoices");

            migrationBuilder.DropColumn(
                name: "type",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "type",
                table: "receiving_reports");

            migrationBuilder.DropColumn(
                name: "type",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "type",
                table: "journal_voucher_headers");

            migrationBuilder.DropColumn(
                name: "type",
                table: "debit_memos");

            migrationBuilder.DropColumn(
                name: "type",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "type",
                table: "credit_memos");

            migrationBuilder.DropColumn(
                name: "type",
                table: "collection_receipts");

            migrationBuilder.DropColumn(
                name: "type",
                table: "check_voucher_headers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "service_invoices",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "sales_invoices",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "receiving_reports",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "purchase_orders",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "journal_voucher_headers",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "debit_memos",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "customers",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "credit_memos",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "collection_receipts",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "check_voucher_headers",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);
        }
    }
}

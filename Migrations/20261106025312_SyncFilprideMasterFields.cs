using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting_System.Migrations
{
    /// <inheritdoc />
    public partial class SyncFilprideMasterFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "withholding_tax_percent",
                table: "suppliers",
                type: "numeric(18,4)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "edited_by",
                table: "suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "edited_date",
                table: "suppliers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "employee_number",
                table: "suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "proof_of_exemption_file_name",
                table: "suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "proof_of_registration_file_name",
                table: "suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "requires_price_adjustment",
                table: "suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "supplier_code",
                table: "suppliers",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "edited_by",
                table: "products",
                type: "varchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "edited_date",
                table: "products",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "bir_document_file_name",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bir_document_file_path",
                table: "customers",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cluster_code",
                table: "customers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "commission_rate",
                table: "customers",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "commissionee_id",
                table: "customers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_limit",
                table: "customers",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_limit_as_of_today",
                table: "customers",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "customer_code",
                table: "customers",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cw_vat_percent",
                table: "customers",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cwt_percent",
                table: "customers",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "edited_by",
                table: "customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "edited_date",
                table: "customers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_branch",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_multiple_terms",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "requires_price_adjustment",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "retention_rate",
                table: "customers",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "station_code",
                table: "customers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vat_type",
                table: "customers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "financial_statement_type",
                table: "chart_of_accounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "chart_of_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "account_no",
                table: "bank_accounts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "branch",
                table: "bank_accounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "bank_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "customer_branches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_id = table.Column<int>(type: "integer", nullable: false),
                    branch_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    branch_address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    branch_tin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_branches", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_branches_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "customer_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                UPDATE customers
                SET vat_type = CASE WHEN customer_type IN ('Vatable', 'Zero Rated', 'Exempt') THEN customer_type ELSE 'Vatable' END,
                    cwt_percent = 0.01, cw_vat_percent = 0.05;
                UPDATE customers SET customer_type = 'Industrial'
                WHERE customer_type IN ('Vatable', 'Zero Rated', 'Exempt');
                WITH codes AS (
                    SELECT customer_id,
                        CASE customer_type WHEN 'Retail' THEN 'RET' WHEN 'Industrial' THEN 'IND'
                            WHEN 'Reseller' THEN 'RES' ELSE 'GOV' END AS prefix,
                        ROW_NUMBER() OVER (PARTITION BY CASE customer_type WHEN 'Retail' THEN 'RET'
                            WHEN 'Industrial' THEN 'IND' WHEN 'Reseller' THEN 'RES' ELSE 'GOV' END ORDER BY customer_id) AS n
                    FROM customers
                )
                UPDATE customers c SET customer_code = codes.prefix || LPAD(codes.n::text, 4, '0')
                FROM codes WHERE c.customer_id = codes.customer_id AND codes.n <= 9999;
                WITH codes AS (SELECT supplier_id, ROW_NUMBER() OVER (ORDER BY supplier_id) AS n FROM suppliers)
                UPDATE suppliers s SET supplier_code = 'S' || LPAD(codes.n::text, 6, '0')
                FROM codes WHERE s.supplier_id = codes.supplier_id AND codes.n <= 999999;
                """);
            migrationBuilder.CreateIndex(
                name: "ix_customers_commissionee_id",
                table: "customers",
                column: "commissionee_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_branches_customer_id",
                table: "customer_branches",
                column: "customer_id");

            migrationBuilder.AddForeignKey(
                name: "fk_customers_suppliers_commissionee_id",
                table: "customers",
                column: "commissionee_id",
                principalTable: "suppliers",
                principalColumn: "supplier_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE customers SET customer_type = vat_type WHERE vat_type IN ('Vatable', 'Zero Rated', 'Exempt');");
            migrationBuilder.DropForeignKey(
                name: "fk_customers_suppliers_commissionee_id",
                table: "customers");

            migrationBuilder.DropTable(
                name: "customer_branches");

            migrationBuilder.DropIndex(
                name: "ix_customers_commissionee_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "edited_by",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "edited_date",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "employee_number",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "proof_of_exemption_file_name",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "proof_of_registration_file_name",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "requires_price_adjustment",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "supplier_code",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "edited_by",
                table: "products");

            migrationBuilder.DropColumn(
                name: "edited_date",
                table: "products");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "products");

            migrationBuilder.DropColumn(
                name: "bir_document_file_name",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "bir_document_file_path",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "cluster_code",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "commission_rate",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "commissionee_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "credit_limit",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "credit_limit_as_of_today",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "customer_code",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "cw_vat_percent",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "cwt_percent",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "edited_by",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "edited_date",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "has_branch",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "has_multiple_terms",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "requires_price_adjustment",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "retention_rate",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "station_code",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "vat_type",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "financial_statement_type",
                table: "chart_of_accounts");

            migrationBuilder.DropColumn(
                name: "is_hidden",
                table: "chart_of_accounts");

            migrationBuilder.DropColumn(
                name: "account_no",
                table: "bank_accounts");

            migrationBuilder.DropColumn(
                name: "branch",
                table: "bank_accounts");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "bank_accounts");

            migrationBuilder.AlterColumn<int>(
                name: "withholding_tax_percent",
                table: "suppliers",
                type: "integer",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldNullable: true);
        }
    }
}

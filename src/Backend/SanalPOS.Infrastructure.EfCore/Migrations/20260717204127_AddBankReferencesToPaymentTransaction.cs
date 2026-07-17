using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SanalPOS.Infrastructure.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddBankReferencesToPaymentTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bank_rrn",
                schema: "sanalpos",
                table: "payment_transactions",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_stan",
                schema: "sanalpos",
                table: "payment_transactions",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bank_rrn",
                schema: "sanalpos",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "bank_stan",
                schema: "sanalpos",
                table: "payment_transactions");
        }
    }
}

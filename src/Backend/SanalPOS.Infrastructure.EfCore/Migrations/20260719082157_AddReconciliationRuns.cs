using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SanalPOS.Infrastructure.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reconciliation_runs",
                schema: "sanalpos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sale_count = table.Column<int>(type: "integer", nullable: false),
                    sale_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    refund_count = table.Column<int>(type: "integer", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    void_count = table.Column<int>(type: "integer", nullable: false),
                    void_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_balanced = table.Column<bool>(type: "boolean", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    reason_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reconciliation_runs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_runs_day_provider_code",
                schema: "sanalpos",
                table: "reconciliation_runs",
                columns: new[] { "day", "provider_code" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_runs_executed_at",
                schema: "sanalpos",
                table: "reconciliation_runs",
                column: "executed_at",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reconciliation_runs",
                schema: "sanalpos");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.XApiDb
{
    /// <summary>
    /// SDKV-19/20 (idempotent ingest): index LocalStatement.UUID so
    /// StatementLogic's pre-insert dedupe lookup (retry-safe /Submit and
    /// /Backup) doesn't sequential-scan the statement table.
    /// <para>
    /// Deliberately NON-unique: deployments that ran the pre-fix
    /// double-commit bug (host retries re-POSTing the same statement)
    /// may already hold duplicate UUIDs, and a unique index would fail
    /// to apply on them. Idempotency is enforced by the pre-insert
    /// lookup in the BLL, not by a constraint.
    /// </para>
    /// </summary>
    public partial class StatementUuidDedupeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LocalStatement_UUID",
                table: "LocalStatement",
                column: "UUID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LocalStatement_UUID",
                table: "LocalStatement");
        }
    }
}

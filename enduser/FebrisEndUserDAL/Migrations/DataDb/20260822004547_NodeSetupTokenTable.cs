using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// (first-run claim 2026-08-21): the one-time token that lets whoever can read the node's
    /// STDOUT claim it by creating the first ITAdmin.
    ///
    /// <para>
    /// Replaces a compiled-in seeded admin, which is a reasonable shape for unattended automation
    /// and a poor one for an open-source project: it put an admin password in a file on disk,
    /// required editing configuration before first boot, and with nothing configured produced an
    /// account at a reserved example.com address with NO password -- unreachable by construction,
    /// since that domain cannot receive the password-reset mail the flow depended on. The
    /// environment-variable seed remains as the unattended door.
    /// </para>
    ///
    /// <para>
    /// <c>TokenHash</c> is UNIQUE for the same reason as the invitation store: the claim finds the
    /// row BY that hash, so a duplicate would make which token a claim redeems depend on row order.
    /// The token itself is NEVER stored and never written through Serilog -- only to stdout.
    /// </para>
    ///
    /// <para>
    /// Consumed rows are KEPT rather than deleted. They are the audit record of when the node was
    /// claimed and by whom, which is the one question nobody can answer afterwards otherwise.
    /// </para>
    ///
    /// <para>
    /// Named <c>NodeSetupTokenTable</c>, not <c>NodeSetupToken</c>, so it does not duplicate the
    /// model type's simple name across projects (duplicate-type ratchet).
    /// </para>
    /// </summary>
    public partial class NodeSetupTokenTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NodeSetupToken",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConsumedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsumedByEmail = table.Column<string>(type: "text", nullable: true),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeSetupToken", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NodeSetupToken_TokenHash",
                table: "NodeSetupToken",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NodeSetupToken");
        }
    }
}

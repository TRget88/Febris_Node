using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// (invitation flow 2026-08-21): admin-issued invitations to create an account on this node.
    /// The account is created when the invitation is ACCEPTED, so an invitation nobody takes up
    /// leaves nothing behind but this row.
    ///
    /// <para>
    /// <c>TokenHash</c> is UNIQUE, and that index is load-bearing rather than hygiene: redemption
    /// finds the invitation BY hashing the presented token and looking it up, so a duplicate would
    /// make which invitation a link redeems depend on row order. It is also the reason the hash is
    /// a fast deterministic SHA-256 rather than a salted KDF -- there is no other handle to look
    /// the row up by.
    /// </para>
    ///
    /// <para>
    /// The token itself is NEVER stored. The column holds only its hash, which is the first of
    /// three points where this table deliberately differs from the central tier's
    /// <c>ContentDeveloperUserInvite</c>; the other two are enforced recipient binding and
    /// <c>RevokedAt</c>. All three are documented in that type's own source as known gaps.
    /// </para>
    ///
    /// <para>
    /// Scaffolded clean: one CreateTable, one CreateIndex, and a purely additive snapshot delta
    /// with no pre-existing DataDb drift re-emitted, so no hand-trimming was needed (the same
    /// happy outcome as NodeRegistrationConfigTable, and unlike the older settings migrations that
    /// carry warnings about it). Verified against a real migrated database rather than assumed.
    /// </para>
    ///
    /// <para>
    /// Named <c>NodeUserInviteTable</c>, not <c>NodeUserInvite</c>, so it does not duplicate the
    /// model type's simple name across projects -- the duplicate-type ratchet counts those.
    /// </para>
    /// </summary>
    public partial class NodeUserInviteTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NodeUserInvite",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedByEmail = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConsumedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RevokedByEmail = table.Column<string>(type: "text", nullable: true),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeUserInvite", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NodeUserInvite_TokenHash",
                table: "NodeUserInvite",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NodeUserInvite");
        }
    }
}

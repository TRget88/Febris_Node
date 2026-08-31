using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// (hub-pull sync; owner-ratified 2026-07-17): the node's persisted hub-federation
    /// settings -- exactly one row, written by the portal's Hub Federation admin page (no seeder:
    /// absence means the operator never opted in). When the row exists it GOVERNS the ONE
    /// hub-federation gate over the legacy configuration resolution; <c>LicenseKey</c> stores the
    /// IDataProtection payload (dedicated protector purpose, see
    /// <c>HubFederationConfigQueries</c>), never plaintext.
    ///
    /// <para>
    /// Hand-trimmed to the real delta (this one CreateTable), per the
    /// LocalVocabularyStores/LocalModuleCatalog/NodeLocalIdentity 42P07 precedent: a raw scaffold
    /// re-emits the KNOWN pre-existing DataDb snapshot drift (NET8 Hardware table-split remap,
    /// Hardware1 drop, ParentLinkedStudent, ContentDeveloper columns) describing schema the
    /// shipped DBs already have. That drift is deliberately KEPT VISIBLE in the snapshot (its
    /// reconciliation is a tracked, owner-gated roadmap item); the snapshot/Designer here were
    /// hand-merged with only the HubFederationConfig entity added. The migration class is named
    /// HubFederationConfigTable (not HubFederationConfig) so it does not duplicate the model
    /// type's name across projects (duplicate-type ratchet).
    /// </para>
    /// </summary>
    public partial class HubFederationConfigTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HubFederationConfig",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DataApi = table.Column<string>(type: "text", nullable: true),
                    AuthenticationApi = table.Column<string>(type: "text", nullable: true),
                    LicenseKey = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HubFederationConfig", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HubFederationConfig");
        }
    }
}

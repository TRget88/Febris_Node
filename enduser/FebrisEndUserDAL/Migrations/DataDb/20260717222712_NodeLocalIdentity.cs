using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// Auth severance: the node's LOCAL single-tenant identity table --
    /// exactly one row, seeded idempotently at provision time by <c>NodeIdentitySeeder</c>
    /// (InstitutionUUID generated once and persisted). Replaces the License-claim-derived
    /// institution identity when no hub license is present.
    ///
    /// <para>
    /// Hand-trimmed to the real delta (this one CreateTable). The raw scaffold again picked up
    /// the PRE-EXISTING snapshot drift (NET8 Hardware table-split remap, Hardware1 drop,
    /// ParentLinkedStudent, ContentDeveloper columns) describing schema the shipped DBs already
    /// have; excluded per the LocalVocabularyStores 42P07 precedent, and the snapshot/Designer
    /// were hand-merged so that drift stays VISIBLE to future scaffolds instead of being
    /// silently buried. The migration class is named NodeLocalIdentity (not NodeIdentity) so it
    /// does not duplicate the model type's name across projects (duplicate-type ratchet).
    /// </para>
    /// </summary>
    public partial class NodeLocalIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NodeIdentity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    InstitutionUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeIdentity", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NodeIdentity");
        }
    }
}

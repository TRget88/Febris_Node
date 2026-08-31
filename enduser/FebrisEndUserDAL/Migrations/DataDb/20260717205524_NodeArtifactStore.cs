using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// Node artifact store -- hand-trimmed to THIS slice's real delta.
    ///
    /// <para>
    /// Creates the node's client-software catalog (LocalSoftwarePackage -- previously CENTRAL-only;
    /// the tenant proxied it over HTTP and never had the table) and the node-only PackageArtifact
    /// bookkeeping table (storage key + SHA-256 + length for every binary ingested through
    /// IStorageProvider; unique on StorageKey). Both are plain creates: neither table has ever
    /// existed anywhere in the tenant migration chain.
    /// </para>
    ///
    /// <para>
    /// As with 20260717203315_LocalModuleCatalog (see its header for the full rationale), the
    /// as-scaffolded version also replayed PRE-EXISTING model-vs-snapshot drift the shipped tenant
    /// databases already have (NET8 Hardware table-split remap, Hardware1 drop,
    /// ParentLinkedStudent, ContentDeveloper columns). Those operations are excluded, and the
    /// Designer/snapshot were hand-merged to add ONLY the two new entities so the drift stays
    /// visible to future scaffolds. Edited before ever being merged or deployed, so the
    /// never-edit-a-shipped-migration rule does not apply.
    /// </para>
    /// </summary>
    public partial class NodeArtifactStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalSoftwarePackage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Obsolete = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LocalSoftwarePackageType = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalSoftwarePackage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PackageArtifact",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StorageKey = table.Column<string>(type: "text", nullable: true),
                    Sha256 = table.Column<string>(type: "text", nullable: true),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false),
                    SourceFileName = table.Column<string>(type: "text", nullable: true),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageArtifact", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageArtifact_StorageKey",
                table: "PackageArtifact",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalSoftwarePackage");

            migrationBuilder.DropTable(
                name: "PackageArtifact");
        }
    }
}

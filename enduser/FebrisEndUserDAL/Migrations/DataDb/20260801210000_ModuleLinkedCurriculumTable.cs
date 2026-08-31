using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// Modules > Curricula, node-locally (owner ruling 2026-08-01). The node already owned
    /// <c>Module</c> and <c>ModuleClassification</c> as its own DbSets, and reached
    /// <c>Curriculum</c> / <c>CurriculumClassification</c> by convention through the existing link
    /// entities' navigations -- both tables have shipped since the Initial migration. The
    /// cohort end of the chain (<c>CohortLinkedCurriculum</c>) has shipped just as long.
    ///
    /// <para>
    /// The one missing edge was Module-to-Curriculum: that join existed ONLY hub-side, behind
    /// <c>Remote/ModuleLinkedCurriculumQueries</c>, so a self-hosted node could not answer "which
    /// modules does this curriculum contain" without calling infrastructure it does not have.
    /// This adds it. The chain is now entirely local:
    /// Cohort -> CohortLinkedCurriculum -> Curriculum -> ModuleLinkedCurriculum -> Module.
    /// </para>
    ///
    /// <para>
    /// Hand-trimmed to the real delta (this one CreateTable plus its two indexes), per the
    /// LocalVocabularyStores / LocalModuleCatalog / NodeLocalIdentity / HubFederationConfigTable
    /// precedent: a raw scaffold re-emits the KNOWN pre-existing DataDb snapshot drift (NET8
    /// Hardware table-split remap, Hardware1 drop, ParentLinkedStudent, ContentDeveloper columns)
    /// describing schema the shipped DBs already have. That drift stays deliberately visible in
    /// the snapshot; its reconciliation is a tracked, owner-gated roadmap item. Column shape,
    /// nullability, FK delete behaviour and index naming all copy CohortLinkedCurriculum in the
    /// Initial migration, so the two link tables stay consistent.
    /// </para>
    ///
    /// <para>
    /// The migration class is named <c>ModuleLinkedCurriculumTable</c> rather than
    /// <c>ModuleLinkedCurriculum</c> so it does not duplicate the model type's name across
    /// projects (duplicate-type ratchet), matching HubFederationConfigTable.
    /// </para>
    /// </summary>
    public partial class ModuleLinkedCurriculumTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModuleLinkedCurriculum",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CurriculumId = table.Column<long>(type: "bigint", nullable: true),
                    CurriculumUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<long>(type: "bigint", nullable: true),
                    ModuleUUID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleLinkedCurriculum", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleLinkedCurriculum_Curriculum_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curriculum",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModuleLinkedCurriculum_Module_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Module",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModuleLinkedCurriculum_CurriculumId",
                table: "ModuleLinkedCurriculum",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleLinkedCurriculum_ModuleId",
                table: "ModuleLinkedCurriculum",
                column: "ModuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModuleLinkedCurriculum");
        }
    }
}

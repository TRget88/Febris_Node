using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <summary>
    /// Local module catalog -- hand-trimmed to THIS slice's real delta.
    ///
    /// <para>
    /// Creates the node-local catalog tables: Module + ModuleClassification (originally created by
    /// the 2022 Initial migration, then DROPPED by 20220726211838_updates when the catalog moved
    /// central-side -- so at the end of the shipped chain they do NOT exist and a plain create is
    /// correct, the inverse of the LocalVocabularyStores convergence case) and ModuleLinkedObject
    /// (previously central-only; never existed in the tenant schema).
    /// </para>
    ///
    /// <para>
    /// The as-scaffolded version of this migration also picked up PRE-EXISTING model-vs-snapshot
    /// drift unrelated to this slice (the NET8 Hardware/LocalHardware table-split remap, a
    /// Hardware1 drop, ParentLinkedStudent, ContentDeveloper PendingSelfSignUp/SubscriptionRate
    /// columns). Those differences describe schema the shipped tenant databases ALREADY have (they
    /// were provisioned by EnsureCreated from newer models, not by this migration chain), so
    /// replaying them here would fail on a real database -- the same 42P07 class of failure the
    /// LocalVocabularyStores header documents. They are excluded; the accompanying
    /// Designer/snapshot were likewise hand-merged to add ONLY the three catalog entities on top of
    /// the previous snapshot, so the pre-existing drift stays visible to future scaffolds instead
    /// of being silently buried. Edited before ever being merged or deployed, so the
    /// never-edit-a-shipped-migration rule does not apply.
    /// </para>
    /// </summary>
    public partial class LocalModuleCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModuleClassification",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Obsolete = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleClassification", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Module",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Obsolete = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ModuleClassificationId = table.Column<long>(type: "bigint", nullable: true),
                    ModuleClassificationUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    XApiInteractionType = table.Column<int>(type: "integer", nullable: false),
                    MainSectionCount = table.Column<int>(type: "integer", nullable: false),
                    TotalSectionCount = table.Column<int>(type: "integer", nullable: false),
                    InteractionComponents = table.Column<string>(type: "text", nullable: true),
                    EstimatedCompletionTime = table.Column<int>(type: "integer", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Module", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Module_ModuleClassification_ModuleClassificationId",
                        column: x => x.ModuleClassificationId,
                        principalTable: "ModuleClassification",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ModuleLinkedObject",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ObjectId = table.Column<long>(type: "bigint", nullable: false),
                    ObjectUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<long>(type: "bigint", nullable: true),
                    ModuleUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleLinkedObject", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleLinkedObject_Module_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Module",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Module_ModuleClassificationId",
                table: "Module",
                column: "ModuleClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleLinkedObject_ModuleId",
                table: "ModuleLinkedObject",
                column: "ModuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModuleLinkedObject");

            migrationBuilder.DropTable(
                name: "Module");

            migrationBuilder.DropTable(
                name: "ModuleClassification");
        }
    }
}

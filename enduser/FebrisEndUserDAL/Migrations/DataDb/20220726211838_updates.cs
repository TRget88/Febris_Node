using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    public partial class updates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCohort_Cohort_CohortId",
                table: "HardwareLinkedCohort");

            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCohort_Hardware_HardwareId",
                table: "HardwareLinkedCohort");

            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCurriculum_Curriculum_CurriculumId",
                table: "HardwareLinkedCurriculum");

            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware1_HardwareId",
                table: "HardwareLinkedCurriculum");

            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedModule_Hardware_HardwareId",
                table: "HardwareLinkedModule");

            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedModule_Module_ModuleId",
                table: "HardwareLinkedModule");

            migrationBuilder.DropTable(
                name: "Module");

            migrationBuilder.DropTable(
                name: "ModuleClassification");

            migrationBuilder.DropIndex(
                name: "IX_HardwareLinkedModule_ModuleId",
                table: "HardwareLinkedModule");

            migrationBuilder.AlterColumn<long>(
                name: "ModuleId",
                table: "HardwareLinkedModule",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "HardwareId",
                table: "HardwareLinkedModule",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "HardwareId",
                table: "HardwareLinkedCurriculum",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CurriculumId",
                table: "HardwareLinkedCurriculum",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "HardwareId",
                table: "HardwareLinkedCohort",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CohortId",
                table: "HardwareLinkedCohort",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCohort_Cohort_CohortId",
                table: "HardwareLinkedCohort",
                column: "CohortId",
                principalTable: "Cohort",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCohort_Hardware_HardwareId",
                table: "HardwareLinkedCohort",
                column: "HardwareId",
                principalTable: "Hardware",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCurriculum_Curriculum_CurriculumId",
                table: "HardwareLinkedCurriculum",
                column: "CurriculumId",
                principalTable: "Curriculum",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware1_HardwareId",
                table: "HardwareLinkedCurriculum",
                column: "HardwareId",
                principalTable: "Hardware1",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedModule_Hardware_HardwareId",
                table: "HardwareLinkedModule",
                column: "HardwareId",
                principalTable: "Hardware",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCohort_Cohort_CohortId",
                table: "HardwareLinkedCohort");

            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCohort_Hardware_HardwareId",
                table: "HardwareLinkedCohort");

            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCurriculum_Curriculum_CurriculumId",
                table: "HardwareLinkedCurriculum");

            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware1_HardwareId",
                table: "HardwareLinkedCurriculum");

            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedModule_Hardware_HardwareId",
                table: "HardwareLinkedModule");

            migrationBuilder.AlterColumn<long>(
                name: "ModuleId",
                table: "HardwareLinkedModule",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<long>(
                name: "HardwareId",
                table: "HardwareLinkedModule",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<long>(
                name: "HardwareId",
                table: "HardwareLinkedCurriculum",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<long>(
                name: "CurriculumId",
                table: "HardwareLinkedCurriculum",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<long>(
                name: "HardwareId",
                table: "HardwareLinkedCohort",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<long>(
                name: "CohortId",
                table: "HardwareLinkedCohort",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long));

            migrationBuilder.CreateTable(
                name: "ModuleClassification",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Obsolete = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Description = table.Column<string>(type: "text", nullable: true),
                    EstimatedCompletionTime = table.Column<int>(type: "integer", nullable: false),
                    InteractionComponents = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    MainSectionCount = table.Column<int>(type: "integer", nullable: false),
                    ModuleClassificationId = table.Column<long>(type: "bigint", nullable: true),
                    ModuleClassificationUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Obsolete = table.Column<bool>(type: "boolean", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalSectionCount = table.Column<int>(type: "integer", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: true),
                    XApiInteractionType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Module", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Module_ModuleClassification_ModuleClassificationId",
                        column: x => x.ModuleClassificationId,
                        principalTable: "ModuleClassification",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HardwareLinkedModule_ModuleId",
                table: "HardwareLinkedModule",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Module_ModuleClassificationId",
                table: "Module",
                column: "ModuleClassificationId");

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCohort_Cohort_CohortId",
                table: "HardwareLinkedCohort",
                column: "CohortId",
                principalTable: "Cohort",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCohort_Hardware_HardwareId",
                table: "HardwareLinkedCohort",
                column: "HardwareId",
                principalTable: "Hardware",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCurriculum_Curriculum_CurriculumId",
                table: "HardwareLinkedCurriculum",
                column: "CurriculumId",
                principalTable: "Curriculum",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware1_HardwareId",
                table: "HardwareLinkedCurriculum",
                column: "HardwareId",
                principalTable: "Hardware1",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedModule_Hardware_HardwareId",
                table: "HardwareLinkedModule",
                column: "HardwareId",
                principalTable: "Hardware",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedModule_Module_ModuleId",
                table: "HardwareLinkedModule",
                column: "ModuleId",
                principalTable: "Module",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

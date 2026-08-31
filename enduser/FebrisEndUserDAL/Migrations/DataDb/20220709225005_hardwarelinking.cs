using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    public partial class hardwarelinking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hardware_HardwareType_HardwareTypeId",
                table: "Hardware");

            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware_HardwareId",
                table: "HardwareLinkedCurriculum");

            migrationBuilder.DropForeignKey(
                name: "FK_LocationLinkedHardware_Hardware_HardwareId",
                table: "LocationLinkedHardware");

            migrationBuilder.DropTable(
                name: "DailyUse");

            migrationBuilder.DropIndex(
                name: "IX_Hardware_HardwareTypeId",
                table: "Hardware");

            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "HardwareLinkedModule",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "uuid_generate_v4()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeStamp",
                table: "HardwareLinkedModule",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastUpdateTimeStamp",
                table: "HardwareLinkedModule",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<long>(
                name: "HardwareTypeId",
                table: "Hardware",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "CohortMember",
                nullable: false,
                defaultValueSql: "uuid_generate_v4()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeStamp",
                table: "CohortMember",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastUpdateTimeStamp",
                table: "CohortMember",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AddColumn<bool>(
                name: "Archive",
                table: "Cohort",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LockMembers",
                table: "Cohort",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Hardware1",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    HardwareTypeUUID = table.Column<Guid>(nullable: false),
                    HardwareTypeId = table.Column<long>(nullable: true),
                    DescriptiveName = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    PhysicalLicense = table.Column<string>(nullable: true),
                    HardwareCondition = table.Column<int>(nullable: false),
                    IsLockedOut = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hardware1", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hardware1_HardwareType_HardwareTypeId",
                        column: x => x.HardwareTypeId,
                        principalTable: "HardwareType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HardwareLinkedCohort",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    HardwareId = table.Column<long>(nullable: true),
                    HardwareUUID = table.Column<Guid>(nullable: false),
                    CohortId = table.Column<long>(nullable: true),
                    CohortUUID = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardwareLinkedCohort", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardwareLinkedCohort_Cohort_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohort",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HardwareLinkedCohort_Hardware_HardwareId",
                        column: x => x.HardwareId,
                        principalTable: "Hardware",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hardware1_HardwareTypeId",
                table: "Hardware1",
                column: "HardwareTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_HardwareLinkedCohort_CohortId",
                table: "HardwareLinkedCohort",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_HardwareLinkedCohort_HardwareId",
                table: "HardwareLinkedCohort",
                column: "HardwareId");

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware1_HardwareId",
                table: "HardwareLinkedCurriculum",
                column: "HardwareId",
                principalTable: "Hardware1",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LocationLinkedHardware_Hardware1_HardwareId",
                table: "LocationLinkedHardware",
                column: "HardwareId",
                principalTable: "Hardware1",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware1_HardwareId",
                table: "HardwareLinkedCurriculum");

            migrationBuilder.DropForeignKey(
                name: "FK_LocationLinkedHardware_Hardware1_HardwareId",
                table: "LocationLinkedHardware");

            migrationBuilder.DropTable(
                name: "Hardware1");

            migrationBuilder.DropTable(
                name: "HardwareLinkedCohort");

            migrationBuilder.DropColumn(
                name: "Archive",
                table: "Cohort");

            migrationBuilder.DropColumn(
                name: "LockMembers",
                table: "Cohort");

            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "HardwareLinkedModule",
                type: "uuid",
                nullable: false,
                defaultValueSql: "uuid_generate_v4()",
                oldClrType: typeof(Guid));

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeStamp",
                table: "HardwareLinkedModule",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastUpdateTimeStamp",
                table: "HardwareLinkedModule",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime));

            migrationBuilder.AlterColumn<long>(
                name: "HardwareTypeId",
                table: "Hardware",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long));

            migrationBuilder.AlterColumn<Guid>(
                name: "UUID",
                table: "CohortMember",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldDefaultValueSql: "uuid_generate_v4()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeStamp",
                table: "CohortMember",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastUpdateTimeStamp",
                table: "CohortMember",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.CreateTable(
                name: "DailyUse",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContentDeveloperId = table.Column<long>(type: "bigint", nullable: false),
                    ContentDeveloperUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    InstitutionTypeId = table.Column<long>(type: "bigint", nullable: true),
                    InstitutionUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TenantType = table.Column<int>(type: "integer", nullable: false),
                    TestingModuleTotal = table.Column<int>(type: "integer", nullable: false),
                    TestingTimeDuration = table.Column<double>(type: "double precision", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TrainingModuleTotal = table.Column<int>(type: "integer", nullable: false),
                    TrainingTimeDuration = table.Column<double>(type: "double precision", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    VideoByteSize = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyUse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyUse_ContentDeveloper_ContentDeveloperId",
                        column: x => x.ContentDeveloperId,
                        principalTable: "ContentDeveloper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailyUse_Institution_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institution",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailyUse_InstitutionType_InstitutionTypeId",
                        column: x => x.InstitutionTypeId,
                        principalTable: "InstitutionType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hardware_HardwareTypeId",
                table: "Hardware",
                column: "HardwareTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyUse_ContentDeveloperId",
                table: "DailyUse",
                column: "ContentDeveloperId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyUse_InstitutionId",
                table: "DailyUse",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyUse_InstitutionTypeId",
                table: "DailyUse",
                column: "InstitutionTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Hardware_HardwareType_HardwareTypeId",
                table: "Hardware",
                column: "HardwareTypeId",
                principalTable: "HardwareType",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HardwareLinkedCurriculum_Hardware_HardwareId",
                table: "HardwareLinkedCurriculum",
                column: "HardwareId",
                principalTable: "Hardware",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LocationLinkedHardware_Hardware_HardwareId",
                table: "LocationLinkedHardware",
                column: "HardwareId",
                principalTable: "Hardware",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

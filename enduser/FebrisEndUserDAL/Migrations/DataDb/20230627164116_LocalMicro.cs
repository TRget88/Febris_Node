using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    public partial class LocalMicro : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "LocationUUID",
                table: "MessageBoard",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "InstitutionUUID",
                table: "MessageBoard",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContentDeveloperUUID",
                table: "MessageBoard",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "AccreditationBodyUUID",
                table: "MessageBoard",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "InstitutionTypeUUID",
                table: "Institution",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "InstitutionSettingsUUID",
                table: "Institution",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "DeploymentTypeUUID",
                table: "Institution",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "HardwareTypeUUID",
                table: "Hardware1",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "HardwareTypeUUID",
                table: "Hardware",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "CurriculumClassificationUUID",
                table: "Curriculum",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ContentDeveloperSettingsUUID",
                table: "ContentDeveloper",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceChargeRate",
                table: "ContentDeveloper",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "LocalMicrocredentialLinkedActor",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ValidityExpires = table.Column<bool>(nullable: false),
                    ValidityExparationDate = table.Column<DateTime>(nullable: true),
                    MicrocredentialId = table.Column<long>(nullable: false),
                    MicrocredentialUUID = table.Column<Guid>(nullable: false),
                    ActorId = table.Column<long>(nullable: false),
                    ActorUUID = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalMicrocredentialLinkedActor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MicrocredentialProgress",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CompletionTimeFrame = table.Column<DateTime>(nullable: true),
                    InProgress = table.Column<bool>(nullable: false),
                    Complete = table.Column<bool>(nullable: false),
                    ValidStatementIdList = table.Column<List<long>>(nullable: true),
                    ValidStatementUUIDList = table.Column<List<Guid>>(nullable: true),
                    CompletedObjectIdList = table.Column<List<long>>(nullable: true),
                    CompletedObjectUUIDList = table.Column<List<Guid>>(nullable: true),
                    ActorId = table.Column<long>(nullable: false),
                    ActorUUID = table.Column<Guid>(nullable: false),
                    ReferenceObjectIdList = table.Column<List<long>>(nullable: true),
                    ReferenceObjectUUIDList = table.Column<List<Guid>>(nullable: true),
                    MicrocredentialId = table.Column<long>(nullable: true),
                    MicrocredentialUUID = table.Column<Guid>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MicrocredentialProgress", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalMicrocredentialLinkedActor");

            migrationBuilder.DropTable(
                name: "MicrocredentialProgress");

            migrationBuilder.DropColumn(
                name: "ServiceChargeRate",
                table: "ContentDeveloper");

            migrationBuilder.AlterColumn<Guid>(
                name: "LocationUUID",
                table: "MessageBoard",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "InstitutionUUID",
                table: "MessageBoard",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ContentDeveloperUUID",
                table: "MessageBoard",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccreditationBodyUUID",
                table: "MessageBoard",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "InstitutionTypeUUID",
                table: "Institution",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "InstitutionSettingsUUID",
                table: "Institution",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DeploymentTypeUUID",
                table: "Institution",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "HardwareTypeUUID",
                table: "Hardware1",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "HardwareTypeUUID",
                table: "Hardware",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CurriculumClassificationUUID",
                table: "Curriculum",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ContentDeveloperSettingsUUID",
                table: "ContentDeveloper",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldNullable: true);
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <inheritdoc />
    public partial class DropNodeMicrocredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalMicrocredentialLinkedActor");

            migrationBuilder.DropTable(
                name: "MicrocredentialProgress");

            migrationBuilder.DropColumn(
                name: "MicroCredentialAvailable",
                table: "Curriculum");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MicroCredentialAvailable",
                table: "Curriculum",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LocalMicrocredentialLinkedActor",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorId = table.Column<long>(type: "bigint", nullable: false),
                    ActorUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    MicrocredentialId = table.Column<long>(type: "bigint", nullable: false),
                    MicrocredentialUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ValidityExparationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ValidityExpires = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalMicrocredentialLinkedActor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MicrocredentialProgress",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorId = table.Column<long>(type: "bigint", nullable: false),
                    ActorUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    Complete = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedObjectIdList = table.Column<List<long>>(type: "bigint[]", nullable: true),
                    CompletedObjectUUIDList = table.Column<List<Guid>>(type: "uuid[]", nullable: true),
                    CompletionTimeFrame = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InProgress = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    MicrocredentialId = table.Column<long>(type: "bigint", nullable: true),
                    MicrocredentialUUID = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenceObjectIdList = table.Column<List<long>>(type: "bigint[]", nullable: true),
                    ReferenceObjectUUIDList = table.Column<List<Guid>>(type: "uuid[]", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ValidStatementIdList = table.Column<List<long>>(type: "bigint[]", nullable: true),
                    ValidStatementUUIDList = table.Column<List<Guid>>(type: "uuid[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MicrocredentialProgress", x => x.Id);
                });
        }
    }
}

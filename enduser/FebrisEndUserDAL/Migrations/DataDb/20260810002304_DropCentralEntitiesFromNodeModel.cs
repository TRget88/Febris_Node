using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <inheritdoc />
    public partial class DropCentralEntitiesFromNodeModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageBoard_AccreditationBody_AccreditationBodyId",
                table: "MessageBoard");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageBoard_ContentDeveloper_ContentDeveloperId",
                table: "MessageBoard");

            migrationBuilder.DropForeignKey(
                name: "FK_MessageBoard_Institution_InstitutionId",
                table: "MessageBoard");

            migrationBuilder.DropTable(
                name: "AccreditationBody");

            migrationBuilder.DropTable(
                name: "ContentDeveloper");

            migrationBuilder.DropTable(
                name: "Institution");

            migrationBuilder.DropTable(
                name: "AccreditationBodySettings");

            migrationBuilder.DropTable(
                name: "ContentDeveloperSettings");

            migrationBuilder.DropTable(
                name: "ContentDeveloperType");

            migrationBuilder.DropTable(
                name: "DeploymentType");

            migrationBuilder.DropTable(
                name: "InstitutionSettings");

            migrationBuilder.DropTable(
                name: "InstitutionType");

            migrationBuilder.DropIndex(
                name: "IX_MessageBoard_AccreditationBodyId",
                table: "MessageBoard");

            migrationBuilder.DropIndex(
                name: "IX_MessageBoard_ContentDeveloperId",
                table: "MessageBoard");

            migrationBuilder.DropIndex(
                name: "IX_MessageBoard_InstitutionId",
                table: "MessageBoard");

            migrationBuilder.DropColumn(
                name: "AccreditationBodyId",
                table: "MessageBoard");

            migrationBuilder.DropColumn(
                name: "ContentDeveloperId",
                table: "MessageBoard");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                table: "MessageBoard");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AccreditationBodyId",
                table: "MessageBoard",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ContentDeveloperId",
                table: "MessageBoard",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "InstitutionId",
                table: "MessageBoard",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccreditationBodySettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllowEmailAddressesOutsideOfDomain = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EmailDomain = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    ForceMultifactorAuthentication = table.Column<bool>(type: "boolean", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    POCEmail = table.Column<string>(type: "text", nullable: true),
                    POCPhoneNumber = table.Column<string>(type: "text", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false),
                    Website = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccreditationBodySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentDeveloperSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllowEmailAddressesOutsideOfDomain = table.Column<bool>(type: "boolean", nullable: false),
                    ApplyDiscountToEntireProductRange = table.Column<bool>(type: "boolean", nullable: false),
                    AutoConnectAllLibraryCurriculum = table.Column<bool>(type: "boolean", nullable: false),
                    AutoDownloadLibraryAcrossHardware = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EmailDomain = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    ForceMultifactorAuthentication = table.Column<bool>(type: "boolean", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    POCEmail = table.Column<string>(type: "text", nullable: true),
                    POCPhoneNumber = table.Column<string>(type: "text", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false),
                    Website = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentDeveloperSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentDeveloperType",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentDeveloperType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentType",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstitutionSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AllowEmailAddressesOutsideOfDomain = table.Column<bool>(type: "boolean", nullable: false),
                    AllowMigrations = table.Column<bool>(type: "boolean", nullable: false),
                    AllowPrivateDeployments = table.Column<bool>(type: "boolean", nullable: false),
                    AutoConnectAllLibraryCurriculum = table.Column<bool>(type: "boolean", nullable: false),
                    AutoDownloadLibraryAcrossHardware = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EmailDomain = table.Column<string>(type: "text", nullable: true),
                    FERPACompliance = table.Column<bool>(type: "boolean", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    ForceMultifactorAuthentication = table.Column<bool>(type: "boolean", nullable: false),
                    ForcePasswordValidityTimespan = table.Column<bool>(type: "boolean", nullable: false),
                    IsFederallyTaxExempt = table.Column<bool>(type: "boolean", nullable: false),
                    IsNonProfit = table.Column<bool>(type: "boolean", nullable: false),
                    IsStateTaxExempt = table.Column<bool>(type: "boolean", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    MaxVideoStorage = table.Column<int>(type: "integer", nullable: false),
                    POCEmail = table.Column<string>(type: "text", nullable: true),
                    POCPhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PasswordValidityInMonths = table.Column<int>(type: "integer", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false),
                    UseMaxVideoStorage = table.Column<bool>(type: "boolean", nullable: false),
                    UseVideoStrorageTimeSpan = table.Column<bool>(type: "boolean", nullable: false),
                    VideoStorageOption = table.Column<bool>(type: "boolean", nullable: false),
                    VideoStorageTimeSpan = table.Column<int>(type: "integer", nullable: false),
                    Website = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstitutionType",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccreditationBody",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccreditationBodySettingsId = table.Column<long>(type: "bigint", nullable: true),
                    AccreditationBodySettingsUUID = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    IsLockedOut = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    MaxTestUserAccounts = table.Column<int>(type: "integer", nullable: false),
                    MaxVideoStorage = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false),
                    ZipCode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccreditationBody", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccreditationBody_AccreditationBodySettings_AccreditationBo~",
                        column: x => x.AccreditationBodySettingsId,
                        principalTable: "AccreditationBodySettings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContentDeveloper",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContentDeveloperSettingsId = table.Column<long>(type: "bigint", nullable: true),
                    ContentDeveloperTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    ConnectionToken = table.Column<string>(type: "text", nullable: true),
                    ContentDeveloperSettingsUUID = table.Column<Guid>(type: "uuid", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    IsLockedOut = table.Column<bool>(type: "boolean", nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    MaxTestUserAccounts = table.Column<int>(type: "integer", nullable: false),
                    MaxVideoStorage = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    PaymentInfoVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ServiceChargeRate = table.Column<decimal>(type: "numeric", nullable: false),
                    State = table.Column<string>(type: "text", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false),
                    ZipCode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentDeveloper", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentDeveloper_ContentDeveloperSettings_ContentDeveloperS~",
                        column: x => x.ContentDeveloperSettingsId,
                        principalTable: "ContentDeveloperSettings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ContentDeveloper_ContentDeveloperType_ContentDeveloperTypeId",
                        column: x => x.ContentDeveloperTypeId,
                        principalTable: "ContentDeveloperType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Institution",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeploymentTypeId = table.Column<long>(type: "bigint", nullable: true),
                    InstitutionSettingsId = table.Column<long>(type: "bigint", nullable: true),
                    InstitutionTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    DeploymentTypeUUID = table.Column<Guid>(type: "uuid", nullable: true),
                    InstitutionSettingsUUID = table.Column<Guid>(type: "uuid", nullable: true),
                    InstitutionTypeUUID = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdateTimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    MaxTestUserAccounts = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    TaxRate = table.Column<decimal>(type: "numeric", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: true),
                    UUID = table.Column<Guid>(type: "uuid", nullable: false),
                    ZipCode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Institution_DeploymentType_DeploymentTypeId",
                        column: x => x.DeploymentTypeId,
                        principalTable: "DeploymentType",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Institution_InstitutionSettings_InstitutionSettingsId",
                        column: x => x.InstitutionSettingsId,
                        principalTable: "InstitutionSettings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Institution_InstitutionType_InstitutionTypeId",
                        column: x => x.InstitutionTypeId,
                        principalTable: "InstitutionType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageBoard_AccreditationBodyId",
                table: "MessageBoard",
                column: "AccreditationBodyId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageBoard_ContentDeveloperId",
                table: "MessageBoard",
                column: "ContentDeveloperId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageBoard_InstitutionId",
                table: "MessageBoard",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AccreditationBody_AccreditationBodySettingsId",
                table: "AccreditationBody",
                column: "AccreditationBodySettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentDeveloper_ContentDeveloperSettingsId",
                table: "ContentDeveloper",
                column: "ContentDeveloperSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentDeveloper_ContentDeveloperTypeId",
                table: "ContentDeveloper",
                column: "ContentDeveloperTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Institution_DeploymentTypeId",
                table: "Institution",
                column: "DeploymentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Institution_InstitutionSettingsId",
                table: "Institution",
                column: "InstitutionSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_Institution_InstitutionTypeId",
                table: "Institution",
                column: "InstitutionTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageBoard_AccreditationBody_AccreditationBodyId",
                table: "MessageBoard",
                column: "AccreditationBodyId",
                principalTable: "AccreditationBody",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageBoard_ContentDeveloper_ContentDeveloperId",
                table: "MessageBoard",
                column: "ContentDeveloperId",
                principalTable: "ContentDeveloper",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageBoard_Institution_InstitutionId",
                table: "MessageBoard",
                column: "InstitutionId",
                principalTable: "Institution",
                principalColumn: "Id");
        }
    }
}

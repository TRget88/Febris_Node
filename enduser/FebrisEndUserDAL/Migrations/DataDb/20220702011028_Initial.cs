using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "AccreditationBodySettings",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    AllowEmailAddressesOutsideOfDomain = table.Column<bool>(nullable: false),
                    ForceMultifactorAuthentication = table.Column<bool>(nullable: false),
                    EmailDomain = table.Column<string>(nullable: true),
                    Logo = table.Column<string>(nullable: true),
                    Website = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    FirstName = table.Column<string>(nullable: true),
                    LastName = table.Column<string>(nullable: true),
                    POCEmail = table.Column<string>(nullable: true),
                    POCPhoneNumber = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccreditationBodySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cohort",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    InstructorId = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cohort", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentDeveloperSettings",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    AutoDownloadLibraryAcrossHardware = table.Column<bool>(nullable: false),
                    AutoConnectAllLibraryCurriculum = table.Column<bool>(nullable: false),
                    AllowEmailAddressesOutsideOfDomain = table.Column<bool>(nullable: false),
                    ApplyDiscountToEntireProductRange = table.Column<bool>(nullable: false),
                    ForceMultifactorAuthentication = table.Column<bool>(nullable: false),
                    EmailDomain = table.Column<string>(nullable: true),
                    Logo = table.Column<string>(nullable: true),
                    Website = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    FirstName = table.Column<string>(nullable: true),
                    LastName = table.Column<string>(nullable: true),
                    POCEmail = table.Column<string>(nullable: true),
                    POCPhoneNumber = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentDeveloperSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentDeveloperType",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentDeveloperType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumClassification",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    Obsolete = table.Column<bool>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumClassification", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentType",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HardwareType",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardwareType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstitutionSettings",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    AutoDownloadLibraryAcrossHardware = table.Column<bool>(nullable: false),
                    AutoConnectAllLibraryCurriculum = table.Column<bool>(nullable: false),
                    IsStateTaxExempt = table.Column<bool>(nullable: false),
                    IsFederallyTaxExempt = table.Column<bool>(nullable: false),
                    IsNonProfit = table.Column<bool>(nullable: false),
                    AllowMigrations = table.Column<bool>(nullable: false),
                    AllowEmailAddressesOutsideOfDomain = table.Column<bool>(nullable: false),
                    ForceMultifactorAuthentication = table.Column<bool>(nullable: false),
                    AllowPrivateDeployments = table.Column<bool>(nullable: false),
                    ForcePasswordValidityTimespan = table.Column<bool>(nullable: false),
                    PasswordValidityInMonths = table.Column<int>(nullable: false),
                    FERPACompliance = table.Column<bool>(nullable: false),
                    EmailDomain = table.Column<string>(nullable: true),
                    Logo = table.Column<string>(nullable: true),
                    Website = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    VideoStorageOption = table.Column<bool>(nullable: false),
                    UseVideoStrorageTimeSpan = table.Column<bool>(nullable: false),
                    VideoStorageTimeSpan = table.Column<int>(nullable: false),
                    UseMaxVideoStorage = table.Column<bool>(nullable: false),
                    MaxVideoStorage = table.Column<int>(nullable: false),
                    FirstName = table.Column<string>(nullable: true),
                    LastName = table.Column<string>(nullable: true),
                    POCEmail = table.Column<string>(nullable: true),
                    POCPhoneNumber = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstitutionType",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Location",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Name = table.Column<string>(nullable: true),
                    Address = table.Column<string>(nullable: true),
                    City = table.Column<string>(nullable: true),
                    ZipCode = table.Column<string>(nullable: true),
                    State = table.Column<string>(nullable: true),
                    Country = table.Column<string>(nullable: true),
                    Longitude = table.Column<double>(nullable: false),
                    Latitude = table.Column<double>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Location", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuleClassification",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    Obsolete = table.Column<bool>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleClassification", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestUser",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UserName = table.Column<string>(nullable: true),
                    FirstName = table.Column<string>(nullable: true),
                    LastName = table.Column<string>(nullable: true),
                    IdentificationNumber = table.Column<string>(nullable: true),
                    PhotoOfProfessional = table.Column<string>(nullable: true),
                    PhoneNumber = table.Column<string>(nullable: true),
                    ActorId = table.Column<Guid>(nullable: false),
                    EmailAddress = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestUser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccreditationBody",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    AccreditationBodySettingsId = table.Column<long>(nullable: true),
                    AccreditationBodySettingsUUID = table.Column<Guid>(nullable: false),
                    IsLockedOut = table.Column<bool>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Address = table.Column<string>(nullable: true),
                    City = table.Column<string>(nullable: true),
                    ZipCode = table.Column<string>(nullable: true),
                    State = table.Column<string>(nullable: true),
                    Country = table.Column<string>(nullable: true),
                    Longitude = table.Column<double>(nullable: false),
                    Latitude = table.Column<double>(nullable: false),
                    MaxVideoStorage = table.Column<int>(nullable: false),
                    MaxTestUserAccounts = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccreditationBody", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccreditationBody_AccreditationBodySettings_AccreditationBo~",
                        column: x => x.AccreditationBodySettingsId,
                        principalTable: "AccreditationBodySettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortMember",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    UserId = table.Column<Guid>(nullable: false),
                    CohortId = table.Column<long>(nullable: true),
                    CohortUUID = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortMember", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CohortMember_Cohort_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohort",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentDeveloper",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    ContentDeveloperSettingsId = table.Column<long>(nullable: true),
                    ContentDeveloperSettingsUUID = table.Column<Guid>(nullable: false),
                    ContentDeveloperTypeId = table.Column<long>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    Address = table.Column<string>(nullable: true),
                    City = table.Column<string>(nullable: true),
                    ZipCode = table.Column<string>(nullable: true),
                    State = table.Column<string>(nullable: true),
                    Country = table.Column<string>(nullable: true),
                    Longitude = table.Column<double>(nullable: false),
                    Latitude = table.Column<double>(nullable: false),
                    ConnectionToken = table.Column<string>(nullable: true),
                    IsLockedOut = table.Column<bool>(nullable: false),
                    MaxVideoStorage = table.Column<int>(nullable: false),
                    MaxTestUserAccounts = table.Column<int>(nullable: false),
                    PaymentInfoVerified = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentDeveloper", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentDeveloper_ContentDeveloperSettings_ContentDeveloperS~",
                        column: x => x.ContentDeveloperSettingsId,
                        principalTable: "ContentDeveloperSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentDeveloper_ContentDeveloperType_ContentDeveloperTypeId",
                        column: x => x.ContentDeveloperTypeId,
                        principalTable: "ContentDeveloperType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Curriculum",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    Obsolete = table.Column<bool>(nullable: false),
                    CurriculumClassificationId = table.Column<long>(nullable: true),
                    CurriculumClassificationUUID = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    Version = table.Column<string>(nullable: true),
                    MicroCredentialAvailable = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Curriculum", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Curriculum_CurriculumClassification_CurriculumClassificatio~",
                        column: x => x.CurriculumClassificationId,
                        principalTable: "CurriculumClassification",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Hardware",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
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
                    table.PrimaryKey("PK_Hardware", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hardware_HardwareType_HardwareTypeId",
                        column: x => x.HardwareTypeId,
                        principalTable: "HardwareType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Institution",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    InstitutionSettingsId = table.Column<long>(nullable: true),
                    InstitutionSettingsUUID = table.Column<Guid>(nullable: false),
                    InstitutionTypeId = table.Column<long>(nullable: true),
                    InstitutionTypeUUID = table.Column<Guid>(nullable: false),
                    DeploymentTypeId = table.Column<long>(nullable: true),
                    DeploymentTypeUUID = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Address = table.Column<string>(nullable: true),
                    City = table.Column<string>(nullable: true),
                    State = table.Column<string>(nullable: true),
                    ZipCode = table.Column<string>(nullable: true),
                    Country = table.Column<string>(nullable: true),
                    MaxTestUserAccounts = table.Column<int>(nullable: false),
                    Longitude = table.Column<double>(nullable: false),
                    Latitude = table.Column<double>(nullable: false),
                    Token = table.Column<string>(nullable: true),
                    TaxRate = table.Column<decimal>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Institution_DeploymentType_DeploymentTypeId",
                        column: x => x.DeploymentTypeId,
                        principalTable: "DeploymentType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Institution_InstitutionSettings_InstitutionSettingsId",
                        column: x => x.InstitutionSettingsId,
                        principalTable: "InstitutionSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Institution_InstitutionType_InstitutionTypeId",
                        column: x => x.InstitutionTypeId,
                        principalTable: "InstitutionType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CohortLinkedLocation",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CohortId = table.Column<long>(nullable: true),
                    CohortUUID = table.Column<Guid>(nullable: false),
                    LocationId = table.Column<long>(nullable: true),
                    LocationUUID = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortLinkedLocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CohortLinkedLocation_Cohort_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohort",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortLinkedLocation_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationLinkedUser",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LocationId = table.Column<long>(nullable: true),
                    LocationUUID = table.Column<Guid>(nullable: false),
                    UserId = table.Column<Guid>(nullable: false),
                    AttachmentStatus = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationLinkedUser", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationLinkedUser_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Module",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    TimeStamp = table.Column<DateTime>(nullable: false),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false),
                    Obsolete = table.Column<bool>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Version = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    ModuleClassificationId = table.Column<long>(nullable: true),
                    ModuleClassificationUUID = table.Column<Guid>(nullable: false),
                    Language = table.Column<int>(nullable: false),
                    XApiInteractionType = table.Column<int>(nullable: false),
                    MainSectionCount = table.Column<int>(nullable: false),
                    TotalSectionCount = table.Column<int>(nullable: false),
                    InteractionComponents = table.Column<string>(nullable: true),
                    EstimatedCompletionTime = table.Column<int>(nullable: false)
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

            migrationBuilder.CreateTable(
                name: "CohortLinkedCurriculum",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CohortId = table.Column<long>(nullable: true),
                    CohortUUID = table.Column<Guid>(nullable: false),
                    CurriculumId = table.Column<long>(nullable: true),
                    CurriculumUUID = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CohortLinkedCurriculum", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CohortLinkedCurriculum_Cohort_CohortId",
                        column: x => x.CohortId,
                        principalTable: "Cohort",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CohortLinkedCurriculum_Curriculum_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curriculum",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HardwareLinkedCurriculum",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    HardwareId = table.Column<long>(nullable: true),
                    HardwareUUID = table.Column<Guid>(nullable: false),
                    CurriculumId = table.Column<long>(nullable: true),
                    CurriculumUUID = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardwareLinkedCurriculum", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardwareLinkedCurriculum_Curriculum_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curriculum",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HardwareLinkedCurriculum_Hardware_HardwareId",
                        column: x => x.HardwareId,
                        principalTable: "Hardware",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationLinkedHardware",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    HardwareId = table.Column<long>(nullable: true),
                    HardwareUUID = table.Column<Guid>(nullable: false),
                    LocationId = table.Column<long>(nullable: true),
                    LocationUUID = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationLinkedHardware", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationLinkedHardware_Hardware_HardwareId",
                        column: x => x.HardwareId,
                        principalTable: "Hardware",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocationLinkedHardware_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyUse",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Date = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TenantType = table.Column<int>(nullable: false),
                    InstitutionTypeId = table.Column<long>(nullable: true),
                    TrainingModuleTotal = table.Column<int>(nullable: false),
                    TestingModuleTotal = table.Column<int>(nullable: false),
                    TrainingTimeDuration = table.Column<double>(nullable: false),
                    TestingTimeDuration = table.Column<double>(nullable: false),
                    VideoByteSize = table.Column<long>(nullable: false),
                    ContentDeveloperId = table.Column<long>(nullable: false),
                    ContentDeveloperUUID = table.Column<Guid>(nullable: false),
                    InstitutionId = table.Column<long>(nullable: false),
                    InstitutionUUID = table.Column<Guid>(nullable: false)
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

            migrationBuilder.CreateTable(
                name: "MessageBoard",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Archive = table.Column<bool>(nullable: false),
                    Subject = table.Column<string>(nullable: true),
                    Message = table.Column<string>(nullable: true),
                    UserId = table.Column<Guid>(nullable: false),
                    UserName = table.Column<string>(nullable: true),
                    UserEmail = table.Column<string>(nullable: true),
                    InstitutionId = table.Column<long>(nullable: true),
                    InstitutionUUID = table.Column<Guid>(nullable: false),
                    LocationId = table.Column<long>(nullable: true),
                    LocationUUID = table.Column<Guid>(nullable: false),
                    ContentDeveloperId = table.Column<long>(nullable: true),
                    ContentDeveloperUUID = table.Column<Guid>(nullable: false),
                    AccreditationBodyId = table.Column<long>(nullable: true),
                    AccreditationBodyUUID = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageBoard", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageBoard_AccreditationBody_AccreditationBodyId",
                        column: x => x.AccreditationBodyId,
                        principalTable: "AccreditationBody",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageBoard_ContentDeveloper_ContentDeveloperId",
                        column: x => x.ContentDeveloperId,
                        principalTable: "ContentDeveloper",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageBoard_Institution_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institution",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageBoard_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HardwareLinkedModule",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    HardwareId = table.Column<long>(nullable: true),
                    HardwareUUID = table.Column<Guid>(nullable: false),
                    ModuleId = table.Column<long>(nullable: true),
                    ModuleUUID = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardwareLinkedModule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardwareLinkedModule_Hardware_HardwareId",
                        column: x => x.HardwareId,
                        principalTable: "Hardware",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HardwareLinkedModule_Module_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Module",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccreditationBody_AccreditationBodySettingsId",
                table: "AccreditationBody",
                column: "AccreditationBodySettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortLinkedCurriculum_CohortId",
                table: "CohortLinkedCurriculum",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortLinkedCurriculum_CurriculumId",
                table: "CohortLinkedCurriculum",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortLinkedLocation_CohortId",
                table: "CohortLinkedLocation",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortLinkedLocation_LocationId",
                table: "CohortLinkedLocation",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CohortMember_CohortId",
                table: "CohortMember",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentDeveloper_ContentDeveloperSettingsId",
                table: "ContentDeveloper",
                column: "ContentDeveloperSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentDeveloper_ContentDeveloperTypeId",
                table: "ContentDeveloper",
                column: "ContentDeveloperTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Curriculum_CurriculumClassificationId",
                table: "Curriculum",
                column: "CurriculumClassificationId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Hardware_HardwareTypeId",
                table: "Hardware",
                column: "HardwareTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_HardwareLinkedCurriculum_CurriculumId",
                table: "HardwareLinkedCurriculum",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_HardwareLinkedCurriculum_HardwareId",
                table: "HardwareLinkedCurriculum",
                column: "HardwareId");

            migrationBuilder.CreateIndex(
                name: "IX_HardwareLinkedModule_HardwareId",
                table: "HardwareLinkedModule",
                column: "HardwareId");

            migrationBuilder.CreateIndex(
                name: "IX_HardwareLinkedModule_ModuleId",
                table: "HardwareLinkedModule",
                column: "ModuleId");

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

            migrationBuilder.CreateIndex(
                name: "IX_LocationLinkedHardware_HardwareId",
                table: "LocationLinkedHardware",
                column: "HardwareId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationLinkedHardware_LocationId",
                table: "LocationLinkedHardware",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationLinkedUser_LocationId",
                table: "LocationLinkedUser",
                column: "LocationId");

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
                name: "IX_MessageBoard_LocationId",
                table: "MessageBoard",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Module_ModuleClassificationId",
                table: "Module",
                column: "ModuleClassificationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CohortLinkedCurriculum");

            migrationBuilder.DropTable(
                name: "CohortLinkedLocation");

            migrationBuilder.DropTable(
                name: "CohortMember");

            migrationBuilder.DropTable(
                name: "DailyUse");

            migrationBuilder.DropTable(
                name: "HardwareLinkedCurriculum");

            migrationBuilder.DropTable(
                name: "HardwareLinkedModule");

            migrationBuilder.DropTable(
                name: "LocationLinkedHardware");

            migrationBuilder.DropTable(
                name: "LocationLinkedUser");

            migrationBuilder.DropTable(
                name: "MessageBoard");

            migrationBuilder.DropTable(
                name: "TestUser");

            migrationBuilder.DropTable(
                name: "Cohort");

            migrationBuilder.DropTable(
                name: "Curriculum");

            migrationBuilder.DropTable(
                name: "Module");

            migrationBuilder.DropTable(
                name: "Hardware");

            migrationBuilder.DropTable(
                name: "AccreditationBody");

            migrationBuilder.DropTable(
                name: "ContentDeveloper");

            migrationBuilder.DropTable(
                name: "Institution");

            migrationBuilder.DropTable(
                name: "Location");

            migrationBuilder.DropTable(
                name: "CurriculumClassification");

            migrationBuilder.DropTable(
                name: "ModuleClassification");

            migrationBuilder.DropTable(
                name: "HardwareType");

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
        }
    }
}

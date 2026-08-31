using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Febris.UserNode.DataAccessLayer.Migrations
{
    public partial class AddedHardware : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModuleDownloadAnalytics",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TimeSpan = table.Column<TimeSpan>(nullable: true),
                    IPAddress = table.Column<string>(nullable: true),
                    UserAgent = table.Column<string>(nullable: true),
                    Query = table.Column<string>(nullable: true),
                    Referer = table.Column<string>(nullable: true),
                    Path = table.Column<string>(nullable: true),
                    SourceId = table.Column<string>(nullable: true),
                    Visits = table.Column<int>(nullable: true),
                    GeoIPDataId = table.Column<long>(nullable: false),
                    LicenseId = table.Column<long>(nullable: true),
                    LicenseUUID = table.Column<Guid>(nullable: true),
                    HardwareId = table.Column<long>(nullable: true),
                    HardwareUUID = table.Column<Guid>(nullable: true),
                    ContentDeveloperId = table.Column<long>(nullable: true),
                    ContentDeveloperUUID = table.Column<Guid>(nullable: true),
                    AccreditationBodyId = table.Column<long>(nullable: true),
                    AccreditationBodyUUID = table.Column<Guid>(nullable: true),
                    UserId = table.Column<Guid>(nullable: true),
                    FebrisUser = table.Column<bool>(nullable: true),
                    ModuleId = table.Column<long>(nullable: true),
                    ModuleUUID = table.Column<Guid>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleDownloadAnalytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuleUsageAnalytics",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TimeSpan = table.Column<TimeSpan>(nullable: true),
                    IPAddress = table.Column<string>(nullable: true),
                    UserAgent = table.Column<string>(nullable: true),
                    Query = table.Column<string>(nullable: true),
                    Referer = table.Column<string>(nullable: true),
                    Path = table.Column<string>(nullable: true),
                    SourceId = table.Column<string>(nullable: true),
                    Visits = table.Column<int>(nullable: true),
                    GeoIPDataId = table.Column<long>(nullable: false),
                    LicenseId = table.Column<long>(nullable: true),
                    LicenseUUID = table.Column<Guid>(nullable: true),
                    HardwareId = table.Column<long>(nullable: true),
                    HardwareUUID = table.Column<Guid>(nullable: true),
                    ContentDeveloperId = table.Column<long>(nullable: true),
                    ContentDeveloperUUID = table.Column<Guid>(nullable: true),
                    AccreditationBodyId = table.Column<long>(nullable: true),
                    AccreditationBodyUUID = table.Column<Guid>(nullable: true),
                    UserId = table.Column<Guid>(nullable: true),
                    FebrisUser = table.Column<bool>(nullable: true),
                    ModuleId = table.Column<long>(nullable: true),
                    ModuleUUID = table.Column<Guid>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleUsageAnalytics", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModuleDownloadAnalytics");

            migrationBuilder.DropTable(
                name: "ModuleUsageAnalytics");
        }
    }
}

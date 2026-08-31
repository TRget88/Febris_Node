using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Febris.UserNode.DataAccessLayer.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "LocalAnalytics",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TimeSpan = table.Column<TimeSpan>(nullable: true),
                    IPAddress = table.Column<string>(nullable: true),
                    UserAgent = table.Column<string>(nullable: true),
                    Query = table.Column<string>(nullable: true),
                    Referer = table.Column<string>(nullable: true),
                    Path = table.Column<string>(nullable: true),
                    SourceId = table.Column<string>(nullable: true),
                    Visits = table.Column<int>(nullable: true),
                    GeoIPDataId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalAnalytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAnalytics",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    TimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdateTimeStamp = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TimeSpan = table.Column<TimeSpan>(nullable: true),
                    IPAddress = table.Column<string>(nullable: true),
                    UserAgent = table.Column<string>(nullable: true),
                    Query = table.Column<string>(nullable: true),
                    Referer = table.Column<string>(nullable: true),
                    Path = table.Column<string>(nullable: true),
                    SourceId = table.Column<string>(nullable: true),
                    Visits = table.Column<int>(nullable: true),
                    GeoIPDataId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAnalytics", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalAnalytics");

            migrationBuilder.DropTable(
                name: "UserAnalytics");
        }
    }
}

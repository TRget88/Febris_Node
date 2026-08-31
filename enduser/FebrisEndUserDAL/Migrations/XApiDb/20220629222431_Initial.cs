using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Febris.UserNode.DataAccessLayer.Migrations.XApiDb
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "Account",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    HomePage = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Account", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContextActivities",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Parent = table.Column<string>(nullable: true),
                    Grouping = table.Column<string>(nullable: true),
                    Category = table.Column<string>(nullable: true),
                    Other = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContextActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Extensions",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ExtensionMap = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Extensions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Member",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Member", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Score",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Scaled = table.Column<float>(nullable: false),
                    Raw = table.Column<float>(nullable: false),
                    Min = table.Column<float>(nullable: false),
                    Max = table.Column<float>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Score", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatementReference",
                columns: table => new
                {
                    Key = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Id = table.Column<Guid>(nullable: false),
                    ObjectType = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementReference", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Verb",
                columns: table => new
                {
                    Key = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    Id = table.Column<string>(nullable: true),
                    Display = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Verb", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Version",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    VersionNumber = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Version", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Definition",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    Type = table.Column<string>(nullable: true),
                    MoreInfo = table.Column<string>(nullable: true),
                    ExtensionsId = table.Column<long>(nullable: true),
                    InteractionType = table.Column<string>(nullable: true),
                    CorrectResponsesPattern = table.Column<string>(nullable: true),
                    InteractionComponents = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Definition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Definition_Extensions_ExtensionsId",
                        column: x => x.ExtensionsId,
                        principalTable: "Extensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Result",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ScoreId = table.Column<long>(nullable: true),
                    Success = table.Column<bool>(nullable: false),
                    Completion = table.Column<bool>(nullable: false),
                    Response = table.Column<string>(nullable: true),
                    Duration = table.Column<TimeSpan>(nullable: false),
                    ExtensionsId = table.Column<long>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Result", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Result_Extensions_ExtensionsId",
                        column: x => x.ExtensionsId,
                        principalTable: "Extensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Result_Score_ScoreId",
                        column: x => x.ScoreId,
                        principalTable: "Score",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Object",
                columns: table => new
                {
                    Key = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    Id = table.Column<string>(nullable: true),
                    ObjectType = table.Column<string>(nullable: true),
                    DefinitionId = table.Column<long>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Object", x => x.Key);
                    table.ForeignKey(
                        name: "FK_Object_Definition_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "Definition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "XApiResultExtras",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ResultId = table.Column<long>(nullable: true),
                    ResultUUID = table.Column<Guid>(nullable: false),
                    RestartCount = table.Column<int>(nullable: false),
                    NotesList = table.Column<List<string>>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XApiResultExtras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XApiResultExtras_Result_ResultId",
                        column: x => x.ResultId,
                        principalTable: "Result",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Actor",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ObjectType = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    Mbox = table.Column<string>(nullable: true),
                    Mbox_sha1sum = table.Column<string>(nullable: true),
                    OpenId = table.Column<string>(nullable: true),
                    AccountId = table.Column<long>(nullable: true),
                    MemberId = table.Column<long>(nullable: true),
                    ContextId = table.Column<long>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Actor_Account_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Actor_Member_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Member",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Authority",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ActorId = table.Column<long>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Authority", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Authority_Actor_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Actor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Context",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Registration = table.Column<Guid>(nullable: false),
                    InstructorId = table.Column<long>(nullable: true),
                    ContextActivitiesId = table.Column<long>(nullable: true),
                    Revision = table.Column<string>(nullable: true),
                    Platform = table.Column<string>(nullable: true),
                    Language = table.Column<string>(nullable: true),
                    StatementReferenceKey = table.Column<long>(nullable: true),
                    ExtensionsId = table.Column<long>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Context", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Context_ContextActivities_ContextActivitiesId",
                        column: x => x.ContextActivitiesId,
                        principalTable: "ContextActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Context_Extensions_ExtensionsId",
                        column: x => x.ExtensionsId,
                        principalTable: "Extensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Context_Actor_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Actor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Context_StatementReference_StatementReferenceKey",
                        column: x => x.StatementReferenceKey,
                        principalTable: "StatementReference",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocalStatement",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Timestamp = table.Column<DateTime>(nullable: false),
                    Stored = table.Column<DateTime>(nullable: false),
                    ActorId = table.Column<long>(nullable: false),
                    VerbId = table.Column<long>(nullable: false),
                    VerbUUID = table.Column<Guid>(nullable: false),
                    ObjectId = table.Column<long>(nullable: false),
                    ObjectUUID = table.Column<Guid>(nullable: false),
                    ResultId = table.Column<long>(nullable: true),
                    ContextId = table.Column<long>(nullable: true),
                    AuthorityId = table.Column<long>(nullable: true),
                    VersionUUID = table.Column<Guid>(nullable: false),
                    VersionId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalStatement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalStatement_Actor_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Actor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocalStatement_Authority_AuthorityId",
                        column: x => x.AuthorityId,
                        principalTable: "Authority",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocalStatement_Context_ContextId",
                        column: x => x.ContextId,
                        principalTable: "Context",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocalStatement_Result_ResultId",
                        column: x => x.ResultId,
                        principalTable: "Result",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Statement",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false),
                    Timestamp = table.Column<DateTime>(nullable: false),
                    Stored = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ActorId = table.Column<long>(nullable: false),
                    VerbKey = table.Column<long>(nullable: false),
                    ObjectKey = table.Column<long>(nullable: false),
                    ResultId = table.Column<long>(nullable: true),
                    ContextId = table.Column<long>(nullable: true),
                    AuthorityId = table.Column<long>(nullable: true),
                    VersionId = table.Column<long>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Statement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Statement_Actor_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Actor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Statement_Authority_AuthorityId",
                        column: x => x.AuthorityId,
                        principalTable: "Authority",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Statement_Context_ContextId",
                        column: x => x.ContextId,
                        principalTable: "Context",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Statement_Object_ObjectKey",
                        column: x => x.ObjectKey,
                        principalTable: "Object",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Statement_Result_ResultId",
                        column: x => x.ResultId,
                        principalTable: "Result",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Statement_Verb_VerbKey",
                        column: x => x.VerbKey,
                        principalTable: "Verb",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Statement_Version_VersionId",
                        column: x => x.VersionId,
                        principalTable: "Version",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UUID = table.Column<Guid>(nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    UsageType = table.Column<string>(nullable: true),
                    Display = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    ContentType = table.Column<string>(nullable: true),
                    Length = table.Column<int>(nullable: false),
                    Sha2 = table.Column<string>(nullable: true),
                    FileURL = table.Column<string>(nullable: true),
                    LocalStatementId = table.Column<long>(nullable: true),
                    StatementId = table.Column<long>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_LocalStatement_LocalStatementId",
                        column: x => x.LocalStatementId,
                        principalTable: "LocalStatement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Attachments_Statement_StatementId",
                        column: x => x.StatementId,
                        principalTable: "Statement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Actor_AccountId",
                table: "Actor",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Actor_ContextId",
                table: "Actor",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_Actor_MemberId",
                table: "Actor",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_LocalStatementId",
                table: "Attachments",
                column: "LocalStatementId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_StatementId",
                table: "Attachments",
                column: "StatementId");

            migrationBuilder.CreateIndex(
                name: "IX_Authority_ActorId",
                table: "Authority",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_Context_ContextActivitiesId",
                table: "Context",
                column: "ContextActivitiesId");

            migrationBuilder.CreateIndex(
                name: "IX_Context_ExtensionsId",
                table: "Context",
                column: "ExtensionsId");

            migrationBuilder.CreateIndex(
                name: "IX_Context_InstructorId",
                table: "Context",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_Context_StatementReferenceKey",
                table: "Context",
                column: "StatementReferenceKey");

            migrationBuilder.CreateIndex(
                name: "IX_Definition_ExtensionsId",
                table: "Definition",
                column: "ExtensionsId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalStatement_ActorId",
                table: "LocalStatement",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalStatement_AuthorityId",
                table: "LocalStatement",
                column: "AuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalStatement_ContextId",
                table: "LocalStatement",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalStatement_ResultId",
                table: "LocalStatement",
                column: "ResultId");

            migrationBuilder.CreateIndex(
                name: "IX_Object_DefinitionId",
                table: "Object",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Result_ExtensionsId",
                table: "Result",
                column: "ExtensionsId");

            migrationBuilder.CreateIndex(
                name: "IX_Result_ScoreId",
                table: "Result",
                column: "ScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Statement_ActorId",
                table: "Statement",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_Statement_AuthorityId",
                table: "Statement",
                column: "AuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_Statement_ContextId",
                table: "Statement",
                column: "ContextId");

            migrationBuilder.CreateIndex(
                name: "IX_Statement_ObjectKey",
                table: "Statement",
                column: "ObjectKey");

            migrationBuilder.CreateIndex(
                name: "IX_Statement_ResultId",
                table: "Statement",
                column: "ResultId");

            migrationBuilder.CreateIndex(
                name: "IX_Statement_VerbKey",
                table: "Statement",
                column: "VerbKey");

            migrationBuilder.CreateIndex(
                name: "IX_Statement_VersionId",
                table: "Statement",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_XApiResultExtras_ResultId",
                table: "XApiResultExtras",
                column: "ResultId");

            migrationBuilder.AddForeignKey(
                name: "FK_Actor_Context_ContextId",
                table: "Actor",
                column: "ContextId",
                principalTable: "Context",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Actor_Account_AccountId",
                table: "Actor");

            migrationBuilder.DropForeignKey(
                name: "FK_Actor_Context_ContextId",
                table: "Actor");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "XApiResultExtras");

            migrationBuilder.DropTable(
                name: "LocalStatement");

            migrationBuilder.DropTable(
                name: "Statement");

            migrationBuilder.DropTable(
                name: "Authority");

            migrationBuilder.DropTable(
                name: "Object");

            migrationBuilder.DropTable(
                name: "Result");

            migrationBuilder.DropTable(
                name: "Verb");

            migrationBuilder.DropTable(
                name: "Version");

            migrationBuilder.DropTable(
                name: "Definition");

            migrationBuilder.DropTable(
                name: "Score");

            migrationBuilder.DropTable(
                name: "Account");

            migrationBuilder.DropTable(
                name: "Context");

            migrationBuilder.DropTable(
                name: "ContextActivities");

            migrationBuilder.DropTable(
                name: "Extensions");

            migrationBuilder.DropTable(
                name: "Actor");

            migrationBuilder.DropTable(
                name: "StatementReference");

            migrationBuilder.DropTable(
                name: "Member");
        }
    }
}

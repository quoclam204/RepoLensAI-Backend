using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoLens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceLocation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repositories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "analyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentStage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CommitHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_analyses_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IssueType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_analysis_issues_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartLine = table.Column<int>(type: "integer", nullable: false),
                    EndLine = table.Column<int>(type: "integer", nullable: false),
                    EvidenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evidences_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projects_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DependencyType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dependencies_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dependencies_evidences_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "evidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "source_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AnalysisStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_source_files_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_source_files_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "code_symbols",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FullName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SymbolType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartLine = table.Column<int>(type: "integer", nullable: false),
                    EndLine = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_symbols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_code_symbols_source_files_SourceFileId",
                        column: x => x.SourceFileId,
                        principalTable: "source_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_chunks_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_chunks_evidences_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "evidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_document_chunks_source_files_SourceFileId",
                        column: x => x.SourceFileId,
                        principalTable: "source_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "api_endpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Route = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Controller = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SymbolId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_endpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_api_endpoints_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_api_endpoints_code_symbols_SymbolId",
                        column: x => x.SymbolId,
                        principalTable: "code_symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_api_endpoints_evidences_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "evidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_api_endpoints_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "database_entities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceSymbolId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_entities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_database_entities_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_database_entities_code_symbols_SourceSymbolId",
                        column: x => x.SourceSymbolId,
                        principalTable: "code_symbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "database_relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_database_relationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_database_relationships_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_database_relationships_database_entities_SourceEntityId",
                        column: x => x.SourceEntityId,
                        principalTable: "database_entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_relationships_database_entities_TargetEntityId",
                        column: x => x.TargetEntityId,
                        principalTable: "database_entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_database_relationships_evidences_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "evidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analyses_RepositoryId",
                table: "analyses",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_issues_AnalysisId",
                table: "analysis_issues",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_api_endpoints_AnalysisId",
                table: "api_endpoints",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_api_endpoints_EvidenceId",
                table: "api_endpoints",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_api_endpoints_ProjectId",
                table: "api_endpoints",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_api_endpoints_SymbolId",
                table: "api_endpoints",
                column: "SymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_code_symbols_FullName",
                table: "code_symbols",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_code_symbols_SourceFileId",
                table: "code_symbols",
                column: "SourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_database_entities_AnalysisId",
                table: "database_entities",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_database_entities_SourceSymbolId",
                table: "database_entities",
                column: "SourceSymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_database_relationships_AnalysisId",
                table: "database_relationships",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_database_relationships_EvidenceId",
                table: "database_relationships",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_database_relationships_SourceEntityId",
                table: "database_relationships",
                column: "SourceEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_database_relationships_TargetEntityId",
                table: "database_relationships",
                column: "TargetEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_dependencies_AnalysisId",
                table: "dependencies",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_dependencies_EvidenceId",
                table: "dependencies",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_dependencies_SourceId",
                table: "dependencies",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_dependencies_TargetId",
                table: "dependencies",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_AnalysisId",
                table: "document_chunks",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_EvidenceId",
                table: "document_chunks",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_SourceFileId",
                table: "document_chunks",
                column: "SourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_evidences_AnalysisId",
                table: "evidences",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_evidences_FilePath",
                table: "evidences",
                column: "FilePath");

            migrationBuilder.CreateIndex(
                name: "IX_projects_AnalysisId",
                table: "projects",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_source_files_AnalysisId",
                table: "source_files",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_source_files_AnalysisId_Path",
                table: "source_files",
                columns: new[] { "AnalysisId", "Path" });

            migrationBuilder.CreateIndex(
                name: "IX_source_files_ProjectId",
                table: "source_files",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_issues");

            migrationBuilder.DropTable(
                name: "api_endpoints");

            migrationBuilder.DropTable(
                name: "database_relationships");

            migrationBuilder.DropTable(
                name: "dependencies");

            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropTable(
                name: "database_entities");

            migrationBuilder.DropTable(
                name: "evidences");

            migrationBuilder.DropTable(
                name: "code_symbols");

            migrationBuilder.DropTable(
                name: "source_files");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "analyses");

            migrationBuilder.DropTable(
                name: "repositories");
        }
    }
}

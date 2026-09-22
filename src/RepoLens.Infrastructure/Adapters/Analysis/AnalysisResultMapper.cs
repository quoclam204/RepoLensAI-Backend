using RepoLens.Analysis.Graph;
using RepoLens.Analysis.Orchestration;
using RepoLens.Analysis.RAG;
using RepoLens.Application.DTOs.Persistence;
using RepoLens.Domain.Enums;

namespace RepoLens.Infrastructure.Adapters.Analysis;

/// <summary>
/// Deterministically maps low-level Static Analysis knowledge graph and scanner results
/// into the persistence data transfer model (AnalysisResultModel) without loss of evidence or relationships.
/// </summary>
public static class AnalysisResultMapper
{
    public static AnalysisResultModel Map(
        RepositoryAnalysisResult analysisResult,
        Guid analysisId,
        IReadOnlyDictionary<string, string>? fileContents = null)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);

        var scan = analysisResult.ScannedMetadata;
        var graph = analysisResult.Analysis;

        // 1. Map Projects
        var projectModels = new List<ProjectPersistenceModel>(scan.Projects.Count);
        var projectPathToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var proj in scan.Projects)
        {
            var projId = Guid.NewGuid();
            projectPathToId[proj.RelativePath] = projId;

            projectModels.Add(new ProjectPersistenceModel(
                Id: projId,
                Name: proj.ProjectName,
                Path: proj.RelativePath,
                Language: proj.ProjectType.Equals("CSharp", StringComparison.OrdinalIgnoreCase) ? "csharp" : "typescript",
                ProjectType: proj.ProjectType));
        }

        // 2. Map SourceFiles
        var fileModels = new List<SourceFilePersistenceModel>(scan.SourceFiles.Count);
        var filePathToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in scan.SourceFiles)
        {
            var fileId = Guid.NewGuid();
            filePathToId[file.RelativePath] = fileId;

            // Match enclosing project
            var matchedProj = scan.Projects
                .Where(p => file.RelativePath.StartsWith(Path.GetDirectoryName(p.RelativePath) ?? "", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.RelativePath.Length)
                .FirstOrDefault();

            fileModels.Add(new SourceFilePersistenceModel(
                Id: fileId,
                ProjectId: matchedProj != null && projectPathToId.TryGetValue(matchedProj.RelativePath, out var pid) ? pid : null,
                ProjectPath: matchedProj?.RelativePath,
                Path: file.RelativePath,
                Language: file.Extension.TrimStart('.').ToLowerInvariant(),
                Size: file.SizeInBytes,
                Hash: string.Empty,
                AnalysisStatus: FileAnalysisStatus.Analyzed));
        }

        // 3. Map Evidences
        var evidenceModels = new List<EvidencePersistenceModel>();
        var evidenceKeyToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var rel in graph.Relationships)
        {
            if (rel.Evidence == null) continue;

            var evi = rel.Evidence;
            var key = $"ev:{evi.FilePath}:{evi.StartLine}-{evi.EndLine}";
            if (!evidenceKeyToId.ContainsKey(key))
            {
                var eviId = evi.Id != Guid.Empty ? evi.Id : Guid.NewGuid();
                evidenceKeyToId[key] = eviId;

                evidenceModels.Add(new EvidencePersistenceModel(
                    Id: eviId,
                    EvidenceKey: key,
                    FilePath: evi.FilePath,
                    Symbol: evi.Symbol,
                    StartLine: evi.StartLine,
                    EndLine: evi.EndLine,
                    EvidenceType: evi.EvidenceType,
                    Description: evi.Snippet));
            }
        }

        // 4. Map CodeSymbols
        var symbolModels = new List<CodeSymbolPersistenceModel>();
        var symbolKeyToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Nodes)
        {
            if (IsCodeSymbol(node.Type, out var symType))
            {
                var symId = Guid.NewGuid();
                symbolKeyToId[node.Id] = symId;

                var startLine = node.Location?.StartLine ?? 1;
                var endLine = node.Location?.EndLine ?? 1;

                symbolModels.Add(new CodeSymbolPersistenceModel(
                    Id: symId,
                    SymbolKey: node.Id,
                    FilePath: node.FilePath,
                    SourceFileId: !string.IsNullOrWhiteSpace(node.FilePath) && filePathToId.TryGetValue(node.FilePath, out var fid) ? fid : null,
                    Name: node.Name,
                    FullName: node.Id,
                    SymbolType: symType,
                    StartLine: startLine,
                    EndLine: endLine));
            }
        }

        // 5. Map Dependencies
        var depModels = new List<DependencyPersistenceModel>(graph.Relationships.Count);
        foreach (var rel in graph.Relationships)
        {
            var depType = MapRelationshipType(rel.Type);
            string? evidenceKey = null;
            Guid? evidenceId = null;

            if (rel.Evidence != null)
            {
                evidenceKey = $"ev:{rel.Evidence.FilePath}:{rel.Evidence.StartLine}-{rel.Evidence.EndLine}";
                if (evidenceKeyToId.TryGetValue(evidenceKey, out var eid))
                {
                    evidenceId = eid;
                }
            }

            depModels.Add(new DependencyPersistenceModel(
                Id: Guid.NewGuid(),
                SourceId: rel.SourceId,
                TargetId: rel.TargetId,
                DependencyType: depType,
                EvidenceKey: evidenceKey,
                EvidenceId: evidenceId));
        }

        // 6. Map ApiEndpoints
        var endpointModels = new List<ApiEndpointPersistenceModel>();
        foreach (var node in graph.Nodes.Where(n => n.Type == KnowledgeNodeType.Endpoint))
        {
            var httpMethod = node.Properties.GetValueOrDefault("HttpMethod", "GET");
            var route = node.Properties.GetValueOrDefault("Route", node.Name);
            var controller = node.Properties.GetValueOrDefault("Controller");
            var action = node.Properties.GetValueOrDefault("Action");

            var rel = graph.Relationships.FirstOrDefault(r => r.SourceId == node.Id || r.TargetId == node.Id);
            string? eviKey = null;
            Guid? eviId = null;
            if (rel?.Evidence != null)
            {
                eviKey = $"ev:{rel.Evidence.FilePath}:{rel.Evidence.StartLine}-{rel.Evidence.EndLine}";
                evidenceKeyToId.TryGetValue(eviKey, out var eid);
                eviId = eid;
            }

            endpointModels.Add(new ApiEndpointPersistenceModel(
                Id: Guid.NewGuid(),
                ProjectId: null,
                ProjectPath: null,
                Method: httpMethod,
                Route: route,
                Controller: controller,
                Action: action,
                SymbolId: null,
                SymbolKey: node.Id,
                SymbolFullName: node.Id,
                EvidenceId: eviId,
                EvidenceKey: eviKey));
        }

        // 7. Map DatabaseEntities
        var dbEntityModels = new List<DatabaseEntityPersistenceModel>();
        var dbEntityKeyToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Nodes.Where(n => n.Type == KnowledgeNodeType.DatabaseEntity))
        {
            var entityId = Guid.NewGuid();
            dbEntityKeyToId[node.Id] = entityId;

            dbEntityModels.Add(new DatabaseEntityPersistenceModel(
                Id: entityId,
                Name: node.Name,
                EntityType: "Table",
                SourceSymbolKey: node.Id,
                SourceSymbolFullName: node.Id,
                SourceSymbolId: null));
        }

        // 8. Map DatabaseRelationships
        var dbRelModels = new List<DatabaseRelationshipPersistenceModel>();
        foreach (var rel in graph.Relationships)
        {
            if (dbEntityKeyToId.TryGetValue(rel.SourceId, out var srcEntityId) &&
                dbEntityKeyToId.TryGetValue(rel.TargetId, out var tgtEntityId))
            {
                var relType = rel.Properties.GetValueOrDefault("DatabaseRelationType") switch
                {
                    "OneToOne" => DatabaseRelationshipType.OneToOne,
                    "ManyToMany" => DatabaseRelationshipType.ManyToMany,
                    _ => DatabaseRelationshipType.OneToMany
                };

                string? eviKey = null;
                Guid? eviId = null;
                if (rel.Evidence != null)
                {
                    eviKey = $"ev:{rel.Evidence.FilePath}:{rel.Evidence.StartLine}-{rel.Evidence.EndLine}";
                    evidenceKeyToId.TryGetValue(eviKey, out var eid);
                    eviId = eid;
                }

                dbRelModels.Add(new DatabaseRelationshipPersistenceModel(
                    Id: Guid.NewGuid(),
                    SourceEntityName: rel.SourceId,
                    SourceEntityId: srcEntityId,
                    TargetEntityName: rel.TargetId,
                    TargetEntityId: tgtEntityId,
                    RelationshipType: relType,
                    EvidenceKey: eviKey,
                    EvidenceId: eviId));
            }
        }

        // 9. Map Issues
        var issueModels = analysisResult.AllErrors.Select(err => new AnalysisIssuePersistenceModel(
            Id: Guid.NewGuid(),
            FilePath: null,
            IssueType: IssueType.ParserFailure,
            Severity: IssueSeverity.Warning,
            Message: err)).ToList();

        // 10. Generate DocumentChunks for RAG
        var contents = fileContents ?? new Dictionary<string, string>();
        var generatedChunks = DocumentChunkGenerator.GenerateChunks(graph.Nodes, contents);
        var chunkModels = generatedChunks.Select(c => new DocumentChunkPersistenceModel(
            Id: Guid.NewGuid(),
            SourceFileId: filePathToId.TryGetValue(c.FilePath, out var fid) ? fid : null,
            FilePath: c.FilePath,
            Content: c.Content,
            TokenCount: c.TokenCount,
            ChunkIndex: c.ChunkIndex,
            EvidenceId: c.EvidenceKey != null && evidenceKeyToId.TryGetValue(c.EvidenceKey, out var eid) ? eid : null,
            EvidenceKey: c.EvidenceKey)).ToList();

        return new AnalysisResultModel
        {
            AnalysisId = analysisId,
            CurrentStage = AnalysisStage.Completed.ToString(),
            NewStatus = AnalysisStatus.Completed,
            CommitHash = null,
            Projects = projectModels.AsReadOnly(),
            SourceFiles = fileModels.AsReadOnly(),
            CodeSymbols = symbolModels.AsReadOnly(),
            Evidences = evidenceModels.AsReadOnly(),
            Dependencies = depModels.AsReadOnly(),
            ApiEndpoints = endpointModels.AsReadOnly(),
            DatabaseEntities = dbEntityModels.AsReadOnly(),
            DatabaseRelationships = dbRelModels.AsReadOnly(),
            Issues = issueModels.AsReadOnly(),
            DocumentChunks = chunkModels.AsReadOnly()
        };
    }

    private static bool IsCodeSymbol(KnowledgeNodeType nodeType, out SymbolType symbolType)
    {
        switch (nodeType)
        {
            case KnowledgeNodeType.Class:
                symbolType = SymbolType.Class;
                return true;
            case KnowledgeNodeType.Record:
                symbolType = SymbolType.Class;
                return true;
            case KnowledgeNodeType.Interface:
                symbolType = SymbolType.Interface;
                return true;
            case KnowledgeNodeType.Struct:
                symbolType = SymbolType.Struct;
                return true;
            case KnowledgeNodeType.Enum:
                symbolType = SymbolType.Enum;
                return true;
            case KnowledgeNodeType.Method:
                symbolType = SymbolType.Method;
                return true;
            case KnowledgeNodeType.Property:
                symbolType = SymbolType.Property;
                return true;
            case KnowledgeNodeType.Namespace:
                symbolType = SymbolType.Namespace;
                return true;
            default:
                symbolType = SymbolType.Class;
                return false;
        }
    }

    private static DependencyType MapRelationshipType(KnowledgeRelationshipType type)
    {
        return type switch
        {
            KnowledgeRelationshipType.Contains => DependencyType.Contains,
            KnowledgeRelationshipType.Defines => DependencyType.Defines,
            KnowledgeRelationshipType.Inherits => DependencyType.Inherits,
            KnowledgeRelationshipType.Implements => DependencyType.Implements,
            KnowledgeRelationshipType.Calls => DependencyType.Calls,
            KnowledgeRelationshipType.DependsOn => DependencyType.DependsOn,
            KnowledgeRelationshipType.Exposes => DependencyType.Exposes,
            KnowledgeRelationshipType.MapsTo => DependencyType.MapsTo,
            KnowledgeRelationshipType.Reads => DependencyType.Reads,
            KnowledgeRelationshipType.Writes => DependencyType.Writes,
            _ => DependencyType.DependsOn
        };
    }
}

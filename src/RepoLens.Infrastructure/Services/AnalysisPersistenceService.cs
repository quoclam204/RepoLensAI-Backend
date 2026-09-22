using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoLens.Application.Abstractions;
using RepoLens.Application.DTOs.Persistence;
using RepoLens.Domain.Entities;
using RepoLens.Domain.Enums;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

/// <summary>
/// Service responsible for atomically persisting the knowledge model and static analysis results into PostgreSQL storage (T059).
/// </summary>
public class AnalysisPersistenceService : IAnalysisPersistenceService
{
    private readonly RepoLensDbContext _context;
    private readonly ILogger<AnalysisPersistenceService> _logger;

    public AnalysisPersistenceService(RepoLensDbContext context, ILogger<AnalysisPersistenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PersistAnalysisResultAsync(AnalysisResultModel result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        _logger.LogInformation("Beginning atomic persistence of knowledge model for Analysis {AnalysisId}...", result.AnalysisId);

        // TODO: [Idempotency & Re-analysis] Trong trường hợp một AnalysisId được chạy phân tích lại hoặc bổ sung,
        // cần có cơ chế dọn dẹp (cleanup/cascade purge) các node/edge liên quan cũ trước khi ghi dữ liệu mới để tránh trùng lặp.

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            var analysis = await _context.Analyses
                .FirstOrDefaultAsync(a => a.Id == result.AnalysisId, ct)
                ?? throw new KeyNotFoundException($"Analysis with ID '{result.AnalysisId}' does not exist.");

            // 1. Projects
            var projects = new List<Project>(result.Projects.Count);
            var projectPathMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var projectIdSet = new HashSet<Guid>();

            foreach (var projDto in result.Projects)
            {
                var projId = projDto.Id ?? Guid.NewGuid();
                projectPathMap.TryAdd(projDto.Path, projId);
                projectIdSet.Add(projId);

                projects.Add(new Project
                {
                    Id = projId,
                    AnalysisId = result.AnalysisId,
                    Name = projDto.Name,
                    Path = projDto.Path,
                    Language = projDto.Language,
                    ProjectType = projDto.ProjectType
                });
            }

            if (projects.Count > 0)
            {
                await _context.Projects.AddRangeAsync(projects, ct);
            }

            // 2. SourceFiles
            var sourceFiles = new List<SourceFile>(result.SourceFiles.Count);
            var filePathMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var fileIdSet = new HashSet<Guid>();

            foreach (var fileDto in result.SourceFiles)
            {
                var fileId = fileDto.Id ?? Guid.NewGuid();
                filePathMap.TryAdd(fileDto.Path, fileId);
                fileIdSet.Add(fileId);

                // Resolve ProjectId
                Guid resolvedProjectId = Guid.Empty;
                if (fileDto.ProjectId.HasValue && projectIdSet.Contains(fileDto.ProjectId.Value))
                {
                    resolvedProjectId = fileDto.ProjectId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(fileDto.ProjectPath) && projectPathMap.TryGetValue(fileDto.ProjectPath, out var matchedProjId))
                {
                    resolvedProjectId = matchedProjId;
                }
                else if (projects.Count == 1)
                {
                    resolvedProjectId = projects[0].Id;
                }
                else if (projects.Count > 1)
                {
                    // Match the longest project path prefix
                    var matchedPrefixProj = projectPathMap
                        .Where(kvp => !string.IsNullOrEmpty(kvp.Key) && fileDto.Path.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(kvp => kvp.Key.Length)
                        .FirstOrDefault();

                    resolvedProjectId = matchedPrefixProj.Value != Guid.Empty ? matchedPrefixProj.Value : projects[0].Id;
                }

                sourceFiles.Add(new SourceFile
                {
                    Id = fileId,
                    AnalysisId = result.AnalysisId,
                    ProjectId = resolvedProjectId,
                    Path = fileDto.Path,
                    Language = fileDto.Language,
                    Size = fileDto.Size,
                    Hash = fileDto.Hash,
                    AnalysisStatus = fileDto.AnalysisStatus
                });
            }

            if (sourceFiles.Count > 0)
            {
                await _context.SourceFiles.AddRangeAsync(sourceFiles, ct);
            }

            // 3. CodeSymbols
            // Lưu ý: FullName có thể bị trùng do method overloads trong cùng class hoặc namespace.
            // Sử dụng TryAdd để tránh ArgumentException, ưu tiên SymbolKey duy nhất nếu được cung cấp.
            var symbols = new List<CodeSymbol>(result.CodeSymbols.Count);
            var symbolKeyMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var symbolFullNameMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var symbolIdSet = new HashSet<Guid>();

            foreach (var symDto in result.CodeSymbols)
            {
                var symId = symDto.Id ?? Guid.NewGuid();
                symbolIdSet.Add(symId);

                if (!string.IsNullOrWhiteSpace(symDto.SymbolKey))
                {
                    symbolKeyMap.TryAdd(symDto.SymbolKey, symId);
                }

                if (!string.IsNullOrWhiteSpace(symDto.FullName))
                {
                    symbolFullNameMap.TryAdd(symDto.FullName, symId);
                }

                // Resolve SourceFileId
                Guid resolvedFileId = Guid.Empty;
                if (symDto.SourceFileId.HasValue && fileIdSet.Contains(symDto.SourceFileId.Value))
                {
                    resolvedFileId = symDto.SourceFileId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(symDto.FilePath) && filePathMap.TryGetValue(symDto.FilePath, out var matchedFileId))
                {
                    resolvedFileId = matchedFileId;
                }
                else if (sourceFiles.Count > 0)
                {
                    resolvedFileId = sourceFiles[0].Id;
                }

                symbols.Add(new CodeSymbol
                {
                    Id = symId,
                    SourceFileId = resolvedFileId,
                    Name = symDto.Name,
                    FullName = symDto.FullName,
                    SymbolType = symDto.SymbolType,
                    StartLine = symDto.StartLine,
                    EndLine = symDto.EndLine
                });
            }

            // TODO: [Tối ưu hiệu năng] Đối với các repository quy mô lớn (> 100,000 code symbols),
            // xem xét áp dụng EFCore.BulkExtensions hoặc NpgsqlBinaryImporter (PostgreSQL COPY binary protocol).
            if (symbols.Count > 0)
            {
                await _context.CodeSymbols.AddRangeAsync(symbols, ct);
            }

            // 4. Evidences
            var evidences = new List<Evidence>(result.Evidences.Count);
            var evidenceKeyMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var evidenceIdSet = new HashSet<Guid>();

            foreach (var eviDto in result.Evidences)
            {
                var eviId = eviDto.Id ?? Guid.NewGuid();
                evidenceIdSet.Add(eviId);

                if (!string.IsNullOrWhiteSpace(eviDto.EvidenceKey))
                {
                    evidenceKeyMap.TryAdd(eviDto.EvidenceKey, eviId);
                }

                evidences.Add(new Evidence
                {
                    Id = eviId,
                    AnalysisId = result.AnalysisId,
                    FilePath = eviDto.FilePath,
                    Symbol = eviDto.Symbol,
                    StartLine = eviDto.StartLine,
                    EndLine = eviDto.EndLine,
                    EvidenceType = eviDto.EvidenceType,
                    Description = eviDto.Description
                });
            }

            if (evidences.Count > 0)
            {
                await _context.Evidences.AddRangeAsync(evidences, ct);
            }

            // 5. DatabaseEntities
            var dbEntities = new List<DatabaseEntity>(result.DatabaseEntities.Count);
            var dbEntityNameMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var dbEntityIdSet = new HashSet<Guid>();

            foreach (var dbEntityDto in result.DatabaseEntities)
            {
                var entityId = dbEntityDto.Id ?? Guid.NewGuid();
                dbEntityIdSet.Add(entityId);
                dbEntityNameMap.TryAdd(dbEntityDto.Name, entityId);

                // Resolve SourceSymbolId (nếu có)
                Guid? sourceSymbolId = null;
                if (dbEntityDto.SourceSymbolId.HasValue && symbolIdSet.Contains(dbEntityDto.SourceSymbolId.Value))
                {
                    sourceSymbolId = dbEntityDto.SourceSymbolId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(dbEntityDto.SourceSymbolKey) && symbolKeyMap.TryGetValue(dbEntityDto.SourceSymbolKey, out var skId))
                {
                    sourceSymbolId = skId;
                }
                else if (!string.IsNullOrWhiteSpace(dbEntityDto.SourceSymbolFullName) && symbolFullNameMap.TryGetValue(dbEntityDto.SourceSymbolFullName, out var sfnId))
                {
                    sourceSymbolId = sfnId;
                }

                dbEntities.Add(new DatabaseEntity
                {
                    Id = entityId,
                    AnalysisId = result.AnalysisId,
                    Name = dbEntityDto.Name,
                    EntityType = dbEntityDto.EntityType,
                    SourceSymbolId = sourceSymbolId
                });
            }

            if (dbEntities.Count > 0)
            {
                await _context.DatabaseEntities.AddRangeAsync(dbEntities, ct);
            }

            // 6. DatabaseRelationships
            var dbRelationships = new List<DatabaseRelationship>(result.DatabaseRelationships.Count);

            foreach (var relDto in result.DatabaseRelationships)
            {
                Guid sourceEntityId = Guid.Empty;
                if (relDto.SourceEntityId.HasValue && dbEntityIdSet.Contains(relDto.SourceEntityId.Value))
                {
                    sourceEntityId = relDto.SourceEntityId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(relDto.SourceEntityName) && dbEntityNameMap.TryGetValue(relDto.SourceEntityName, out var sId))
                {
                    sourceEntityId = sId;
                }

                Guid targetEntityId = Guid.Empty;
                if (relDto.TargetEntityId.HasValue && dbEntityIdSet.Contains(relDto.TargetEntityId.Value))
                {
                    targetEntityId = relDto.TargetEntityId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(relDto.TargetEntityName) && dbEntityNameMap.TryGetValue(relDto.TargetEntityName, out var tId))
                {
                    targetEntityId = tId;
                }

                // Cần cả Source và Target Entity hợp lệ vì DeleteBehavior là Restrict
                if (sourceEntityId == Guid.Empty || targetEntityId == Guid.Empty)
                {
                    _logger.LogWarning("Skipping DatabaseRelationship between '{Source}' and '{Target}' because one of the entities was not found.",
                        relDto.SourceEntityName ?? relDto.SourceEntityId?.ToString(),
                        relDto.TargetEntityName ?? relDto.TargetEntityId?.ToString());
                    continue;
                }

                Guid? evidenceId = null;
                if (relDto.EvidenceId.HasValue && evidenceIdSet.Contains(relDto.EvidenceId.Value))
                {
                    evidenceId = relDto.EvidenceId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(relDto.EvidenceKey) && evidenceKeyMap.TryGetValue(relDto.EvidenceKey, out var eId))
                {
                    evidenceId = eId;
                }

                dbRelationships.Add(new DatabaseRelationship
                {
                    Id = relDto.Id ?? Guid.NewGuid(),
                    AnalysisId = result.AnalysisId,
                    SourceEntityId = sourceEntityId,
                    TargetEntityId = targetEntityId,
                    RelationshipType = relDto.RelationshipType,
                    EvidenceId = evidenceId
                });
            }

            if (dbRelationships.Count > 0)
            {
                await _context.DatabaseRelationships.AddRangeAsync(dbRelationships, ct);
            }

            // 7. ApiEndpoints
            var endpoints = new List<ApiEndpoint>(result.ApiEndpoints.Count);

            foreach (var epDto in result.ApiEndpoints)
            {
                Guid epProjId = Guid.Empty;
                if (epDto.ProjectId.HasValue && projectIdSet.Contains(epDto.ProjectId.Value))
                {
                    epProjId = epDto.ProjectId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(epDto.ProjectPath) && projectPathMap.TryGetValue(epDto.ProjectPath, out var pId))
                {
                    epProjId = pId;
                }
                else if (projects.Count > 0)
                {
                    epProjId = projects[0].Id;
                }

                // TODO: [Partial Failures & Resilience] Nếu một số entity mapping khóa ngoại không tìm thấy,
                // mặc định gán null (OnDelete SetNull) thay vì làm fail toàn bộ analysis.
                Guid? epSymId = null;
                if (epDto.SymbolId.HasValue && symbolIdSet.Contains(epDto.SymbolId.Value))
                {
                    epSymId = epDto.SymbolId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(epDto.SymbolKey) && symbolKeyMap.TryGetValue(epDto.SymbolKey, out var skId))
                {
                    epSymId = skId;
                }
                else if (!string.IsNullOrWhiteSpace(epDto.SymbolFullName) && symbolFullNameMap.TryGetValue(epDto.SymbolFullName, out var sfnId))
                {
                    epSymId = sfnId;
                }

                Guid? epEvidenceId = null;
                if (epDto.EvidenceId.HasValue && evidenceIdSet.Contains(epDto.EvidenceId.Value))
                {
                    epEvidenceId = epDto.EvidenceId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(epDto.EvidenceKey) && evidenceKeyMap.TryGetValue(epDto.EvidenceKey, out var ekId))
                {
                    epEvidenceId = ekId;
                }

                endpoints.Add(new ApiEndpoint
                {
                    Id = epDto.Id ?? Guid.NewGuid(),
                    AnalysisId = result.AnalysisId,
                    ProjectId = epProjId,
                    Method = epDto.Method,
                    Route = epDto.Route,
                    Controller = epDto.Controller,
                    Action = epDto.Action,
                    SymbolId = epSymId,
                    EvidenceId = epEvidenceId
                });
            }

            if (endpoints.Count > 0)
            {
                await _context.ApiEndpoints.AddRangeAsync(endpoints, ct);
            }

            // 8. Dependencies
            var dependencies = new List<Dependency>(result.Dependencies.Count);

            foreach (var depDto in result.Dependencies)
            {
                Guid? depEvidenceId = null;
                if (depDto.EvidenceId.HasValue && evidenceIdSet.Contains(depDto.EvidenceId.Value))
                {
                    depEvidenceId = depDto.EvidenceId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(depDto.EvidenceKey) && evidenceKeyMap.TryGetValue(depDto.EvidenceKey, out var dekId))
                {
                    depEvidenceId = dekId;
                }

                dependencies.Add(new Dependency
                {
                    Id = depDto.Id ?? Guid.NewGuid(),
                    AnalysisId = result.AnalysisId,
                    SourceId = depDto.SourceId,
                    TargetId = depDto.TargetId,
                    DependencyType = depDto.DependencyType,
                    EvidenceId = depEvidenceId
                });
            }

            if (dependencies.Count > 0)
            {
                await _context.Dependencies.AddRangeAsync(dependencies, ct);
            }

            // 9. AnalysisIssues
            var issues = new List<AnalysisIssue>(result.Issues.Count);

            foreach (var issueDto in result.Issues)
            {
                issues.Add(new AnalysisIssue
                {
                    Id = issueDto.Id ?? Guid.NewGuid(),
                    AnalysisId = result.AnalysisId,
                    FilePath = issueDto.FilePath,
                    IssueType = issueDto.IssueType,
                    Severity = issueDto.Severity,
                    Message = issueDto.Message
                });
            }

            if (issues.Count > 0)
            {
                await _context.AnalysisIssues.AddRangeAsync(issues, ct);
            }

            // 10. DocumentChunks
            var chunks = new List<DocumentChunk>(result.DocumentChunks.Count);

            foreach (var chunkDto in result.DocumentChunks)
            {
                Guid? chunkFileId = null;
                if (chunkDto.SourceFileId.HasValue && fileIdSet.Contains(chunkDto.SourceFileId.Value))
                {
                    chunkFileId = chunkDto.SourceFileId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(chunkDto.FilePath) && filePathMap.TryGetValue(chunkDto.FilePath, out var cfId))
                {
                    chunkFileId = cfId;
                }

                Guid? chunkEviId = null;
                if (chunkDto.EvidenceId.HasValue && evidenceIdSet.Contains(chunkDto.EvidenceId.Value))
                {
                    chunkEviId = chunkDto.EvidenceId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(chunkDto.EvidenceKey) && evidenceKeyMap.TryGetValue(chunkDto.EvidenceKey, out var cekId))
                {
                    chunkEviId = cekId;
                }

                chunks.Add(new DocumentChunk
                {
                    Id = chunkDto.Id ?? Guid.NewGuid(),
                    AnalysisId = result.AnalysisId,
                    SourceFileId = chunkFileId,
                    Content = chunkDto.Content,
                    TokenCount = chunkDto.TokenCount,
                    ChunkIndex = chunkDto.ChunkIndex,
                    EvidenceId = chunkEviId
                });
            }

            if (chunks.Count > 0)
            {
                await _context.DocumentChunks.AddRangeAsync(chunks, ct);
            }

            // 11. Cập nhật thông tin Analysis (Stage, Status, Hash, CompletedAt)
            if (result.NewStatus.HasValue)
            {
                analysis.Status = result.NewStatus.Value;
                if (result.NewStatus == AnalysisStatus.Completed)
                {
                    analysis.CompletedAt = DateTimeOffset.UtcNow;
                }
            }

            if (!string.IsNullOrWhiteSpace(result.CurrentStage))
            {
                analysis.CurrentStage = result.CurrentStage;
            }

            if (!string.IsNullOrWhiteSpace(result.CommitHash))
            {
                analysis.CommitHash = result.CommitHash;
            }

            // 12. SaveChanges & Commit Transaction
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Successfully persisted knowledge model for Analysis {AnalysisId}: {ProjectsCount} projects, {FilesCount} files, {SymbolsCount} symbols, {EvidencesCount} evidences, {EndpointsCount} endpoints.",
                result.AnalysisId, projects.Count, sourceFiles.Count, symbols.Count, evidences.Count, endpoints.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction failed while persisting knowledge model for Analysis {AnalysisId}. Transaction rolled back.", result.AnalysisId);
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}

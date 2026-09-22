using Microsoft.EntityFrameworkCore;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Common;
using RepoLens.Application.DTOs.Endpoints;
using RepoLens.Infrastructure.Persistence;

namespace RepoLens.Infrastructure.Services;

public class ApiEndpointService : IApiEndpointService
{
    private readonly RepoLensDbContext _context;

    public ApiEndpointService(RepoLensDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<EndpointItemDto>> GetEndpointsAsync(Guid analysisId, EndpointFilterParams filter, CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = _context.ApiEndpoints
            .AsNoTracking()
            .Include(e => e.Project)
            .Where(e => e.AnalysisId == analysisId);

        if (!string.IsNullOrWhiteSpace(filter.Method))
        {
            query = query.Where(e => e.Method.ToLower() == filter.Method.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(filter.Route))
        {
            query = query.Where(e => e.Route.ToLower().Contains(filter.Route.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(filter.ProjectId) && Guid.TryParse(filter.ProjectId, out var projId))
        {
            query = query.Where(e => e.ProjectId == projId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Controller))
        {
            query = query.Where(e => e.Controller != null && e.Controller.ToLower().Contains(filter.Controller.ToLower()));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(e => e.Route)
            .ThenBy(e => e.Method)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var resultItems = items.Select(e => new EndpointItemDto(
            Id: e.Id.ToString(),
            Method: e.Method,
            Route: e.Route,
            Project: new ProjectRefDto(e.ProjectId.ToString(), e.Project?.Name ?? string.Empty),
            Controller: e.Controller,
            Action: e.Action,
            SymbolId: e.SymbolId?.ToString(),
            EvidenceId: e.EvidenceId?.ToString()
        )).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<EndpointItemDto>(resultItems, totalCount, page, pageSize, totalPages);
    }

    public async Task<EndpointDetailResponse?> GetEndpointDetailAsync(Guid analysisId, Guid endpointId, CancellationToken ct = default)
    {
        var endpoint = await _context.ApiEndpoints
            .AsNoTracking()
            .Include(e => e.Project)
            .Include(e => e.Symbol)
                .ThenInclude(s => s!.SourceFile)
            .Include(e => e.Evidence)
            .FirstOrDefaultAsync(e => e.AnalysisId == analysisId && e.Id == endpointId, ct);

        if (endpoint == null)
        {
            return null;
        }

        var sourceFilePath = endpoint.Symbol?.SourceFile?.Path ?? endpoint.Evidence?.FilePath ?? string.Empty;
        var symbolName = endpoint.Symbol?.FullName ?? (endpoint.Controller != null && endpoint.Action != null ? $"{endpoint.Controller}.{endpoint.Action}" : null);

        var evidenceList = new List<EndpointEvidenceSnippetDto>();
        if (endpoint.Evidence != null)
        {
            evidenceList.Add(new EndpointEvidenceSnippetDto(
                endpoint.Evidence.FilePath,
                endpoint.Evidence.StartLine,
                endpoint.Evidence.EndLine,
                !string.IsNullOrWhiteSpace(endpoint.Evidence.Description) ? endpoint.Evidence.Description : "Defines the endpoint."
            ));
        }

        return new EndpointDetailResponse(
            Id: endpoint.Id.ToString(),
            Method: endpoint.Method,
            Route: endpoint.Route,
            Controller: endpoint.Controller,
            Action: endpoint.Action,
            Project: endpoint.Project?.Name ?? string.Empty,
            Source: new SourceRefDto(sourceFilePath, symbolName),
            Evidence: evidenceList
        );
    }
}

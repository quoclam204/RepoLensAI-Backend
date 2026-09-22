using RepoLens.Application.DTOs.Analyses;
using RepoLens.Application.DTOs.Overview;

namespace RepoLens.Application.Abstractions;

public interface IAnalysisService
{
    Task<CreateAnalysisResponse> CreateAnalysisFromGitAsync(CreateAnalysisGitRequest request, CancellationToken ct = default);
    Task<CreateAnalysisResponse> CreateAnalysisFromZipAsync(string fileName, Stream contentStream, CancellationToken ct = default);
    Task<AnalysisStatusResponse?> GetAnalysisStatusAsync(Guid analysisId, CancellationToken ct = default);
    Task<AnalysisOverviewResponse?> GetAnalysisOverviewAsync(Guid analysisId, CancellationToken ct = default);
}

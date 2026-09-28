using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RepoLens.Application.Abstractions.AI;

namespace RepoLens.Infrastructure.Ai;

/// <summary>
/// Deterministic embedding provider for local development, CI testing, and evaluation (T082).
/// Generates reproducible, unit-normalized 1536-dimensional vectors from input text without network calls.
/// </summary>
public sealed class DeterministicEmbeddingProvider : IEmbeddingProvider
{
    private readonly int _dimensions;

    public DeterministicEmbeddingProvider(IOptions<EmbeddingOptions>? options = null)
    {
        _dimensions = options?.Value.Dimensions is > 0 ? options.Value.Dimensions : 1536;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var results = new List<float[]>(inputs.Count);

        foreach (var text in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(text))
            {
                results.Add(new float[_dimensions]);
                continue;
            }

            var vector = new float[_dimensions];
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

            // Populate vector deterministically using rotating seed bytes
            for (var i = 0; i < _dimensions; i++)
            {
                var seedByte = hash[i % hash.Length];
                vector[i] = ((seedByte / 255.0f) * 2.0f) - 1.0f;
            }

            // Normalize vector to unit length (L2 norm) for cosine similarity
            var norm = MathF.Sqrt(vector.Sum(x => x * x));
            if (norm > 0)
            {
                for (var i = 0; i < _dimensions; i++)
                {
                    vector[i] /= norm;
                }
            }

            results.Add(vector);
        }

        return Task.FromResult<IReadOnlyList<float[]>>(results);
    }
}

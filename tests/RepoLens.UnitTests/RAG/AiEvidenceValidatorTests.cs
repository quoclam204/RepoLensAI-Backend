using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;
using RepoLens.Application.Services;

namespace RepoLens.UnitTests.RAG;

public class AiEvidenceValidatorTests
{
    private static VectorChunkSearchResult MakeChunk(
        string filePath = "src/Services/OrderService.cs",
        string? symbol = "OrderService.ProcessOrder",
        string content = "public class OrderService { public void ProcessOrder(Order o) { _repo.Save(o); } }",
        int startLine = 10,
        int endLine = 25,
        float confidence = 0.95f)
    {
        return new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: Guid.NewGuid(),
            SourceFileId: Guid.NewGuid(),
            FilePath: filePath,
            Symbol: symbol,
            StartLine: startLine,
            EndLine: endLine,
            Content: content,
            TokenCount: 20,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: confidence,
            CosineDistance: 0.1,
            SimilarityScore: 0.9);
    }

    [Fact]
    public async Task ValidateAnswerAsync_WhenAnswerIsFullyGrounded_ReturnsValidAndFullySupported()
    {
        // 1: A grounded answer passes validation
        var chunk = MakeChunk("src/Services/OrderService.cs", "ProcessOrder", "public void ProcessOrder(Order o) { }");
        var validator = new AiEvidenceValidator();

        var answer = "The repository provides `OrderService.cs` where `ProcessOrder` handles incoming orders.";
        var result = await validator.ValidateAnswerAsync(answer, [chunk]);

        Assert.True(result.IsValid);
        Assert.Equal(AnswerValidationStatus.FullySupported, result.Status);
        Assert.Equal(answer, result.ValidatedAnswer);
        Assert.Empty(result.UnsupportedClaims);
        Assert.Empty(result.RejectedCitations);
        Assert.NotEmpty(result.ValidatedEvidence);
        Assert.All(result.ClaimDetails, c => Assert.True(c.IsSupported));
    }

    [Fact]
    public async Task ValidateAnswerAsync_WhenAnswerContainsUnsupportedFileClaim_MarksInvalidAndAppliesUncertaintyTag()
    {
        // 2 & 5: Unsupported claims rejected/marked invalid; does not accept arbitrary facts
        var chunk = MakeChunk("src/Services/OrderService.cs", "ProcessOrder", "public void ProcessOrder(Order o) { }");
        var validator = new AiEvidenceValidator();

        var answer = "Order processing is located in `OrderService.cs`. Authentication is handled in `src/Auth/SecretHacker.cs` using tokens.";
        var result = await validator.ValidateAnswerAsync(answer, [chunk]);

        Assert.False(result.IsValid);
        Assert.Equal(AnswerValidationStatus.PartiallySupported, result.Status);
        Assert.Single(result.UnsupportedClaims);
        Assert.Contains("src/Auth/SecretHacker.cs", result.UnsupportedClaims[0]);
        Assert.Contains("[Uncertain - unsupported by retrieved evidence]", result.ValidatedAnswer);
        Assert.Contains("OrderService.cs", result.ValidatedAnswer);
    }

    [Fact]
    public async Task ValidateAnswerAsync_WhenPolicyIsRemove_RemovesUnsupportedClaimFromAnswer()
    {
        // 2: Unsupported claims removed according to policy
        var chunk = MakeChunk("src/Services/OrderService.cs", "ProcessOrder", "public void ProcessOrder(Order o) { }");
        var options = new AnswerValidationOptions
        {
            ActionOnUnsupportedClaims = UnsupportedClaimAction.Remove
        };
        var validator = new AiEvidenceValidator(options);

        var answer = "Order processing is in `OrderService.cs`. Authentication is in `src/Auth/FakeService.cs`.";
        var result = await validator.ValidateAnswerAsync(answer, [chunk]);

        Assert.False(result.IsValid);
        Assert.Equal(AnswerValidationStatus.PartiallySupported, result.Status);
        Assert.Contains("OrderService.cs", result.ValidatedAnswer);
        Assert.DoesNotContain("src/Auth/FakeService.cs", result.ValidatedAnswer);
    }

    [Fact]
    public async Task ValidateAnswerAsync_WhenPolicyIsRejectIfAny_ReturnsInsufficientEvidence()
    {
        // 2: Policy returns insufficient evidence if any claim is unsupported
        var chunk = MakeChunk("src/Services/OrderService.cs", "ProcessOrder", "public void ProcessOrder(Order o) { }");
        var options = new AnswerValidationOptions
        {
            ActionOnUnsupportedClaims = UnsupportedClaimAction.RejectIfAny
        };
        var validator = new AiEvidenceValidator(options);

        var answer = "Order processing is in `OrderService.cs`. Authentication is in `src/Auth/FakeService.cs`.";
        var result = await validator.ValidateAnswerAsync(answer, [chunk]);

        Assert.False(result.IsValid);
        Assert.Equal(AnswerValidationStatus.PartiallySupported, result.Status);
        Assert.Equal(options.InsufficientEvidenceMessage, result.ValidatedAnswer);
    }

    [Fact]
    public async Task ValidateAnswerAsync_WhenAnswerContainsUnsupportedSymbol_MarksInvalid()
    {
        // 2 & 5: Referenced symbol not in evidence is flagged
        var chunk = MakeChunk("src/Services/OrderService.cs", "ProcessOrder", "public void ProcessOrder(Order o) { }");
        var validator = new AiEvidenceValidator();

        var answer = "The system calls `UnregisteredBillingMethod` to debit the customer.";
        var result = await validator.ValidateAnswerAsync(answer, [chunk]);

        Assert.False(result.IsValid);
        Assert.Contains("UnregisteredBillingMethod", result.UnsupportedClaims[0]);
    }

    [Fact]
    public async Task ValidateAnswerAsync_WhenAiCitationsMatchRetrievedChunks_AcceptsCitations()
    {
        // 3 & 6: Valid citations are checked against retrieved evidence and accepted
        var chunk = MakeChunk("src/Services/OrderService.cs", "ProcessOrder", "public void ProcessOrder(Order o) { }");
        var validator = new AiEvidenceValidator();

        var aiCitation = new AiEvidenceItem
        {
            File = "src/Services/OrderService.cs",
            Symbol = "ProcessOrder",
            StartLine = 10,
            EndLine = 20,
            Reason = "Explicitly cited by model"
        };

        var result = await validator.ValidateAnswerAsync(
            "Orders are handled by `OrderService.cs`.",
            [chunk],
            [aiCitation]);

        Assert.True(result.IsValid);
        Assert.Empty(result.RejectedCitations);
        Assert.Contains(result.ValidatedEvidence, e => e.File == "src/Services/OrderService.cs" && e.Symbol == "ProcessOrder");
    }

    [Fact]
    public async Task ValidateAnswerAsync_WhenAiCitationDoesNotMatchRetrievedChunks_RejectsCitation()
    {
        // 3 & 4: Missing/invalid citations are rejected as hallucinated
        var chunk = MakeChunk("src/Services/OrderService.cs", "ProcessOrder", "public void ProcessOrder(Order o) { }");
        var validator = new AiEvidenceValidator();

        var fakeCitation = new AiEvidenceItem
        {
            File = "src/Secret/HallucinatedService.cs",
            Symbol = "SecretMethod",
            StartLine = 1,
            EndLine = 50,
            Reason = "Hallucinated citation"
        };

        var result = await validator.ValidateAnswerAsync(
            "Orders are handled by `OrderService.cs`.",
            [chunk],
            [fakeCitation]);

        Assert.False(result.IsValid);
        Assert.Single(result.RejectedCitations);
        Assert.Equal("src/Secret/HallucinatedService.cs", result.RejectedCitations[0].File);
        Assert.DoesNotContain(result.ValidatedEvidence, e => e.File == "src/Secret/HallucinatedService.cs");
    }

    [Fact]
    public async Task ValidateAnswerAsync_WhenEmptyEvidence_AndAffirmativeClaimsMade_ReturnsInsufficientEvidenceAndMarksInvalid()
    {
        // 7: Empty evidence with affirmative claims results in insufficient-evidence response
        var validator = new AiEvidenceValidator();

        var answer = "The repository uses EF Core PostgreSQL database in `DbContext.cs`.";
        var result = await validator.ValidateAnswerAsync(answer, []);

        Assert.False(result.IsValid);
        Assert.Equal(AnswerValidationStatus.InsufficientEvidence, result.Status);
        Assert.Equal("Insufficient evidence in the analyzed repository.", result.ValidatedAnswer);
        Assert.Single(result.UnsupportedClaims);
        Assert.Empty(result.ValidatedEvidence);
    }

    [Fact]
    public async Task ValidateAnswerAsync_WhenEmptyEvidence_AndAnswerAlreadyAcknowledgesNoEvidence_ReturnsValidWithInsufficientEvidenceStatus()
    {
        // 7: Empty evidence where answer honestly states insufficient evidence passes
        var validator = new AiEvidenceValidator();

        var answer = "There is insufficient evidence in the analyzed repository to answer this question.";
        var result = await validator.ValidateAnswerAsync(answer, []);

        Assert.True(result.IsValid);
        Assert.Equal(AnswerValidationStatus.InsufficientEvidence, result.Status);
        Assert.Equal(answer, result.ValidatedAnswer);
        Assert.Empty(result.UnsupportedClaims);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ValidateAnswerAsync_WhenAnswerIsEmptyOrWhitespace_ReturnsInvalidWithInsufficientEvidence(string? emptyAnswer)
    {
        // 4: Missing/invalid answer handled safely
        var validator = new AiEvidenceValidator();

        var result = await validator.ValidateAnswerAsync(emptyAnswer!, [MakeChunk()]);

        Assert.False(result.IsValid);
        Assert.Equal(AnswerValidationStatus.InsufficientEvidence, result.Status);
    }

    [Fact]
    public async Task ValidateAnswerAsync_WithMultipleChunksAndCitations_HandlesAllCorrectly()
    {
        // 6: Multiple evidence items handled correctly across relative paths and backslashes
        var chunk1 = MakeChunk("src/Api/Controllers/UserController.cs", "UserController.Get", "public IActionResult Get() { }");
        var chunk2 = MakeChunk("src/Domain/Entities/User.cs", "User", "public class User { public Guid Id { get; set; } }");
        var chunk3 = MakeChunk("src/Infrastructure/Data/UserDb.cs", "UserDb", "public class UserDb : DbContext { }");

        var validator = new AiEvidenceValidator();

        var citation1 = new AiEvidenceItem { File = "UserController.cs", Symbol = "UserController.Get" }; // short path match
        var citation2 = new AiEvidenceItem { File = "src\\Domain\\Entities\\User.cs", Symbol = "User" }; // backslash match
        var fakeCitation = new AiEvidenceItem { File = "src/Api/Fake.cs", Symbol = "Fake" }; // invalid

        var answer = "The API exposes `UserController.cs` and the domain model is `User.cs`.";

        var result = await validator.ValidateAnswerAsync(
            answer,
            [chunk1, chunk2, chunk3],
            [citation1, citation2, fakeCitation]);

        Assert.False(result.IsValid); // false because fakeCitation was rejected
        Assert.Single(result.RejectedCitations);
        Assert.Equal("src/Api/Fake.cs", result.RejectedCitations[0].File);
        Assert.Contains(result.ValidatedEvidence, e => e.File == "src/Api/Controllers/UserController.cs");
        Assert.Contains(result.ValidatedEvidence, e => e.File == "src/Domain/Entities/User.cs");
    }

    [Fact]
    public async Task ValidateAnswerAsync_PropagatesCancellationToken()
    {
        // 8: Cancellation propagation
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var validator = new AiEvidenceValidator();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            validator.ValidateAnswerAsync("Some answer", [MakeChunk()], cancellationToken: cts.Token));
    }
}

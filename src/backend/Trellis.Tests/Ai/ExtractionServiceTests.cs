using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Files;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Dtos;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Application.Services;
using Trellis.Api.Modules.Ai.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Ai;

public sealed class ExtractionServiceTests
{
    private readonly IExtractionJobRepository _repository = Substitute.For<IExtractionJobRepository>();
    private readonly IOverrideLogRepository _overrideLogRepository = Substitute.For<IOverrideLogRepository>();
    private readonly IAiProvider _aiProvider = Substitute.For<IAiProvider>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly IAiTierGate _tierGate = Substitute.For<IAiTierGate>();

    public ExtractionServiceTests()
    {
        _tierGate.IsEntitledAsync(Arg.Any<Guid>()).Returns(true);
        _fileStorage.OpenReadAsync(Arg.Any<string>()).Returns(Stream.Null);
        _repository.GetRowVersion(Arg.Any<ExtractionJob>()).Returns("AAAAAAAAAAA=");
    }

    private ExtractionService CreateSut() => new(_repository, _overrideLogRepository, _aiProvider, _fileStorage, _auditService, _tierGate);

    private ExtractionJob CaptureAddedJob()
    {
        ExtractionJob? captured = null;
        _repository.AddAsync(Arg.Do<ExtractionJob>(j => captured = j));
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns(_ => captured);
        return captured!;
    }

    [Fact]
    public async Task ExtractAsync_WhenTenantIsStarter_CreatesNoJobAndNeverCallsProvider()
    {
        _tierGate.IsEntitledAsync(Arg.Any<Guid>()).Returns(false);
        var sut = CreateSut();

        await sut.ExtractAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LEASE", "key", "lease.pdf", Guid.NewGuid());

        await _repository.DidNotReceive().AddAsync(Arg.Any<ExtractionJob>());
        await _aiProvider.DidNotReceive().ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact(Timeout = 60000)]
    public async Task ExtractAsync_WhenProviderThrowsHttpRequestException_MarksManualReviewRequired_AndDoesNotThrow()
    {
        // The literal Phase 6 exit criterion: a provider failure degrades to manual review
        // without blocking the underlying document upload — this call must not throw. Runs
        // through the real ResiliencePolicies.ExternalProviderPolicy (3 attempts, 2s/8s/32s
        // backoff), so this one test genuinely takes ~42s — worth the one-time cost to prove the
        // exit criterion against the real policy, not a bypassed one. The TaskCanceledException
        // branch is handled by the identical catch clause (`ex is HttpRequestException or
        // TaskCanceledException`), so a second, equally slow test for it would be redundant.
        CaptureAddedJob();
        _aiProvider.ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException<ExtractionResult>(new HttpRequestException()));
        var sut = CreateSut();

        var act = () => sut.ExtractAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LEASE", "key", "lease.pdf", Guid.NewGuid());

        await act.Should().NotThrowAsync();
        var job = await _repository.GetByIdAsync(Guid.NewGuid());
        job!.Status.Should().Be(ExtractionStatus.ManualReviewRequired);
    }

    [Fact]
    public async Task ExtractAsync_WhenProviderSucceeds_MarksExtracted_WithConfidence()
    {
        CaptureAddedJob();
        _aiProvider.ExtractAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ExtractionResult([new ExtractedField("PartyName", "Acme")], 0.9m));
        var sut = CreateSut();

        await sut.ExtractAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LEASE", "key", "lease.pdf", Guid.NewGuid());

        var job = await _repository.GetByIdAsync(Guid.NewGuid());
        job!.Status.Should().Be(ExtractionStatus.Extracted);
        job.Confidence.Should().Be(0.9m);
    }

    [Fact]
    public async Task ConfirmAsync_WhenFieldsChangedFromExtracted_WritesOverrideLogAndAudit()
    {
        var job = ExtractionJob.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LEASE");
        job.MarkExtracted("""[{"FieldName":"PartyName","Value":"Acme"}]""", 0.9m);
        _repository.GetByIdAsync(job.ExtractionJobId).Returns(job);

        var sut = CreateSut();
        var request = new ExtractionConfirmRequest([new ExtractedFieldDto("PartyName", "Acme Corrected")], "AAAAAAAAAAA=");

        var result = await sut.ConfirmAsync(job.ExtractionJobId, request, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        job.Status.Should().Be(ExtractionStatus.Confirmed);
        await _overrideLogRepository.Received(1).AddAsync(Arg.Any<OverrideLogEntry>());
        await _auditService.Received(1).RecordAsync(
            job.TenantId, Arg.Any<Guid>(), AuditActorType.HumanUser, Trellis.Api.Core.Audit.AuditTypes.Ai.SuggestionOverridden, Arg.Any<string>());
    }

    [Fact]
    public async Task ConfirmAsync_WhenFieldsUnchanged_DoesNotWriteOverrideLog()
    {
        var job = ExtractionJob.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LEASE");
        job.MarkExtracted("""[{"FieldName":"PartyName","Value":"Acme"}]""", 0.9m);
        _repository.GetByIdAsync(job.ExtractionJobId).Returns(job);

        var sut = CreateSut();
        var request = new ExtractionConfirmRequest([new ExtractedFieldDto("PartyName", "Acme")], "AAAAAAAAAAA=");

        await sut.ConfirmAsync(job.ExtractionJobId, request, Guid.NewGuid());

        await _overrideLogRepository.DidNotReceive().AddAsync(Arg.Any<OverrideLogEntry>());
    }

    [Fact]
    public async Task ConfirmAsync_WhenAlreadyConfirmed_ReturnsBusinessRuleViolation()
    {
        var job = ExtractionJob.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LEASE");
        job.MarkExtracted("[]", 0.9m);
        job.Confirm("[]", Guid.NewGuid());
        _repository.GetByIdAsync(job.ExtractionJobId).Returns(job);

        var sut = CreateSut();
        var request = new ExtractionConfirmRequest([], "AAAAAAAAAAA=");

        var result = await sut.ConfirmAsync(job.ExtractionJobId, request, Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
    }
}

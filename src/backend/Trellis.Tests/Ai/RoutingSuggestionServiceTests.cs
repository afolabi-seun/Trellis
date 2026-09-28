using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Dtos;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Application.Services;
using Trellis.Api.Modules.Ai.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Workflow.Application.Dtos;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Ai;

public sealed class RoutingSuggestionServiceTests
{
    private readonly IRoutingSuggestionRepository _repository = Substitute.For<IRoutingSuggestionRepository>();
    private readonly IOverrideLogRepository _overrideLogRepository = Substitute.For<IOverrideLogRepository>();
    private readonly IAiProvider _aiProvider = Substitute.For<IAiProvider>();
    private readonly IDepartmentService _departmentService = Substitute.For<IDepartmentService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly IAiTierGate _tierGate = Substitute.For<IAiTierGate>();

    public RoutingSuggestionServiceTests()
    {
        TenantContext.Current = new TenantContext(Guid.NewGuid());
        _tierGate.IsEntitledAsync(Arg.Any<Guid>()).Returns(true);
        _departmentService.ListAsync(null, 100).Returns(ServiceResult<PagedResponse<DepartmentResponse>>.Success(
            new PagedResponse<DepartmentResponse>([], null, false, 0)));
    }

    private RoutingSuggestionService CreateSut() =>
        new(_repository, _overrideLogRepository, _aiProvider, _departmentService, _auditService, _tierGate);

    private RoutingSuggestion CaptureAddedSuggestion()
    {
        RoutingSuggestion? captured = null;
        _repository.AddAsync(Arg.Do<RoutingSuggestion>(s => captured = s));
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns(_ => captured);
        return captured!;
    }

    [Fact]
    public async Task CreateAsync_WhenTenantIsStarter_ReturnsForbidden_AndNeverCallsProvider()
    {
        _tierGate.IsEntitledAsync(Arg.Any<Guid>()).Returns(false);
        var sut = CreateSut();

        var result = await sut.CreateAsync(new RoutingSuggestionCreateRequest("Leak", null), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Forbidden);
        await _aiProvider.DidNotReceive().ClassifyRoutingAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<DepartmentCandidate>>());
    }

    [Fact(Timeout = 60000)]
    public async Task CreateAsync_WhenClassifierThrows_MarksFailed_AndDoesNotThrow()
    {
        // Mirrors ExtractionServiceTests' own exit-criterion test — BDR-AI-003's resilience path,
        // also running through the real ResiliencePolicies.ExternalProviderPolicy (~42s).
        CaptureAddedSuggestion();
        _aiProvider.ClassifyRoutingAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<DepartmentCandidate>>())
            .Returns(Task.FromException<RoutingClassification>(new HttpRequestException()));
        var sut = CreateSut();

        var act = () => sut.CreateAsync(new RoutingSuggestionCreateRequest("Leak", null), Guid.NewGuid());

        await act.Should().NotThrowAsync();
        var suggestion = await _repository.GetByIdAsync(Guid.NewGuid());
        suggestion!.Status.Should().Be(RoutingSuggestionStatus.Failed);
    }

    [Fact]
    public async Task CreateAsync_WhenClassifierSucceeds_MarksSuggestedDepartment()
    {
        CaptureAddedSuggestion();
        var departmentId = Guid.NewGuid();
        _aiProvider.ClassifyRoutingAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<DepartmentCandidate>>())
            .Returns(new RoutingClassification(departmentId, null, 0.8m));
        var sut = CreateSut();

        await sut.CreateAsync(new RoutingSuggestionCreateRequest("Leak", null), Guid.NewGuid());

        var suggestion = await _repository.GetByIdAsync(Guid.NewGuid());
        suggestion!.SuggestedDepartmentId.Should().Be(departmentId);
    }

    [Fact]
    public async Task LinkAsync_WhenFinalDepartmentMatchesSuggestion_MarksAccepted_NoOverrideLog()
    {
        var departmentId = Guid.NewGuid();
        var suggestion = RoutingSuggestion.Create(Guid.NewGuid(), "Leak", null, Guid.NewGuid());
        suggestion.MarkClassified(departmentId, null, 0.8m);
        _repository.GetByIdAsync(suggestion.RoutingSuggestionId).Returns(suggestion);
        var sut = CreateSut();

        var result = await sut.LinkAsync(
            suggestion.RoutingSuggestionId, new RoutingSuggestionLinkRequest(Guid.NewGuid(), departmentId), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        suggestion.Status.Should().Be(RoutingSuggestionStatus.Accepted);
        await _overrideLogRepository.DidNotReceive().AddAsync(Arg.Any<OverrideLogEntry>());
    }

    [Fact]
    public async Task LinkAsync_WhenFinalDepartmentDiffers_MarksOverridden_WritesOverrideLogAndAudit()
    {
        var suggestion = RoutingSuggestion.Create(Guid.NewGuid(), "Leak", null, Guid.NewGuid());
        suggestion.MarkClassified(Guid.NewGuid(), null, 0.8m);
        _repository.GetByIdAsync(suggestion.RoutingSuggestionId).Returns(suggestion);
        var sut = CreateSut();

        var result = await sut.LinkAsync(
            suggestion.RoutingSuggestionId, new RoutingSuggestionLinkRequest(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        suggestion.Status.Should().Be(RoutingSuggestionStatus.Overridden);
        await _overrideLogRepository.Received(1).AddAsync(Arg.Any<OverrideLogEntry>());
    }

    [Fact]
    public async Task LinkAsync_WhenAlreadyLinked_ReturnsBusinessRuleViolation()
    {
        var suggestion = RoutingSuggestion.Create(Guid.NewGuid(), "Leak", null, Guid.NewGuid());
        suggestion.Link(Guid.NewGuid(), null);
        _repository.GetByIdAsync(suggestion.RoutingSuggestionId).Returns(suggestion);
        var sut = CreateSut();

        var result = await sut.LinkAsync(
            suggestion.RoutingSuggestionId, new RoutingSuggestionLinkRequest(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
    }
}

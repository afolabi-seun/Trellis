using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Application.Services;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;
using Xunit;

namespace Trellis.Tests.PropertyAsset;

/// <summary>Unit layer (mocked repository, no database) — the business-rule guards Testcontainers' RLS/concurrency tests don't cover.</summary>
public sealed class OwnerServiceTests
{
    public OwnerServiceTests() => TenantContext.Current = new TenantContext(Guid.NewGuid());

    [Fact]
    public async Task ArchiveAsync_WhenOwnerHasActiveProperty_ReturnsBusinessRuleViolation()
    {
        var owner = Owner.Create(TenantContext.Current!.TenantId, "Test Owner", null, null);
        var repository = Substitute.For<IOwnerRepository>();
        repository.GetByIdAsync(owner.OwnerId).Returns(owner);
        repository.HasActivePropertiesAsync(owner.OwnerId).Returns(true);

        var sut = new OwnerService(repository);
        var result = await sut.ArchiveAsync(owner.OwnerId);

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
        await repository.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task ArchiveAsync_WhenNoActiveProperties_ArchivesSuccessfully()
    {
        var owner = Owner.Create(TenantContext.Current!.TenantId, "Test Owner", null, null);
        var repository = Substitute.For<IOwnerRepository>();
        repository.GetByIdAsync(owner.OwnerId).Returns(owner);
        repository.HasActivePropertiesAsync(owner.OwnerId).Returns(false);
        repository.GetRowVersion(owner).Returns("irrelevant");

        var sut = new OwnerService(repository);
        var result = await sut.ArchiveAsync(owner.OwnerId);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Status.Should().Be("ARCHIVED");
    }

    [Fact]
    public async Task GetByIdAsync_WhenOwnerDoesNotExist_ReturnsNotFound()
    {
        var repository = Substitute.For<IOwnerRepository>();
        repository.GetByIdAsync(Arg.Any<Guid>()).Returns((Owner?)null);

        var sut = new OwnerService(repository);
        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.Outcome.Should().Be(ServiceResultOutcome.NotFound);
    }
}

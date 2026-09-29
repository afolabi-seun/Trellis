using FluentAssertions;
using NSubstitute;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Application.Services;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;
using Xunit;

namespace Trellis.Tests.PropertyAsset;

public sealed class PropertyAssetPersonalDataHandlerTests
{
    private const string SubjectEmail = "owner@example.com";
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly IOwnerRepository _ownerRepository = Substitute.For<IOwnerRepository>();

    private PropertyAssetPersonalDataHandler CreateSut() => new(_ownerRepository);

    [Fact]
    public async Task ExportAsync_WithNoMatchingOwner_ReturnsNull()
    {
        _ownerRepository.GetByEmailAsync(SubjectEmail).Returns((Owner?)null);
        var sut = CreateSut();

        var result = await sut.ExportAsync(SubjectEmail);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EraseAsync_RedactsTheOwnersNameEmailAndPhone_KeepsTheRowActive()
    {
        var owner = Owner.Create(_tenantId, "Jane Owner", SubjectEmail, "0821234567");
        _ownerRepository.GetByEmailAsync(SubjectEmail).Returns(owner);
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectEmail);

        owner.Name.Should().Be("Erased owner");
        owner.Email.Should().BeNull();
        owner.Phone.Should().BeNull();
        owner.Status.Should().Be(OwnerStatus.Active);
        summary.ModuleName.Should().Be("PropertyAsset");
        summary.RecordsAffected.Should().Be(1);
        await _ownerRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task EraseAsync_WhenNoOwnerMatches_ReturnsZero_AndSavesNothing()
    {
        _ownerRepository.GetByEmailAsync(SubjectEmail).Returns((Owner?)null);
        var sut = CreateSut();

        var summary = await sut.EraseAsync(SubjectEmail);

        summary.RecordsAffected.Should().Be(0);
        await _ownerRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }
}

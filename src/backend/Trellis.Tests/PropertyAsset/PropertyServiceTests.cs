using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.PropertyAsset.Application.Dtos;
using Trellis.Api.Modules.PropertyAsset.Application.Interfaces;
using Trellis.Api.Modules.PropertyAsset.Application.Services;
using Trellis.Api.Modules.PropertyAsset.Domain.Entities;
using Xunit;

namespace Trellis.Tests.PropertyAsset;

public sealed class PropertyServiceTests
{
    public PropertyServiceTests() => TenantContext.Current = new TenantContext(Guid.NewGuid());

    [Fact]
    public async Task ArchiveAsync_WhenPropertyHasActiveUnitAsset_ReturnsBusinessRuleViolation()
    {
        var property = Property.Create(
            TenantContext.Current!.TenantId, Guid.NewGuid(), "1 Test St", PropertyType.Residential, null, null, null, null);
        var repository = Substitute.For<IPropertyRepository>();
        repository.GetByIdAsync(property.PropertyId).Returns(property);
        repository.HasActiveUnitAssetsAsync(property.PropertyId).Returns(true);

        var sut = new PropertyService(repository);
        var result = await sut.ArchiveAsync(property.PropertyId);

        result.Outcome.Should().Be(ServiceResultOutcome.BusinessRuleViolation);
    }

    [Fact]
    public async Task UpdateAsync_MapsMixedUsePropertyTypeStringCorrectlyBothWays()
    {
        var ownerId = Guid.NewGuid();
        var property = Property.Create(
            TenantContext.Current!.TenantId, ownerId, "1 Test St", PropertyType.Residential, null, null, null, null);
        var repository = Substitute.For<IPropertyRepository>();
        repository.GetByIdAsync(property.PropertyId).Returns(property);
        repository.GetRowVersion(property).Returns("irrelevant");

        var sut = new PropertyService(repository);
        var result = await sut.UpdateAsync(
            property.PropertyId,
            new PropertyUpdateRequest(ownerId, "1 Test St", "MIXED_USE", null, null, null, null, "irrelevant"));

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.PropertyType.Should().Be("MIXED_USE");
    }

    [Fact]
    public async Task UpdateAsync_WhenSaveThrowsConcurrencyException_ReturnsConcurrencyConflict()
    {
        var ownerId = Guid.NewGuid();
        var property = Property.Create(
            TenantContext.Current!.TenantId, ownerId, "1 Test St", PropertyType.Residential, null, null, null, null);
        var repository = Substitute.For<IPropertyRepository>();
        repository.GetByIdAsync(property.PropertyId).Returns(property);
        repository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateConcurrencyException()));

        var sut = new PropertyService(repository);
        var result = await sut.UpdateAsync(
            property.PropertyId,
            new PropertyUpdateRequest(ownerId, "1 Test St", "RESIDENTIAL", null, null, null, null, "stale-version"));

        result.Outcome.Should().Be(ServiceResultOutcome.ConcurrencyConflict);
    }
}

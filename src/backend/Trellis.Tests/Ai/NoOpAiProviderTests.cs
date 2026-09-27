using FluentAssertions;
using Trellis.Api.Modules.Ai.Application.Interfaces;
using Trellis.Api.Modules.Ai.Infrastructure.Providers;
using Xunit;

namespace Trellis.Tests.Ai;

public sealed class NoOpAiProviderTests
{
    private readonly NoOpAiProvider _sut = new();

    [Fact]
    public async Task ExtractAsync_ReturnsFixedPlaceholderFields()
    {
        var result = await _sut.ExtractAsync(Stream.Null, "lease.pdf", "LEASE");

        result.Fields.Should().NotBeEmpty();
        result.Confidence.Should().BeInRange(0m, 1m);
    }

    [Fact]
    public async Task ClassifyRoutingAsync_WhenAMaintenanceDepartmentExists_SuggestsIt()
    {
        var maintenanceId = Guid.NewGuid();
        var departments = new List<DepartmentCandidate> { new(Guid.NewGuid(), "Legal"), new(maintenanceId, "Maintenance") };

        var result = await _sut.ClassifyRoutingAsync("Leaking pipe", departments);

        result.SuggestedDepartmentId.Should().Be(maintenanceId);
        result.SuggestedVendorCategory.Should().BeNull();
    }

    [Fact]
    public async Task ClassifyRoutingAsync_WhenNoMaintenanceDepartmentExists_FallsBackToVendorCategory()
    {
        var departments = new List<DepartmentCandidate> { new(Guid.NewGuid(), "Legal") };

        var result = await _sut.ClassifyRoutingAsync("Leaking pipe", departments);

        result.SuggestedDepartmentId.Should().BeNull();
        result.SuggestedVendorCategory.Should().Be("MAINTENANCE");
    }
}

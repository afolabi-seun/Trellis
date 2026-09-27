using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Ai.Application.Services;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.Ai;

public sealed class AiTierGateTests
{
    private readonly ITenantService _tenantService = Substitute.For<ITenantService>();

    private AiTierGate CreateSut() => new(_tenantService);

    [Theory]
    [InlineData("PROFESSIONAL", true)]
    [InlineData("ENTERPRISE", true)]
    [InlineData("STARTER", false)]
    public async Task IsEntitledAsync_ReflectsSubscriptionTier(string tier, bool expected)
    {
        var tenantId = Guid.NewGuid();
        _tenantService.GetByIdAsync(tenantId).Returns(ServiceResult<TenantResponse>.Success(
            new TenantResponse(tenantId, "Acme", "US", "ACTIVE", tier, DateTimeOffset.UtcNow)));

        var sut = CreateSut();
        var result = await sut.IsEntitledAsync(tenantId);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task IsEntitledAsync_WhenTenantLookupFails_ReturnsFalse()
    {
        var tenantId = Guid.NewGuid();
        _tenantService.GetByIdAsync(tenantId).Returns(ServiceResult<TenantResponse>.NotFound());

        var sut = CreateSut();
        var result = await sut.IsEntitledAsync(tenantId);

        result.Should().BeFalse();
    }
}

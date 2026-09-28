using FluentAssertions;
using NSubstitute;
using Trellis.Api.Core.Audit;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Billing.Application.Dtos;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Billing.Application.Services;
using Trellis.Api.Modules.Billing.Domain.Entities;
using Xunit;

namespace Trellis.Tests.Billing;

public sealed class ModuleEntitlementServiceTests
{
    private readonly IModuleEntitlementRepository _repository = Substitute.For<IModuleEntitlementRepository>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();

    private ModuleEntitlementService CreateSut() => new(_repository, _auditService);

    [Fact]
    public async Task IsEntitledAsync_WhenEntitlementIsActive_ReturnsTrue()
    {
        var tenantId = Guid.NewGuid();
        _repository.GetAsync(tenantId, "INSPECTION").Returns(ModuleEntitlement.Create(tenantId, "INSPECTION", 0));
        var sut = CreateSut();

        (await sut.IsEntitledAsync(tenantId, "INSPECTION")).Should().BeTrue();
    }

    [Fact]
    public async Task IsEntitledAsync_WhenEntitlementIsDisabled_ReturnsFalse()
    {
        var tenantId = Guid.NewGuid();
        var entitlement = ModuleEntitlement.Create(tenantId, "AI", 0);
        entitlement.Disable();
        _repository.GetAsync(tenantId, "AI").Returns(entitlement);
        var sut = CreateSut();

        (await sut.IsEntitledAsync(tenantId, "AI")).Should().BeFalse();
    }

    [Fact]
    public async Task IsEntitledAsync_WhenNoEntitlementRowExists_ReturnsFalse()
    {
        var tenantId = Guid.NewGuid();
        _repository.GetAsync(tenantId, "LEASING").Returns((ModuleEntitlement?)null);
        var sut = CreateSut();

        (await sut.IsEntitledAsync(tenantId, "LEASING")).Should().BeFalse();
    }

    [Fact]
    public async Task IsEntitledAsync_WhenNoRowExistsYetForADefaultFreeModule_LazilyBootstrapsItActive()
    {
        var tenantId = Guid.NewGuid();
        _repository.GetAsync(tenantId, "INSPECTION").Returns((ModuleEntitlement?)null);
        var sut = CreateSut();

        var result = await sut.IsEntitledAsync(tenantId, "INSPECTION");

        result.Should().BeTrue();
        await _repository.Received(1).AddAsync(Arg.Is<ModuleEntitlement>(e => e.ModuleKey == "INSPECTION" && e.MonthlyPrice == 0));
        await _repository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task SetAsync_WhenNoEntitlementExistsYet_CreatesOneAtTheRequestedStatusAndPrice()
    {
        var tenantId = Guid.NewGuid();
        _repository.GetAsync(tenantId, "INSPECTION").Returns((ModuleEntitlement?)null);
        var sut = CreateSut();

        var result = await sut.SetAsync(tenantId, "INSPECTION", new ModuleEntitlementSetRequest("ACTIVE", 49.00m));

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.MonthlyPrice.Should().Be(49.00m);
        result.Data.Status.Should().Be("ACTIVE");
        await _repository.Received(1).AddAsync(Arg.Any<ModuleEntitlement>());
    }

    [Fact]
    public async Task SetAsync_WhenDisablingAnExistingActiveEntitlement_TransitionsItToDisabled()
    {
        var tenantId = Guid.NewGuid();
        var entitlement = ModuleEntitlement.Create(tenantId, "AI", 0);
        _repository.GetAsync(tenantId, "AI").Returns(entitlement);
        var sut = CreateSut();

        var result = await sut.SetAsync(tenantId, "AI", new ModuleEntitlementSetRequest("DISABLED", 0));

        result.Data!.Status.Should().Be("DISABLED");
    }

    [Fact]
    public async Task EnsureDefaultEntitlementsAsync_ForABrandNewTenant_CreatesFreeInspectionAndAiEntitlements()
    {
        var tenantId = Guid.NewGuid();
        _repository.GetAsync(tenantId, Arg.Any<string>()).Returns((ModuleEntitlement?)null);
        var sut = CreateSut();

        await sut.EnsureDefaultEntitlementsAsync(tenantId);

        await _repository.Received(1).AddAsync(Arg.Is<ModuleEntitlement>(e => e.ModuleKey == "INSPECTION" && e.MonthlyPrice == 0));
        await _repository.Received(1).AddAsync(Arg.Is<ModuleEntitlement>(e => e.ModuleKey == "AI" && e.MonthlyPrice == 0));
        await _repository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task EnsureDefaultEntitlementsAsync_WhenBothRowsAlreadyExist_IsANoOp()
    {
        var tenantId = Guid.NewGuid();
        _repository.GetAsync(tenantId, "INSPECTION").Returns(ModuleEntitlement.Create(tenantId, "INSPECTION", 0));
        _repository.GetAsync(tenantId, "AI").Returns(ModuleEntitlement.Create(tenantId, "AI", 0));
        var sut = CreateSut();

        await sut.EnsureDefaultEntitlementsAsync(tenantId);

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync();
    }
}

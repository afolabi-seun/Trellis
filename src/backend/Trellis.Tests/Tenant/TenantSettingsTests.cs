using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Trellis.Api.Core.Helpers;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Trellis.Api.Modules.Reporting.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Application.Services;
using Trellis.Api.Modules.Tenant.Application.Validators;
using Trellis.Api.Modules.Tenant.Domain.ValueObjects;
using Xunit;
using TenantEntity = Trellis.Api.Modules.Tenant.Domain.Entities.Tenant;

namespace Trellis.Tests.Tenant;

public sealed class TenantSettingsSerializerTests
{
    private static readonly string[] AdminOnly = ["TENANT_ADMIN"];
    private static readonly string[] Both = ["TENANT_ADMIN", "DEPARTMENT_STAFF"];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    [InlineData("{\"other\":1}")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    public void ReadNotifications_WithNothingUsable_FallsBackToBothRoles_NeverThrows(string? json)
    {
        var settings = TenantSettingsSerializer.ReadNotifications(json);

        settings.ApplicationSubmittedRoles.Should().BeEquivalentTo(Both);
        settings.RentArrearsRoles.Should().BeEquivalentTo(Both);
    }

    [Fact]
    public void WriteThenRead_RoundTripsBothLists()
    {
        var json = TenantSettingsSerializer.WriteNotifications(null, new NotificationRecipientSettings(AdminOnly, Both));

        var settings = TenantSettingsSerializer.ReadNotifications(json);

        settings.ApplicationSubmittedRoles.Should().Equal(AdminOnly);
        settings.RentArrearsRoles.Should().Equal(Both);
    }

    [Fact]
    public void Write_PreservesOtherKeysAlreadyInTheConfigDocument()
    {
        var existing = "{\"featureFlags\":{\"beta\":true},\"notifications\":{\"applicationSubmittedRoles\":[\"TENANT_ADMIN\"]}}";

        var json = TenantSettingsSerializer.WriteNotifications(existing, new NotificationRecipientSettings(Both, AdminOnly));

        var root = JsonNode.Parse(json)!;
        root["featureFlags"]!["beta"]!.GetValue<bool>().Should().BeTrue("another setting's data must survive an update to this one");
        TenantSettingsSerializer.ReadNotifications(json).RentArrearsRoles.Should().Equal(AdminOnly);
    }

    [Fact]
    public void Write_OverACorruptExistingDocument_StartsFresh_RatherThanFailing()
    {
        var json = TenantSettingsSerializer.WriteNotifications("{{{ nope", new NotificationRecipientSettings(AdminOnly, AdminOnly));

        TenantSettingsSerializer.ReadNotifications(json).ApplicationSubmittedRoles.Should().Equal(AdminOnly);
    }

    [Theory]
    [InlineData("{\"notifications\":{\"applicationSubmittedRoles\":[],\"rentArrearsRoles\":[\"TENANT_ADMIN\"]}}")]
    [InlineData("{\"notifications\":{\"applicationSubmittedRoles\":[\"VENDOR_USER\"],\"rentArrearsRoles\":[\"TENANT_ADMIN\"]}}")]
    [InlineData("{\"notifications\":{\"applicationSubmittedRoles\":[\"TENANT_ADMIN\",\"TENANT_ADMIN\"],\"rentArrearsRoles\":[\"TENANT_ADMIN\"]}}")]
    [InlineData("{\"notifications\":{\"applicationSubmittedRoles\":\"TENANT_ADMIN\",\"rentArrearsRoles\":[\"TENANT_ADMIN\"]}}")]
    public void ReadNotifications_ACorruptedList_FallsBackToBothRoles_SoAnAlertIsNeverSilenced(string json)
    {
        var settings = TenantSettingsSerializer.ReadNotifications(json);

        settings.ApplicationSubmittedRoles.Should().BeEquivalentTo(Both);
        settings.RentArrearsRoles.Should().Equal(AdminOnly, "the other, valid list is still honoured");
    }
}

public sealed class StaffRolesTests
{
    [Theory]
    [InlineData("TENANT_ADMIN", true)]
    [InlineData("DEPARTMENT_STAFF", true)]
    [InlineData("TENANT_ADMIN,DEPARTMENT_STAFF", true)]
    [InlineData("", false)]
    [InlineData("TENANT_ADMIN,TENANT_ADMIN", false)]
    [InlineData("SUPER_ADMIN", false)]
    [InlineData("VENDOR_USER,TENANT_ADMIN", false)]
    public void IsValidSelection_AllowsOnlyANonEmptyDistinctSubsetOfTheTwoStaffRoles(string rolesCsv, bool expected) =>
        StaffRoles.IsValidSelection(rolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)).Should().Be(expected);

    [Fact]
    public void IsValidSelection_NullIsInvalid() => StaffRoles.IsValidSelection(null).Should().BeFalse();
}

public sealed class CurrencyDefaultsTests
{
    [Theory]
    [InlineData("ZA", "ZAR")]
    [InlineData("za", "ZAR")]
    [InlineData("KE", "KES")]
    [InlineData("US", "USD")]
    [InlineData("GB", "USD")]
    [InlineData("", "USD")]
    [InlineData(null, "USD")]
    public void ForCountry_MapsTheNamedMarkets_AndFallsBackToUsd(string? country, string expected) =>
        CurrencyDefaults.ForCountry(country).Should().Be(expected);
}

public sealed class TenantSettingsServiceTests : IDisposable
{
    private readonly TenantEntity _tenant = TenantEntity.Create("Acme PM", "ZA", "acme");
    private readonly ITenantRepository _repository = Substitute.For<ITenantRepository>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly Guid _actor = Guid.NewGuid();

    public TenantSettingsServiceTests()
    {
        TenantContext.Current = new TenantContext(_tenant.TenantId);
        _repository.GetByIdAsync(_tenant.TenantId).Returns(_tenant);
    }

    public void Dispose() => TenantContext.Current = null;

    private TenantSettingsService CreateSut() => new(_repository, _auditService);

    [Fact]
    public async Task GetAsync_ForAFreshTenant_ReturnsDefaultRecipientsAndTheCountrysCurrency()
    {
        var result = await CreateSut().GetAsync();

        result.Data!.DefaultCurrency.Should().Be("ZAR");
        result.Data.CountryCode.Should().Be("ZA");
        result.Data.Notifications.ApplicationSubmittedRoles.Should().BeEquivalentTo(["TENANT_ADMIN", "DEPARTMENT_STAFF"]);
    }

    [Fact]
    public async Task UpdateNotificationsAsync_PersistsTheChoice_AndAuditsTheRolesOnly()
    {
        var sut = CreateSut();

        var result = await sut.UpdateNotificationsAsync(new NotificationSettingsDto(["TENANT_ADMIN"], ["DEPARTMENT_STAFF"]), _actor);

        result.Outcome.Should().Be(ServiceResultOutcome.Success);
        result.Data!.Notifications.RentArrearsRoles.Should().Equal("DEPARTMENT_STAFF");
        (await sut.GetNotificationSettingsAsync(_tenant.TenantId)).ApplicationSubmittedRoles.Should().Equal("TENANT_ADMIN");
        await _repository.Received(1).SaveChangesAsync();
        await _auditService.Received(1).RecordAsync(
            _tenant.TenantId, _actor, AuditActorType.HumanUser, AuditTypes.Tenant.TenantSettingsUpdated,
            Arg.Is<string>(d => d.Contains("TENANT_ADMIN") && d.Contains("DEPARTMENT_STAFF") && !d.Contains('@')));
    }

    [Fact]
    public async Task UpdateNotificationsAsync_OnAConcurrentEdit_ReturnsAConflict_NotASilentOverwrite()
    {
        _repository.SaveChangesAsync().Returns(Task.FromException(new DbUpdateConcurrencyException()));

        var result = await CreateSut().UpdateNotificationsAsync(new NotificationSettingsDto(["TENANT_ADMIN"], ["TENANT_ADMIN"]), _actor);

        result.Outcome.Should().Be(ServiceResultOutcome.ConcurrencyConflict);
        await _auditService.DidNotReceiveWithAnyArgs().RecordAsync(default, default, default, default!, default);
    }

    [Fact]
    public async Task GetNotificationSettingsAsync_ForAnUnknownTenant_ReturnsDefaults_SoAnAlertIsNotSilenced()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns((TenantEntity?)null);

        var settings = await CreateSut().GetNotificationSettingsAsync(Guid.NewGuid());

        settings.RentArrearsRoles.Should().BeEquivalentTo(["TENANT_ADMIN", "DEPARTMENT_STAFF"]);
    }

    [Fact]
    public async Task GetNotificationSettingsAsync_NeedsNoAmbientTenantContext()
    {
        TenantContext.Current = null;

        var settings = await CreateSut().GetNotificationSettingsAsync(_tenant.TenantId);

        settings.Should().NotBeNull();
    }
}

public sealed class NotificationSettingsValidatorTests
{
    private readonly NotificationSettingsDtoValidator _validator = new();

    [Fact]
    public void AcceptsAValidSelection() =>
        _validator.Validate(new NotificationSettingsDto(["TENANT_ADMIN"], ["TENANT_ADMIN", "DEPARTMENT_STAFF"])).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("SUPER_ADMIN")]
    [InlineData("TENANT_ADMIN,TENANT_ADMIN")]
    public void RejectsAnEmptyUnknownOrDuplicatedList(string rolesCsv)
    {
        var roles = rolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
        _validator.Validate(new NotificationSettingsDto(roles, ["TENANT_ADMIN"])).IsValid.Should().BeFalse();
        _validator.Validate(new NotificationSettingsDto(["TENANT_ADMIN"], roles)).IsValid.Should().BeFalse();
    }
}

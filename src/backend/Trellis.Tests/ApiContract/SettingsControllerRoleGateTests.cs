using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.ApiContract;

/// <summary>Any staff role may READ tenant settings (forms need the default currency); only a TENANT_ADMIN may change who is notified.</summary>
public sealed class SettingsControllerRoleGateTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SettingsControllerRoleGateTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient CreateClient(UserRole role, out IAsyncDisposable handle, ITenantSettingsService? settings = null)
    {
        var service = settings ?? Substitute.For<ITenantSettingsService>();
        var response = new TenantSettingsResponse("USD", "US", new NotificationSettingsDto(["TENANT_ADMIN"], ["TENANT_ADMIN"]), ["Rent too high"]);
        if (settings is null)
        {
            service.GetAsync().Returns(ServiceResult<TenantSettingsResponse>.Success(response));
            service.UpdateNotificationsAsync(Arg.Any<NotificationSettingsDto>(), Arg.Any<Guid>()).Returns(ServiceResult<TenantSettingsResponse>.Success(response));
        }

        var scoped = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped(_ => service)));
        handle = scoped;
        var client = scoped.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueAccessTokenFor(Guid.NewGuid(), role));
        return client;
    }

    [Theory]
    [InlineData(UserRole.TenantAdmin)]
    [InlineData(UserRole.DepartmentStaff)]
    public async Task Get_IsReadableByEitherStaffRole(UserRole role)
    {
        var client = CreateClient(role, out var handle);
        await using var _ = handle;

        (await client.GetAsync("/api/v1/settings")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.VendorUser)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Get_IsForbiddenToNonStaffRoles(UserRole role)
    {
        var client = CreateClient(role, out var handle);
        await using var _ = handle;

        (await client.GetAsync("/api/v1/settings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateNotifications_IsAllowedForATenantAdmin()
    {
        var client = CreateClient(UserRole.TenantAdmin, out var handle);
        await using var _ = handle;

        var response = await client.PutAsJsonAsync("/api/v1/settings/notifications", new NotificationSettingsDto(["TENANT_ADMIN"], ["DEPARTMENT_STAFF"]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateNotifications_IsForbiddenToDepartmentStaff()
    {
        var settings = Substitute.For<ITenantSettingsService>();
        var client = CreateClient(UserRole.DepartmentStaff, out var handle, settings);
        await using var _ = handle;

        var response = await client.PutAsJsonAsync("/api/v1/settings/notifications", new NotificationSettingsDto(["TENANT_ADMIN"], ["TENANT_ADMIN"]));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await settings.DidNotReceiveWithAnyArgs().UpdateNotificationsAsync(default!, default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("VENDOR_USER")]
    public async Task UpdateNotifications_RejectsAnEmptyOrUnknownRoleList_With400(string rolesCsv)
    {
        var roles = rolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var client = CreateClient(UserRole.TenantAdmin, out var handle);
        await using var _ = handle;

        var response = await client.PutAsJsonAsync("/api/v1/settings/notifications", new NotificationSettingsDto(roles, ["TENANT_ADMIN"]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

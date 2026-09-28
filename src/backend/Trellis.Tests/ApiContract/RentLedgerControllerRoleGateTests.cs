using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.RentLedger.Application.Dtos;
using Trellis.Api.Modules.RentLedger.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.ApiContract;

/// <summary>
/// Rent Collection's staff controller is TENANT_ADMIN + DEPARTMENT_STAFF and gated by
/// [RequiresModuleEntitlement("RENT_COLLECTION")] with no default entitlement (Leasing's own
/// precedent), so a tenant that was never granted the add-on gets the dedicated 403 rather than a
/// silent free ride. Exercises the real role attribute and the real entitlement filter; only the
/// services are substituted.
/// </summary>
public sealed class RentLedgerControllerRoleGateTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RentLedgerControllerRoleGateTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient CreateClient(Guid tenantId, UserRole role, bool entitled, out IAsyncDisposable handle)
    {
        var billingService = Substitute.For<IBillingService>();
        billingService.IsModuleEntitledAsync(tenantId, "RENT_COLLECTION").Returns(entitled);

        var scheduleService = Substitute.For<IRentScheduleService>();
        scheduleService.ListAsync(Arg.Any<string?>(), Arg.Any<int>()).Returns(
            ServiceResult<PagedResponse<RentScheduleResponse>>.Success(new PagedResponse<RentScheduleResponse>([], null, false, 0)));
        scheduleService.ListArrearsAsync().Returns(
            ServiceResult<IReadOnlyList<RentScheduleResponse>>.Success([]));

        var paymentService = Substitute.For<IRentPaymentService>();
        paymentService.ListAsync(Arg.Any<Guid>()).Returns(
            ServiceResult<IReadOnlyList<RentPaymentRecordResponse>>.Success([]));

        var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => billingService);
                services.AddScoped(_ => scheduleService);
                services.AddScoped(_ => paymentService);
            }));
        handle = scopedFactory;

        var client = scopedFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueAccessTokenFor(tenantId, role));
        return client;
    }

    [Theory]
    [InlineData(UserRole.TenantAdmin)]
    [InlineData(UserRole.DepartmentStaff)]
    public async Task ListRentSchedules_ForEitherStaffRole_WithTheModuleEntitled_Returns200(UserRole role)
    {
        var client = CreateClient(Guid.NewGuid(), role, entitled: true, out var handle);
        await using var _ = handle;

        (await client.GetAsync("/api/v1/rent-schedules")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.TenantAdmin)]
    [InlineData(UserRole.DepartmentStaff)]
    public async Task ListArrears_ForEitherStaffRole_WithTheModuleEntitled_Returns200(UserRole role)
    {
        var client = CreateClient(Guid.NewGuid(), role, entitled: true, out var handle);
        await using var _ = handle;

        (await client.GetAsync("/api/v1/rent-schedules/arrears")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.TenantAdmin)]
    [InlineData(UserRole.DepartmentStaff)]
    public async Task ListPaymentRecords_ForEitherStaffRole_WithTheModuleEntitled_Returns200(UserRole role)
    {
        var client = CreateClient(Guid.NewGuid(), role, entitled: true, out var handle);
        await using var _ = handle;

        (await client.GetAsync($"/api/v1/rent-schedules/{Guid.NewGuid()}/payment-records")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.VendorUser)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task ListRentSchedules_ForANonStaffRole_Returns403_BeforeEntitlementIsEvenConsulted(UserRole role)
    {
        var client = CreateClient(Guid.NewGuid(), role, entitled: true, out var handle);
        await using var _ = handle;

        (await client.GetAsync("/api/v1/rent-schedules")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/v1/rent-schedules")]
    [InlineData("/api/v1/rent-schedules/arrears")]
    public async Task RentEndpoints_ForATenantThatWasNeverGrantedTheAddOn_Return403WithTheModuleNotEntitledCode(string path)
    {
        var client = CreateClient(Guid.NewGuid(), UserRole.TenantAdmin, entitled: false, out var handle);
        await using var _ = handle;

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().Contain("TRL-E-BIL-10000");
    }

    [Fact]
    public async Task RentEndpoints_WithNoBearerToken_Return401()
    {
        await using var scopedFactory = _factory.WithWebHostBuilder(_ => { });
        var client = scopedFactory.CreateClient();

        (await client.GetAsync("/api/v1/rent-schedules")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

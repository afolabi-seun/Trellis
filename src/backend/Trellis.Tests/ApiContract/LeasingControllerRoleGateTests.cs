using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.ApiContract;

/// <summary>
/// Leasing's staff controllers are TENANT_ADMIN + DEPARTMENT_STAFF (user-confirmed) and gated by
/// [RequiresModuleEntitlement("LEASING")] from day one — with no default entitlement, so a tenant
/// that was never granted the module gets the dedicated 403, not a silent free ride. Exercises the
/// real role attribute and the real entitlement filter; only the services are substituted.
/// </summary>
public sealed class LeasingControllerRoleGateTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public LeasingControllerRoleGateTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient CreateClient(Guid tenantId, UserRole role, bool entitled, out WebApplicationFactoryHandle handle)
    {
        var billingService = Substitute.For<IBillingService>();
        billingService.IsModuleEntitledAsync(tenantId, "LEASING").Returns(entitled);

        var listingService = Substitute.For<IVacancyListingService>();
        listingService.ListForTenantAsync(Arg.Any<string?>(), Arg.Any<int>()).Returns(
            ServiceResult<PagedResponse<VacancyListingResponse>>.Success(new PagedResponse<VacancyListingResponse>([], null, false, 0)));

        var applicationService = Substitute.For<IApplicationService>();
        applicationService.ListByListingAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<int>()).Returns(
            ServiceResult<PagedResponse<ApplicationResponse>>.Success(new PagedResponse<ApplicationResponse>([], null, false, 0)));
        applicationService.EndLeaseAsync(Arg.Any<Guid>(), Arg.Any<EndLeaseRequest>(), Arg.Any<Guid>()).Returns(
            ServiceResult<ApplicationResponse>.Success(
                new ApplicationResponse(Guid.NewGuid(), Guid.NewGuid(), "a@example.com", "Ada", "LEASE_ENDED", null, DateTimeOffset.UtcNow, null, null)));

        var screeningReportService = Substitute.For<IScreeningReportService>();
        screeningReportService.ViewAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>()).Returns(
            ServiceResult<ScreeningReportResponse>.Success(new ScreeningReportResponse("NOOP", "PASS", "{}", DateTimeOffset.UtcNow)));

        var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => billingService);
                services.AddScoped(_ => listingService);
                services.AddScoped(_ => applicationService);
                services.AddScoped(_ => screeningReportService);
            }));
        handle = new WebApplicationFactoryHandle(scopedFactory);

        var client = scopedFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueAccessTokenFor(tenantId, role));
        return client;
    }

    /// <summary>Keeps the derived factory alive for the request's duration and disposes it after.</summary>
    private sealed class WebApplicationFactoryHandle(IAsyncDisposable factory) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => factory.DisposeAsync();
    }

    [Theory]
    [InlineData(UserRole.TenantAdmin)]
    [InlineData(UserRole.DepartmentStaff)]
    public async Task ListVacancyListings_ForEitherStaffRole_WithTheModuleEntitled_Returns200(UserRole role)
    {
        var client = CreateClient(Guid.NewGuid(), role, entitled: true, out var handle);
        await using var _ = handle;

        var response = await client.GetAsync("/api/v1/vacancy-listings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.TenantAdmin)]
    [InlineData(UserRole.DepartmentStaff)]
    public async Task ListApplications_ForEitherStaffRole_WithTheModuleEntitled_Returns200(UserRole role)
    {
        var client = CreateClient(Guid.NewGuid(), role, entitled: true, out var handle);
        await using var _ = handle;

        var response = await client.GetAsync($"/api/v1/applications?vacancyListingId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.VendorUser)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task ListVacancyListings_ForANonStaffRole_Returns403_BeforeEntitlementIsEvenConsulted(UserRole role)
    {
        var client = CreateClient(Guid.NewGuid(), role, entitled: true, out var handle);
        await using var _ = handle;

        var response = await client.GetAsync("/api/v1/vacancy-listings");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListVacancyListings_ForATenantThatWasNeverGrantedLeasing_Returns403WithTheModuleNotEntitledCode()
    {
        var client = CreateClient(Guid.NewGuid(), UserRole.TenantAdmin, entitled: false, out var handle);
        await using var _ = handle;

        var response = await client.GetAsync("/api/v1/vacancy-listings");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().Contain("TRL-E-BIL-10000");
    }

    [Fact]
    public async Task ListApplications_ForATenantThatWasNeverGrantedLeasing_Returns403WithTheModuleNotEntitledCode()
    {
        var client = CreateClient(Guid.NewGuid(), UserRole.DepartmentStaff, entitled: false, out var handle);
        await using var _ = handle;

        var response = await client.GetAsync($"/api/v1/applications?vacancyListingId={Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().Contain("TRL-E-BIL-10000");
    }

    [Theory]
    [InlineData(UserRole.TenantAdmin)]
    [InlineData(UserRole.DepartmentStaff)]
    public async Task EndLease_ForEitherStaffRole_WithTheModuleEntitled_Returns200(UserRole role)
    {
        var client = CreateClient(Guid.NewGuid(), role, entitled: true, out var handle);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync(
            $"/api/v1/applications/{Guid.NewGuid()}/end-lease", new EndLeaseRequest(new DateOnly(2026, 6, 30), "NON_RENEWAL", "LESSEE", null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ViewScreeningReport_ForTenantAdmin_Returns200_WithANoStoreCacheHeader()
    {
        var client = CreateClient(Guid.NewGuid(), UserRole.TenantAdmin, entitled: true, out var handle);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync(
            $"/api/v1/applications/{Guid.NewGuid()}/screening-report", new ViewScreeningReportRequest("correct-password"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task ViewScreeningReport_ForDepartmentStaff_Returns403_UnlikeEveryOtherStaffEndpointOnThisController()
    {
        var client = CreateClient(Guid.NewGuid(), UserRole.DepartmentStaff, entitled: true, out var handle);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync(
            $"/api/v1/applications/{Guid.NewGuid()}/screening-report", new ViewScreeningReportRequest("correct-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListVacancyListings_WithNoBearerToken_Returns401()
    {
        await using var scopedFactory = _factory.WithWebHostBuilder(_ => { });
        var client = scopedFactory.CreateClient();

        var response = await client.GetAsync("/api/v1/vacancy-listings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

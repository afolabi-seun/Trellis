using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Reporting.Application.Dtos;
using Trellis.Api.Modules.Reporting.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.ApiContract;

/// <summary>
/// Reporting is Core tier — no [RequiresModuleEntitlement] gate — but its two action groups carry
/// different role requirements: the dashboard is TENANT_ADMIN+DEPARTMENT_STAFF, every Owner-report
/// action is TENANT_ADMIN only. Mirrors ModuleEntitlementGateContractTests's own shape.
/// </summary>
public sealed class ReportingControllerRoleGateTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReportingControllerRoleGateTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetDashboard_ForDepartmentStaff_Returns200()
    {
        var dashboardService = Substitute.For<IDashboardService>();
        dashboardService.GetSummaryAsync().Returns(ServiceResult<DashboardSummaryResponse>.Success(new DashboardSummaryResponse(0, 0, 0, 0, null)));

        await using var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped(_ => dashboardService)));
        var client = scopedFactory.CreateClient();
        var token = _factory.IssueAccessTokenFor(Guid.NewGuid(), UserRole.DepartmentStaff);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/reporting/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListOwnerReports_ForDepartmentStaff_Returns403()
    {
        await using var scopedFactory = _factory.WithWebHostBuilder(_ => { });
        var client = scopedFactory.CreateClient();
        var token = _factory.IssueAccessTokenFor(Guid.NewGuid(), UserRole.DepartmentStaff);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/reporting/owner-reports");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListOwnerReports_ForTenantAdmin_RemainsFullyReachable()
    {
        var ownerReportService = Substitute.For<IOwnerReportService>();
        ownerReportService.ListByOwnerAsync(Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<int>()).Returns(
            ServiceResult<Trellis.Api.Core.Dtos.PagedResponse<OwnerReportResponse>>.Success(
                new Trellis.Api.Core.Dtos.PagedResponse<OwnerReportResponse>([], null, false, 0)));

        await using var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped(_ => ownerReportService)));
        var client = scopedFactory.CreateClient();
        var token = _factory.IssueAccessTokenFor(Guid.NewGuid(), UserRole.TenantAdmin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/reporting/owner-reports");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetDashboard_WithNoBearerToken_Returns401()
    {
        await using var scopedFactory = _factory.WithWebHostBuilder(_ => { });
        var client = scopedFactory.CreateClient();

        var response = await client.GetAsync("/api/v1/reporting/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

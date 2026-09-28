using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Auth.Domain.Entities;
using Trellis.Api.Modules.Billing.Application.Interfaces;
using Trellis.Api.Modules.Inspection.Application.Dtos;
using Trellis.Api.Modules.Inspection.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.ApiContract;

/// <summary>
/// billing_design.md's Testing section, third of its 3 explicitly required tests: "a test proving
/// a disabled module's endpoints return the dedicated 403 for a tenant lacking that entitlement
/// while remaining fully reachable for a tenant that has it (BDR-BIL-009)." Exercises the real
/// RequiresModuleEntitlementFilter against a real, retrofitted controller
/// (InspectionsController); IBillingService and IInspectionService are substituted so no request
/// touches Postgres.
/// </summary>
public sealed class ModuleEntitlementGateContractTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ModuleEntitlementGateContractTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetInspections_ForATenantWithTheModuleDisabled_Returns403WithTheModuleNotEntitledCode()
    {
        var tenantId = Guid.NewGuid();
        var billingService = Substitute.For<IBillingService>();
        billingService.IsModuleEntitledAsync(tenantId, "INSPECTION").Returns(false);

        await using var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped(_ => billingService)));
        var client = scopedFactory.CreateClient();
        var token = _factory.IssueAccessTokenFor(tenantId, UserRole.TenantAdmin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/inspections");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        body.Should().Contain("TRL-E-BIL-10000");
    }

    [Fact]
    public async Task GetInspections_ForATenantWithTheModuleActive_RemainsFullyReachable()
    {
        var tenantId = Guid.NewGuid();
        var billingService = Substitute.For<IBillingService>();
        billingService.IsModuleEntitledAsync(tenantId, "INSPECTION").Returns(true);
        var inspectionService = Substitute.For<IInspectionService>();
        inspectionService.ListForTenantAsync(Arg.Any<string?>(), Arg.Any<int>()).Returns(
            ServiceResult<PagedResponse<InspectionReportResponse>>.Success(
                new PagedResponse<InspectionReportResponse>([], null, false, 0)));

        await using var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => billingService);
                services.AddScoped(_ => inspectionService);
            }));
        var client = scopedFactory.CreateClient();
        var token = _factory.IssueAccessTokenFor(tenantId, UserRole.TenantAdmin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/inspections");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetInspections_WithNoBearerToken_Returns401_NeverReachingTheEntitlementFilter()
    {
        var billingService = Substitute.For<IBillingService>();
        await using var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped(_ => billingService)));
        var client = scopedFactory.CreateClient();

        var response = await client.GetAsync("/api/v1/inspections");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await billingService.DidNotReceiveWithAnyArgs().IsModuleEntitledAsync(default, default!);
    }
}

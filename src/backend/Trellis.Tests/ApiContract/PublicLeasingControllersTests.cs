using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Trellis.Api.Core.Dtos;
using Trellis.Api.Core.Results;
using Trellis.Api.Modules.Leasing.Application.Dtos;
using Trellis.Api.Modules.Leasing.Application.Interfaces;
using Trellis.Api.Modules.Leasing.Domain.ValueObjects;
using Trellis.Api.Modules.Tenant.Application.Dtos;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Xunit;

namespace Trellis.Tests.ApiContract;

/// <summary>
/// The public leasing surface — reachable with no Authorization header at all (authenticated by
/// token validity, not a session). Every failure mode is a 404, never a 403: no existence
/// disclosure. Only the services are substituted; the real [AllowAnonymous] pipeline runs.
/// </summary>
public sealed class PublicLeasingControllersTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly Guid AcmeTenantId = Guid.NewGuid();

    public PublicLeasingControllersTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient CreateClient(
        out IAsyncDisposable handle, IApplicationLinkService? linkService = null, IApplicationService? applicationService = null,
        IVacancyListingService? listingService = null, ITenantService? tenantService = null)
    {
        // Public URLs identify a tenant by slug (never its GUID). By default only "acme" resolves.
        var tenants = tenantService ?? Substitute.For<ITenantService>();
        if (tenantService is null)
        {
            tenants.ResolveSlugAsync("acme").Returns(ServiceResult<TenantPublicInfo>.Success(new TenantPublicInfo(AcmeTenantId, "Acme PM", "acme")));
            tenants.ResolveSlugAsync(Arg.Is<string>(x => x != "acme")).Returns(ServiceResult<TenantPublicInfo>.NotFound());
        }

        var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => linkService ?? Substitute.For<IApplicationLinkService>());
                services.AddScoped(_ => applicationService ?? Substitute.For<IApplicationService>());
                services.AddScoped(_ => listingService ?? Substitute.For<IVacancyListingService>());
                services.AddScoped(_ => tenants);
            }));
        handle = scopedFactory;
        return scopedFactory.CreateClient();
    }

    [Fact]
    public async Task ResolveLink_WithNoAuthorizationHeader_ForAnUnknownToken_Returns404_Not403()
    {
        var linkService = Substitute.For<IApplicationLinkService>();
        linkService.ResolveAsync(Arg.Any<string>()).Returns(ServiceResult<ApplicationLinkContext>.NotFound());
        var client = CreateClient(out var handle, linkService);
        await using var _ = handle;

        var response = await client.GetAsync("/api/v1/public/application-links/bogus-token");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResolveLink_ForAValidToken_Returns200WithTheServerTrustedEmail_AndNoTenantId()
    {
        var linkService = Substitute.For<IApplicationLinkService>();
        linkService.ResolveAsync("good").Returns(ServiceResult<ApplicationLinkContext>.Success(
            new ApplicationLinkContext(Guid.NewGuid(), Guid.NewGuid(), "verified@example.com", "Unit 4B", 1500m, "USD")));
        var client = CreateClient(out var handle, linkService);
        await using var _ = handle;

        var response = await client.GetAsync("/api/v1/public/application-links/good");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("verified@example.com").And.Contain("Unit 4B");
        body.Should().NotContain("tenantId", "the anonymous response must not leak internal tenant identity");
    }

    [Fact]
    public async Task SubmitApplication_ForAnInvalidOrConsumedToken_Returns404()
    {
        var applicationService = Substitute.For<IApplicationService>();
        applicationService.SubmitAsync(Arg.Any<string>(), Arg.Any<ApplicationSubmitRequest>()).Returns(ServiceResult<ApplicationSubmitResponse>.NotFound());
        var client = CreateClient(out var handle, applicationService: applicationService);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync("/api/v1/public/application-links/used/applications",
            new ApplicationSubmitRequest("Ada", "+27821234567", new DateOnly(1990, 1, 1), "1 Main Rd", null, null, true));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitApplication_WithoutConsent_IsRejectedByTheValidatorBeforeAnyServiceRuns()
    {
        var applicationService = Substitute.For<IApplicationService>();
        var client = CreateClient(out var handle, applicationService: applicationService);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync("/api/v1/public/application-links/tok/applications",
            new ApplicationSubmitRequest("Ada", "+27821234567", new DateOnly(1990, 1, 1), "1 Main Rd", null, null, false));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await applicationService.DidNotReceiveWithAnyArgs().SubmitAsync(default!, default!);
    }

    [Fact]
    public async Task IssueApplicationLink_ReturnsGenericSuccess_AndNeverTheRawToken()
    {
        var listingService = Substitute.For<IVacancyListingService>();
        var linkService = Substitute.For<IApplicationLinkService>();
        linkService.IssueApplicationLinkAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), "applicant@example.com")
            .Returns(ServiceResult<string>.Success("SECRET-RAW-TOKEN"));
        var client = CreateClient(out var handle, linkService, listingService: listingService);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync(
            $"/api/v1/public/tenants/acme/vacancy-listings/{Guid.NewGuid()}/application-link",
            new ApplicationLinkIssueRequest("applicant@example.com"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContain("SECRET-RAW-TOKEN", "the token is delivered only by email — returning it would let a caller obtain a link for an address they don't control (BDR-LSE-008)");
    }

    [Fact]
    public async Task IssueApplicationLink_ForAnUnknownListing_Returns404()
    {
        var linkService = Substitute.For<IApplicationLinkService>();
        linkService.IssueApplicationLinkAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>()).Returns(ServiceResult<string>.NotFound());
        var client = CreateClient(out var handle, linkService);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync(
            $"/api/v1/public/tenants/acme/vacancy-listings/{Guid.NewGuid()}/application-link",
            new ApplicationLinkIssueRequest("applicant@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IssueApplicationLink_WithAMalformedEmail_Returns400()
    {
        var client = CreateClient(out var handle);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync(
            $"/api/v1/public/tenants/acme/vacancy-listings/{Guid.NewGuid()}/application-link",
            new ApplicationLinkIssueRequest("not-an-email"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BrowsePublicListings_WithNoAuthorizationHeader_IsReachable()
    {
        var listingService = Substitute.For<IVacancyListingService>();
        listingService.BrowsePublicAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<int>()).Returns(
            ServiceResult<PagedResponse<PublicVacancyListingResponse>>.Success(new PagedResponse<PublicVacancyListingResponse>([], null, false, 0)));
        var client = CreateClient(out var handle, listingService: listingService);
        await using var _ = handle;

        var response = await client.GetAsync($"/api/v1/public/tenants/acme/vacancy-listings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BrowsePublicListings_ResolvesTheSlugToTheTenant_BeforeCallingTheService()
    {
        var listingService = Substitute.For<IVacancyListingService>();
        listingService.BrowsePublicAsync(AcmeTenantId, Arg.Any<string?>(), Arg.Any<int>()).Returns(
            ServiceResult<PagedResponse<PublicVacancyListingResponse>>.Success(new PagedResponse<PublicVacancyListingResponse>([], null, false, 0)));
        var client = CreateClient(out var handle, listingService: listingService);
        await using var _ = handle;

        await client.GetAsync("/api/v1/public/tenants/acme/vacancy-listings");

        await listingService.Received(1).BrowsePublicAsync(AcmeTenantId, Arg.Any<string?>(), Arg.Any<int>());
    }

    [Theory]
    [InlineData("/api/v1/public/tenants/nope/vacancy-listings")]
    [InlineData("/api/v1/public/tenants/nope/vacancy-listings/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/v1/public/tenants/nope/vacancy-listings/00000000-0000-0000-0000-000000000001/photos/00000000-0000-0000-0000-000000000002")]
    public async Task PublicListings_ForAnUnknownSlug_Return404_AndNeverReachTheListingService(string path)
    {
        var listingService = Substitute.For<IVacancyListingService>();
        var client = CreateClient(out var handle, listingService: listingService);
        await using var _ = handle;

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await listingService.DidNotReceiveWithAnyArgs().BrowsePublicAsync(default, default, default);
        await listingService.DidNotReceiveWithAnyArgs().GetPublicByIdAsync(default, default);
    }

    [Fact]
    public async Task IssueApplicationLink_ForAnUnknownSlug_Returns404_AndSendsNothing()
    {
        var linkService = Substitute.For<IApplicationLinkService>();
        var client = CreateClient(out var handle, linkService);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync(
            "/api/v1/public/tenants/nope/vacancy-listings/00000000-0000-0000-0000-000000000001/application-link",
            new ApplicationLinkIssueRequest("applicant@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await linkService.DidNotReceiveWithAnyArgs().IssueApplicationLinkAsync(default, default, default!);
    }

    [Fact]
    public async Task PublicTenantProfile_ReturnsTheNameAndSlug_ButNeverTheTenantId()
    {
        var tenants = Substitute.For<ITenantService>();
        tenants.ResolveSlugAsync("acme").Returns(ServiceResult<TenantPublicInfo>.Success(new TenantPublicInfo(AcmeTenantId, "Acme PM", "acme")));
        var client = CreateClient(out var handle, tenantService: tenants);
        await using var _ = handle;

        var response = await client.GetAsync("/api/v1/public/tenants/acme");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("Acme PM").And.Contain("acme");
        body.Should().NotContain(AcmeTenantId.ToString(), "an anonymous response must never disclose the tenant's internal id (BDR-SEC-004)");
    }

    [Fact]
    public async Task PublicTenantProfile_ForAnUnknownOrInactiveSlug_Returns404()
    {
        var tenants = Substitute.For<ITenantService>();
        tenants.ResolveSlugAsync(Arg.Any<string>()).Returns(ServiceResult<TenantPublicInfo>.NotFound());
        var client = CreateClient(out var handle, tenantService: tenants);
        await using var _ = handle;

        (await client.GetAsync("/api/v1/public/tenants/ghost")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

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

    public PublicLeasingControllersTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient CreateClient(
        out IAsyncDisposable handle, IApplicationLinkService? linkService = null, IApplicationService? applicationService = null, IVacancyListingService? listingService = null)
    {
        var scopedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => linkService ?? Substitute.For<IApplicationLinkService>());
                services.AddScoped(_ => applicationService ?? Substitute.For<IApplicationService>());
                services.AddScoped(_ => listingService ?? Substitute.For<IVacancyListingService>());
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
            $"/api/v1/public/tenants/{Guid.NewGuid()}/vacancy-listings/{Guid.NewGuid()}/application-link",
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
            $"/api/v1/public/tenants/{Guid.NewGuid()}/vacancy-listings/{Guid.NewGuid()}/application-link",
            new ApplicationLinkIssueRequest("applicant@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IssueApplicationLink_WithAMalformedEmail_Returns400()
    {
        var client = CreateClient(out var handle);
        await using var _ = handle;

        var response = await client.PostAsJsonAsync(
            $"/api/v1/public/tenants/{Guid.NewGuid()}/vacancy-listings/{Guid.NewGuid()}/application-link",
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

        var response = await client.GetAsync($"/api/v1/public/tenants/{Guid.NewGuid()}/vacancy-listings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

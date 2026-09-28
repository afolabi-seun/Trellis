using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Trellis.Api.Core.Configuration;
using NSubstitute;
using Trellis.Api.Core.Cors;
using Trellis.Api.Modules.Tenant.Application.Interfaces;
using Trellis.Api.Modules.Tenant.Infrastructure;
using Xunit;

namespace Trellis.Tests.Tenant;

/// <summary>Custom-domain CORS: a verified tenant domain gets credentialed CORS, nothing else does — and the static origins keep working exactly as before.</summary>
public sealed class CustomDomainCorsTests
{
    private static readonly string[] StaticOrigins = ["http://localhost:5173", "https://app.trellis-platform.com"];

    private static DefaultHttpContext ContextWith(string? origin, ICustomDomainOriginProvider provider)
    {
        var services = new ServiceCollection().AddSingleton(provider).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        return context;
    }

    private static Task<Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicy?> PolicyFor(string? origin, ICustomDomainOriginProvider provider) =>
        new TrellisCorsPolicyProvider(StaticOrigins).GetPolicyAsync(ContextWith(origin, provider), "trellis-frontend");

    [Fact]
    public async Task AStaticOrigin_IsAllowed_WithoutConsultingTheDomainTable()
    {
        var provider = Substitute.For<ICustomDomainOriginProvider>();

        var policy = await PolicyFor("https://app.trellis-platform.com", provider);

        policy!.Origins.Should().Contain("https://app.trellis-platform.com");
        await provider.DidNotReceiveWithAnyArgs().IsAllowedOriginAsync(default!);
    }

    [Fact]
    public async Task AVerifiedTenantDomain_IsEchoedBack_AlongsideTheStaticOrigins()
    {
        var provider = Substitute.For<ICustomDomainOriginProvider>();
        provider.IsAllowedOriginAsync("https://listings.acme.com").Returns(true);

        var policy = await PolicyFor("https://listings.acme.com", provider);

        policy!.Origins.Should().Contain("https://listings.acme.com").And.Contain("http://localhost:5173");
        policy.SupportsCredentials.Should().BeTrue();
    }

    [Fact]
    public async Task AnUnregisteredOrigin_IsNeverAllowed()
    {
        var provider = Substitute.For<ICustomDomainOriginProvider>();
        provider.IsAllowedOriginAsync(Arg.Any<string>()).Returns(false);

        var policy = await PolicyFor("https://evil.example", provider);

        policy!.Origins.Should().NotContain("https://evil.example");
        policy.Origins.Should().BeEquivalentTo(StaticOrigins);
    }

    [Fact]
    public async Task ARequestWithNoOrigin_GetsJustTheStaticPolicy()
    {
        var provider = Substitute.For<ICustomDomainOriginProvider>();

        var policy = await PolicyFor(null, provider);

        policy!.Origins.Should().BeEquivalentTo(StaticOrigins);
        await provider.DidNotReceiveWithAnyArgs().IsAllowedOriginAsync(default!);
    }

    // ---- The provider behind the interface ----

    [Fact]
    public async Task OriginProvider_AllowsOnlyHttpsOriginsWhoseHostIsAVerifiedDomain()
    {
        var repository = Substitute.For<ITenantDomainRepository>();
        repository.ListAllVerifiedHostsAsync().Returns(["listings.acme.com"]);
        var provider = new TenantDomainOriginProvider(new MemoryCache(new MemoryCacheOptions()), repository, Options.Create(new AppOptions()));

        (await provider.IsAllowedOriginAsync("https://listings.acme.com")).Should().BeTrue();
        (await provider.IsAllowedOriginAsync("https://LISTINGS.acme.com")).Should().BeTrue();
        (await provider.IsAllowedOriginAsync("http://listings.acme.com")).Should().BeFalse("only https origins of custom domains are accepted");
        (await provider.IsAllowedOriginAsync("https://other.acme.com")).Should().BeFalse();
        (await provider.IsAllowedOriginAsync("not-an-origin")).Should().BeFalse();
    }

    [Fact]
    public async Task OriginProvider_AcceptsHttpOriginsOnlyWhenTheDevTestFlagIsExplicitlyOn()
    {
        var repository = Substitute.For<ITenantDomainRepository>();
        repository.ListAllVerifiedHostsAsync().Returns(["acme-e2e.test"]);
        var off = new TenantDomainOriginProvider(new MemoryCache(new MemoryCacheOptions()), repository, Options.Create(new AppOptions()));
        var on = new TenantDomainOriginProvider(
            new MemoryCache(new MemoryCacheOptions()), repository, Options.Create(new AppOptions { AllowInsecureCustomDomainOrigins = true }));

        (await off.IsAllowedOriginAsync("http://acme-e2e.test:4173")).Should().BeFalse();
        (await on.IsAllowedOriginAsync("http://acme-e2e.test:4173")).Should().BeTrue();
        (await on.IsAllowedOriginAsync("http://not-registered.test:4173")).Should().BeFalse();
    }

    [Fact]
    public async Task OriginProvider_CachesTheSnapshot_UntilItIsInvalidated()
    {
        var repository = Substitute.For<ITenantDomainRepository>();
        repository.ListAllVerifiedHostsAsync().Returns(["listings.acme.com"]);
        var provider = new TenantDomainOriginProvider(new MemoryCache(new MemoryCacheOptions()), repository, Options.Create(new AppOptions()));

        await provider.IsAllowedOriginAsync("https://listings.acme.com");
        await provider.IsAllowedOriginAsync("https://listings.acme.com");
        await repository.Received(1).ListAllVerifiedHostsAsync();

        repository.ListAllVerifiedHostsAsync().Returns(["listings.acme.com", "new.acme.com"]);
        provider.Invalidate();

        (await provider.IsAllowedOriginAsync("https://new.acme.com")).Should().BeTrue("a newly verified domain works at once, without waiting out the cache");
    }
}

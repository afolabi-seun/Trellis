using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Trellis.Api.Modules.Auth.Application.Interfaces;
using Trellis.Api.Modules.Auth.Domain.Entities;

namespace Trellis.Tests.ApiContract;

/// <summary>
/// testing_strategy.md's API/Contract layer — the first thing in this repo to use it. Boots the
/// real Trellis.Api Program (real HTTP pipeline, real JWT auth/authorization, real
/// RequiresModuleEntitlement filter), but keeps individual tests DB-free by having each one
/// substitute the specific Service(s) it's exercising via WithWebHostBuilder/ConfigureTestServices
/// — the Postgres-backed proofs live in the Testcontainers isolation layer instead
/// (testing_strategy.md's own pyramid: this layer proves the HTTP/filter/auth contract, not data
/// correctness). Secrets are set as real process environment variables (not IConfiguration) since
/// AddAuthModule's JWT bearer setup and ISecretProvider both read
/// Environment.GetEnvironmentVariable directly, before this factory's own ConfigureWebHost hook
/// ever runs — the same dev-only values already committed in .env.example, safe to reuse here
/// since nothing they sign protects real data.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string TestJwtSigningKeyBase64 =
        "MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQC3Nzx2vQH0uyqsPFByqR54/7XVKUogrR1DnQCgNarv5oXE8744cjDvTVH4NCzSE14UGSdbC3n06GPgJOwWt/Xh06Abq996ijwLJTfpkdqE13+XALvz+jC2NCHDd60a1x2PJYalLycvztwHHPCBTa323nfVIBpfodqAsHOSmeUQnF0TvYYC/ngFQufDNEI1eX/Q/Ca0hR9SQ2XXa1+a9z/YTPC92hfFLxNqY94OauPp5EijrZ4x0Uhx3sFvb7186Y9rr4GQ+PaeA7M/w0fhy5j6bgleP3/Q5DjvTtToB+FTE9B0VRy53lDHuRH8J7xfLIh9V1BMp31RAo/7pe5sk+nBAgMBAAECggEAOSHn4G0DEaJV5byvJ3o2TuwKcyy7eWISWJW6brsdkU600xxg6nAtc0oMsBXxDhdNBH5th9YV0zmuf2R0HIeEDMY1zLs1Zpx5qc4S1+qMFlWu61MmySdxDjhGeRI2MEL5bDx69sKz+RQMB8nN87xZ7bwk6RhoMP8jSHL9ry7/P8TDHBMGSUS9BozPRvhVdgpAb8Aav6Ah9Xnv7TxYfgg0HjAepMz+4PZ1yluMn9mxQ2g6cWt500DCcVBfB3PGvYPEGVljy/vCM+1rAdt9TSrRRE61kIouyV3wObHrYmvvw1JQbjfZs3oJR9FI6N1Tk2FISuMJ70sQ9aYW1sIzqPZ/OwKBgQDtYldgqrmqPr125SZGT7Qu2/UeSrXefRwD2h5zwqDK48hphGx+E+0ClMS9rVKHh61sgwDf8Qiuinj5CDcaAm55DySFhLtwpbOmnTzLp6OSehV3n5XlYX/QKedgncqf7cw7u8bgl6htcOT1+px4OeBwaSgcBOyz4tg/ULIYTUeFswKBgQDFlWzn3eb9BbBQu6+9rFcB2gHPLSamfBQUmqs3/VUlXMlDia2XHMnZ24LXdPbzdAot6XiV6IOIhbYPwcjntQyDeRqM2C3WceApmv8zwkALxgNTzckI4bA86FOUbdLG6EAmRf8LVQcpG6Ic5L+nOOjP8TfuZXfA+ODIuYU6PTXAuwKBgFRe2Il+BIFSzUo9xa3F1Fe3S9aZXSyXsOZZlusz6KxT6z49l8Xz1RBOyeJUgPm0qlnf8eamJ5zJkJeKaPMwnA0g6a0e8PPnoQN97UbA7W05QPoEWWVuktvsmNUJICRZg3ZTlirrHCzRrD8U9JnykCfD6EKVKYACBzPxvWrPxLTrAoGAQNM7vf9CD/XT2Uka6cInccYTHeQUcD75X6E3V9FYJEADEJOVTqT1XPL2RLZtZ4bE6DdXhueYcCxehWDxB4SRLHmJSniK4XbPMxdVb6fl13cKFG7J7I4Pgow/D83Qf84nbec9PHyTMtuMHqq2bqjvSXo8/Yxw8/Ez3udvXKJEho0CgYBXFluO3Y+TJg6Wi97e5JitynKBMq1jzv2f/AUPSXvxr5npghGYPWU9+MNL3YAJOZoUL4ICGtv0h0/5hcdwtPyaKJHn+cxxH70FSJkX+L9L2iJeoug7u5SNiVkQEhJfBFJQtPN1sZ+z3Z1z8G+A9pDqBHs7FtwR93YiKPfq1u7n3A==";

    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("JwtSigningKeyBase64", TestJwtSigningKeyBase64);
        Environment.SetEnvironmentVariable("MfaEncryptionKeyBase64", "LFSf47+F6M2r1CBs3aEJRxCaUzKTIQfqjvqSbaHhJsI=");
        Environment.SetEnvironmentVariable("PaystackSecretKey", "sk_test_apicontract_placeholder");
        Environment.SetEnvironmentVariable("AllowedOrigins", "http://localhost");

        // Never actually connected to — every test using this factory substitutes whichever
        // Service(s) its endpoint touches, so no request in this layer executes a real query.
        // Still needs to be a syntactically valid Npgsql connection string, since UseNpgsql
        // parses it immediately at DbContext registration time.
        const string unusedConnectionString = "Host=localhost;Port=5432;Database=trellis_apicontract_unused;Username=none;Password=none";
        Environment.SetEnvironmentVariable("ConnectionStrings__Trellis", unusedConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__TrellisMigrations", unusedConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");

    /// <summary>Issues a real, real-key-signed access token — the same IJwtTokenService a real login uses, just handed a freshly-constructed AppUser instead of one loaded from Postgres.</summary>
    public string IssueAccessTokenFor(Guid tenantId, UserRole role, string email = "contract-test@example.com")
    {
        using var scope = Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = AppUser.Create(tenantId, email, role);
        return jwtTokenService.IssueAccessToken(user, Guid.NewGuid());
    }
}

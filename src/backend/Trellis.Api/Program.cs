using System.Globalization;
using Serilog;
using Serilog.Events;
using Trellis.Api.Configuration;
using Trellis.Api.Core;
using Trellis.Api.Core.Logging;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Modules.Auth;
using Trellis.Api.Modules.Notification;
using Trellis.Api.Modules.PropertyAsset;
using Trellis.Api.Modules.Tenant;

// devops_environments.md, "Configuration & Secrets Per Environment": .env exists only in Local
// (gitignored — never committed). Development/Staging/Production have no file on disk; the
// hosting platform sets real environment variables directly, so this is a no-op there.
if (File.Exists(".env"))
{
    DotNetEnv.Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

// security_guide.md, "Log PII Redaction" (BDR-NFR-011): applied globally, not opt-in per call site.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .MinimumLevel.Is(context.HostingEnvironment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
    .Enrich.With<RedactingEnricher>()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services.AddControllers();
builder.Services.AddTrellisSwaggerGen();

builder.Services.AddTrellisCore(builder.Configuration);

// Tenant → Notification → Auth → PropertyAsset, per data_model.md's migration dependency order
// and roadmap.md's Phase 1 → Phase 2 sequencing.
builder.Services.AddTenantModule(builder.Configuration);
builder.Services.AddNotificationModule(builder.Configuration);
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddPropertyAssetModule(builder.Configuration);

var app = builder.Build();

// error_handling.md: catches whatever a Service method didn't. First in the pipeline so it
// wraps every other middleware below.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseTrellisSwagger();
}

app.UseHttpsRedirection();

app.UseCors(CoreModuleExtensions.CorsPolicyName);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// multi_tenant_design.md: needs UseAuthentication() to have populated context.User first, so
// the tenant_id/vendor_id/role claims TenantScopeMiddleware reads are actually there.
app.UseMiddleware<TenantScopeMiddleware>();

app.MapControllers();

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> (testing_strategy.md's API/contract test layer) can boot this app in-process.</summary>
public partial class Program;

using System.Globalization;
using Serilog;
using Serilog.Events;
using Trellis.Api.Configuration;
using Trellis.Api.Core;
using Trellis.Api.Core.Logging;
using Trellis.Api.Core.Middleware;
using Trellis.Api.Core.Validation;
using Trellis.Api.Modules.Ai;
using Trellis.Api.Modules.Auth;
using Trellis.Api.Modules.Billing;
using Trellis.Api.Modules.Document;
using Trellis.Api.Modules.Inspection;
using Trellis.Api.Modules.Notification;
using Trellis.Api.Modules.PropertyAsset;
using Trellis.Api.Modules.Reporting;
using Trellis.Api.Modules.Tenant;
using Trellis.Api.Modules.Vendor;
using Trellis.Api.Modules.Workflow;

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

// ValidationActionFilter's own doc comment explains why this registration needed adding at all.
builder.Services.AddControllers(options => options.Filters.Add<ValidationActionFilter>());
builder.Services.AddTrellisSwaggerGen();

builder.Services.AddTrellisCore(builder.Configuration);

// Tenant → Notification → Auth → PropertyAsset → Vendor → Document → Workflow → Inspection → Ai →
// Billing → Reporting, per data_model.md's migration dependency order and roadmap.md's Phase 1 →
// Phase 8 sequencing. Vendor registers before Document/Workflow/Inspection since all three now
// depend on its published IVendorAssignmentReader (Workflow's vendor-safe projection, Document's
// evidence upload, Inspection's own vendor-portal reads).
//
// Ai, Billing, and now Reporting all register last, matching the fixed migration order — this is
// a convention, not a requirement. DocumentService has a compile-time constructor dependency on
// Ai's own IExtractionService (Modules/Ai/Application/Interfaces, not Core), and Inspection's/
// Ai's own controllers now depend on Billing's RequiresModuleEntitlementAttribute
// (Modules/Billing/Presentation/Filters), even though Billing's DbContext migrates after both in
// the schema chain above. This is safe: ASP.NET Core resolves constructor dependencies lazily
// against the fully-built container, so registration order across Add*Module() calls never gates
// resolution — only the Postgres migration order genuinely does. DocumentService already proves
// this today with its longer-standing dependency on Workflow's own IWorkflowInstanceService, a
// module that also migrates after Document. Reporting is the newest, most pointed proof yet: every
// module registered above it (Tenant first) already depends on IAuditService, which now lives in
// Reporting and is registered dead last — and every one of those earlier modules resolves it fine,
// because the container is built once, from the whole IServiceCollection, before the first
// request. Don't "fix" this into a Core hoist later on registration-order grounds alone — there's
// no actual problem to fix. (RequiresModuleEntitlementAttribute itself stays inside Billing's own
// Presentation/Filters, not Core, since Core never depends on any Module — see that attribute's
// own doc comment.)
builder.Services.AddTenantModule(builder.Configuration);
builder.Services.AddNotificationModule(builder.Configuration);
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddPropertyAssetModule(builder.Configuration);
builder.Services.AddVendorModule(builder.Configuration);
builder.Services.AddDocumentModule(builder.Configuration);
builder.Services.AddWorkflowModule(builder.Configuration);
builder.Services.AddInspectionModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);
builder.Services.AddBillingModule(builder.Configuration);
builder.Services.AddReportingModule(builder.Configuration);

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

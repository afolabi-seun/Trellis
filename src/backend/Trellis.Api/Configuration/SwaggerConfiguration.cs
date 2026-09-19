using Microsoft.OpenApi.Models;

namespace Trellis.Api.Configuration;

/// <summary>
/// OpenAPI specs grouped per module (api_standards.md, "OpenAPI Grouping By Module") — one
/// browsable spec per module at <c>/swagger/{module}/swagger.json</c>, plus a combined
/// <c>"all"</c> doc that stays the frontend typed-client generation source
/// (frontend_architecture.md). Every module's controllers get a Swashbuckle
/// <c>GroupName</c> matching its <see cref="ModuleRegistry"/> key.
/// </summary>
public static class SwaggerConfiguration
{
    public static IServiceCollection AddTrellisSwaggerGen(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            foreach (var module in ModuleRegistry.Keys)
            {
                options.SwaggerDoc(module, new OpenApiInfo { Title = $"Trellis — {module}", Version = "v1" });
            }

            options.SwaggerDoc("all", new OpenApiInfo { Title = "Trellis API", Version = "v1" });

            options.DocInclusionPredicate((docName, apiDescription) =>
                docName == "all" || apiDescription.GroupName == docName);
        });

        return services;
    }

    public static WebApplication UseTrellisSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (var module in ModuleRegistry.Keys)
            {
                options.SwaggerEndpoint($"/swagger/{module}/swagger.json", module);
            }

            options.SwaggerEndpoint("/swagger/all/swagger.json", "all");
        });

        return app;
    }
}

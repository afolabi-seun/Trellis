using Microsoft.EntityFrameworkCore;
using Trellis.Api.Core.Data;
using Trellis.Api.Modules.Workflow.Application.Interfaces;
using Trellis.Api.Modules.Workflow.Application.Services;
using Trellis.Api.Modules.Workflow.Infrastructure.Data;
using Trellis.Api.Modules.Workflow.Infrastructure.Repositories;

namespace Trellis.Api.Modules.Workflow;

public static class WorkflowModuleExtensions
{
    public static IServiceCollection AddWorkflowModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<WorkflowDbContext>((sp, opt) => opt.UseNpgsql(
            config.GetConnectionString("Trellis"),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "trellis_workflow"))
            .AddInterceptors(sp.GetRequiredService<TenantScopeConnectionInterceptor>()));

        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IWorkflowTemplateRepository, WorkflowTemplateRepository>();
        services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();

        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IWorkflowTemplateService, WorkflowTemplateService>();
        services.AddScoped<IWorkflowInstanceService, WorkflowInstanceService>();

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Trellis.Api.Modules.Auth.Domain.Entities;

namespace Trellis.Api.Modules.Auth.Infrastructure.Data;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<MfaRecoveryCode> MfaRecoveryCodes => Set<MfaRecoveryCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AuthDbContext).Assembly,
            t => t.Namespace?.StartsWith("Trellis.Api.Modules.Auth.", StringComparison.Ordinal) == true);
}

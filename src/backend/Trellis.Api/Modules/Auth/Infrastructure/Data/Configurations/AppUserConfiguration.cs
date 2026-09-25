using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trellis.Api.Modules.Auth.Domain.Entities;

namespace Trellis.Api.Modules.Auth.Infrastructure.Data.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    private static readonly Dictionary<UserRole, string> RoleToColumn = new()
    {
        [UserRole.SuperAdmin] = "SUPER_ADMIN",
        [UserRole.TenantAdmin] = "TENANT_ADMIN",
        [UserRole.DepartmentStaff] = "DEPARTMENT_STAFF",
        [UserRole.VendorUser] = "VENDOR_USER",
    };

    private static readonly Dictionary<string, UserRole> ColumnToRole =
        RoleToColumn.ToDictionary(kv => kv.Value, kv => kv.Key);

    private static readonly Dictionary<UserStatus, string> StatusToColumn = new()
    {
        [UserStatus.PendingActivation] = "PENDING_ACTIVATION",
        [UserStatus.Active] = "ACTIVE",
        [UserStatus.Locked] = "LOCKED",
        [UserStatus.Deactivated] = "DEACTIVATED",
    };

    private static readonly Dictionary<string, UserStatus> ColumnToStatus =
        StatusToColumn.ToDictionary(kv => kv.Value, kv => kv.Key);

    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user", schema: "trellis_auth");
        builder.HasKey(u => u.UserId);
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(u => u.UserId).HasColumnName("user_id");
        builder.Property(u => u.TenantId).HasColumnName("tenant_id");
        builder.Property(u => u.VendorId).HasColumnName("vendor_id");
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasConversion(r => RoleToColumn[r], r => ColumnToRole[r])
            .HasMaxLength(30);

        builder.Property(u => u.DepartmentIds).HasColumnName("department_ids");

        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasConversion(s => StatusToColumn[s], s => ColumnToStatus[s])
            .HasMaxLength(20)
            .HasDefaultValue(UserStatus.PendingActivation);

        builder.Property(u => u.FailedLoginCount).HasColumnName("failed_login_count").HasDefaultValue((short)0);
        builder.Property(u => u.LockedUntil).HasColumnName("locked_until");
        builder.Property(u => u.TotpSecretEncrypted).HasColumnName("totp_secret_encrypted");

        builder.HasIndex(u => u.Username).IsUnique();
    }
}

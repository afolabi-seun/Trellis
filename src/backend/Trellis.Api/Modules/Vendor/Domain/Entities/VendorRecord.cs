namespace Trellis.Api.Modules.Vendor.Domain.Entities;

/// <summary>data_model.md's `trellis_vendor.vendor.category`.</summary>
public enum VendorCategory
{
    Maintenance,
    Legal,
    Inspection,
    Other,
}

/// <summary>
/// BDR-VEN-001 — a global, tenant-less identity (no <c>tenant_id</c> column at all): one Vendor
/// organization can be independently engaged by many tenants (BDR-VEN-004). Deliberately has no
/// Status/Archive — BDR-VEN-006's own text is explicit termination is "scoped per tenant–Vendor
/// pair, never to the Vendor identity platform-wide," so there is no platform-level lifecycle to
/// archive; see <see cref="TenantVendorRelationship"/> for the per-tenant lifecycle instead.
/// </summary>
public sealed class VendorRecord
{
    public Guid VendorId { get; private set; }
    public string VendorName { get; private set; } = null!;
    public VendorCategory Category { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private VendorRecord() { }

    public static VendorRecord Create(string vendorName, VendorCategory category)
    {
        if (string.IsNullOrWhiteSpace(vendorName))
        {
            throw new ArgumentException("Vendor name is required.", nameof(vendorName));
        }

        return new VendorRecord
        {
            VendorId = Guid.NewGuid(),
            VendorName = vendorName,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}

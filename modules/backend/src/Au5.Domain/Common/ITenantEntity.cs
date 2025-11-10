namespace Au5.Domain.Common;

/// <summary>
/// Marker interface for entities that belong to a specific tenant (organization).
/// Entities implementing this interface will have automatic tenant-based query filtering applied.
/// </summary>
public interface ITenantEntity
{
	/// <summary>
	/// The organization (tenant) ID that owns this entity.
	/// </summary>
	Guid OrganizationId { get; set; }
}

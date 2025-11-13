namespace Au5.Application.Common.Abstractions;

/// <summary>
/// Provides access to the current tenant (organization) context.
/// This service is used throughout the application to ensure data isolation.
/// </summary>
public interface ITenantProvider
{
	/// <summary>
	/// Gets the current tenant's (organization) ID from the authenticated user's context.
	/// Returns null if no tenant context is available (e.g., during setup or for anonymous requests).
	/// </summary>
	Guid? TenantId { get; }

	/// <summary>
	/// Gets whether a valid tenant context is available.
	/// </summary>
	bool HasTenantContext { get; }

	/// <summary>
	/// Gets the tenant ID or throws an exception if no tenant context is available.
	/// Use this method when tenant context is required for the operation.
	/// </summary>
	Guid GetRequiredTenantId();
}

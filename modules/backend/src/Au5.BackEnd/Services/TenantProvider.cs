using Au5.Application.Common.Abstractions;
using Au5.Shared;

namespace Au5.BackEnd.Services;

/// <summary>
/// Implementation of ITenantProvider that extracts tenant (organization) ID from HTTP context claims.
/// This service provides the current tenant context for multi-tenancy data isolation.
/// </summary>
public class TenantProvider : ITenantProvider
{
	private readonly IHttpContextAccessor _httpContextAccessor;

	public TenantProvider(IHttpContextAccessor httpContextAccessor)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	public Guid? TenantId
	{
		get
		{
			var tenantIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimConstants.TenantId)?.Value;
			return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : null;
		}
	}

	public bool HasTenantContext => TenantId.HasValue;

	public Guid GetRequiredTenantId()
	{
		if (!TenantId.HasValue)
		{
			throw new InvalidOperationException(
				"Tenant context is required but not available. Ensure the user is authenticated and has a valid tenant_id claim.");
		}

		return TenantId.Value;
	}
}

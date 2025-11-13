using Au5.Application.Common.Abstractions;
using Au5.Shared;
using System.Security.Claims;

namespace Au5.BackEnd.Middlewares;

/// <summary>
/// Middleware that resolves the current tenant from various sources and validates tenant context.
/// Supports tenant resolution from:
/// 1. JWT token claims (tenant_id)
/// 2. Custom header (X-Tenant-Id)
/// 3. Subdomain (optional, for future enhancement)
/// </summary>
public class TenantResolutionMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger<TenantResolutionMiddleware> _logger;

	// Endpoints that don't require tenant context (initial setup, health checks, etc.)
	private static readonly HashSet<string> TenantExemptPaths = new(StringComparer.OrdinalIgnoreCase)
	{
		"/setup",
		"/health",
		"/swagger",
		"/authentication/login"
	};

	public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
	{
		var path = context.Request.Path.Value ?? string.Empty;

		// Skip tenant validation for exempt paths
		if (IsExemptPath(path))
		{
			await _next(context);
			return;
		}

		// Try to resolve tenant from header (useful for testing or non-JWT scenarios)
		if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdHeader)
		    && Guid.TryParse(tenantIdHeader.First(), out var headerTenantId))
		{
			// If there's a tenant in the header but not in the claims, add it to the claims
			if (context.User.Identity?.IsAuthenticated == true
			    && !context.User.HasClaim(c => c.Type == ClaimConstants.TenantId))
			{
				var identity = context.User.Identity as ClaimsIdentity;
				identity?.AddClaim(new Claim(ClaimConstants.TenantId, headerTenantId.ToString()));

				_logger.LogDebug("Tenant {TenantId} resolved from X-Tenant-Id header", headerTenantId);
			}
		}

		// Tenant will be automatically resolved from JWT claims by TenantProvider
		// If the user is authenticated but doesn't have a tenant, log a warning
		if (context.User.Identity?.IsAuthenticated == true && !tenantProvider.HasTenantContext)
		{
			_logger.LogWarning("Authenticated user {UserId} does not have a tenant context",
				context.User.FindFirst(ClaimConstants.UserId)?.Value);
		}

		await _next(context);
	}

	private static bool IsExemptPath(string path)
	{
		return TenantExemptPaths.Any(exemptPath =>
			path.StartsWith(exemptPath, StringComparison.OrdinalIgnoreCase));
	}
}

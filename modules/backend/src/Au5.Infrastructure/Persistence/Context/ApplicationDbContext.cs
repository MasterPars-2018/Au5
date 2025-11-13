using System.Reflection;
using Au5.Application.Common.Abstractions;
using Au5.Domain.Common;
using Au5.Infrastructure.Persistence.Extensions;
using Au5.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Au5.Infrastructure.Persistence.Context;

public class ApplicationDbContext(
	DbContextOptions<ApplicationDbContext> options,
	ILogger<ApplicationDbContext> contextLogger,
	ITenantProvider tenantProvider)
   : DbContext(options), IApplicationDbContext
{
	private readonly ILogger<ApplicationDbContext> _logger = contextLogger;
	private readonly ITenantProvider _tenantProvider = tenantProvider;

	public new async Task<Result> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			return result is 0 ? Error.Failure("DB.Failure", "SaveChangesFailed") : Result.Success();
		}
		catch (Exception ex)
		{
			_logger.LogDatabaseException(ex);
			return Error.Failure("DB.Failure", "SaveChangesFailed");
		}
	}

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		_ = configurationBuilder.Properties<string>().HaveMaxLength(200);
		_ = configurationBuilder.Properties<string>().AreUnicode(false);
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.RegisterEntities(typeof(EntityAttribute).Assembly);
		_ = modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

		// Apply multi-tenancy global query filters
		ApplyTenantQueryFilters(modelBuilder);

		modelBuilder.SeedData();
		foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
		{
			relationship.DeleteBehavior = DeleteBehavior.NoAction;
		}
	}

	/// <summary>
	/// Applies global query filters for multi-tenancy data isolation.
	/// All entities implementing ITenantEntity will automatically filter by the current tenant.
	/// </summary>
	private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
	{
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			// Check if the entity implements ITenantEntity
			if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
			{
				// Create the filter expression: entity => !_tenantProvider.HasTenantContext || entity.OrganizationId == _tenantProvider.TenantId
				var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "entity");

				// entity.OrganizationId
				var organizationIdProperty = System.Linq.Expressions.Expression.Property(parameter, nameof(ITenantEntity.OrganizationId));

				// _tenantProvider.TenantId
				var tenantIdProperty = System.Linq.Expressions.Expression.Property(
					System.Linq.Expressions.Expression.Constant(_tenantProvider),
					nameof(ITenantProvider.TenantId));

				// _tenantProvider.HasTenantContext
				var hasTenantContextProperty = System.Linq.Expressions.Expression.Property(
					System.Linq.Expressions.Expression.Constant(_tenantProvider),
					nameof(ITenantProvider.HasTenantContext));

				// !_tenantProvider.HasTenantContext
				var notHasTenantContext = System.Linq.Expressions.Expression.Not(hasTenantContextProperty);

				// entity.OrganizationId == _tenantProvider.TenantId
				var tenantFilter = System.Linq.Expressions.Expression.Equal(organizationIdProperty, tenantIdProperty);

				// !_tenantProvider.HasTenantContext || entity.OrganizationId == _tenantProvider.TenantId
				var combinedFilter = System.Linq.Expressions.Expression.OrElse(notHasTenantContext, tenantFilter);

				// Create lambda: entity => !_tenantProvider.HasTenantContext || entity.OrganizationId == _tenantProvider.TenantId
				var lambda = System.Linq.Expressions.Expression.Lambda(combinedFilter, parameter);

				modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);

				_logger.LogInformation("Applied tenant query filter to entity: {EntityType}", entityType.ClrType.Name);
			}
		}
	}
}

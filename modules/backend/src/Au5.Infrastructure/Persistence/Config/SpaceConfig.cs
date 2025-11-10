using Au5.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Au5.Infrastructure.Persistence.Config;

public class SpaceConfig : IEntityTypeConfiguration<Space>
{
	public void Configure(EntityTypeBuilder<Space> builder)
	{
		builder.HasKey(t => t.Id)
			.HasName("PK_dbo_Space");

		builder.Property(x => x.Name)
			.IsRequired()
			.HasMaxLength(100);

		builder.Property(x => x.Description)
			.HasMaxLength(500);

		builder.Property(x => x.IsActive)
			.IsRequired();

		// Multi-tenancy: Organization relationship
		builder.Property(x => x.OrganizationId)
			.IsRequired();

		builder.HasOne(x => x.Organization)
			.WithMany(x => x.Spaces)
			.HasForeignKey(x => x.OrganizationId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => x.OrganizationId)
			.HasDatabaseName("IX_Space_OrganizationId");
	}
}

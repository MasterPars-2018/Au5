using Au5.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Au5.Infrastructure.Persistence.Config;

public class OrganizationConfig : IEntityTypeConfiguration<Organization>
{
	public void Configure(EntityTypeBuilder<Organization> builder)
	{
		builder.HasKey(t => t.Id)
			.HasName("PK_dbo_Organization");

		builder.Property(x => x.Name)
			.IsRequired()
			.HasMaxLength(200);

		builder.Property(x => x.Domain)
			.HasMaxLength(100);

		builder.HasIndex(x => x.Domain)
			.IsUnique()
			.HasFilter("[Domain] IS NOT NULL");

		builder.Property(x => x.IsActive)
			.IsRequired()
			.HasDefaultValue(true);

		builder.Property(x => x.CreatedAt)
			.IsRequired();

		// Navigation properties
		builder.HasMany(x => x.Users)
			.WithOne(x => x.Organization)
			.HasForeignKey(x => x.OrganizationId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany(x => x.Spaces)
			.WithOne(x => x.Organization)
			.HasForeignKey(x => x.OrganizationId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasMany(x => x.Meetings)
			.WithOne(x => x.Organization)
			.HasForeignKey(x => x.OrganizationId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}

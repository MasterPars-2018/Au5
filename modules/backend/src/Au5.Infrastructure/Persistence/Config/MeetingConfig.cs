using Au5.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Au5.Infrastructure.Persistence.Config;

public class MeetingConfig : IEntityTypeConfiguration<Meeting>
{
	public void Configure(EntityTypeBuilder<Meeting> builder)
	{
		builder.HasKey(t => t.Id)
			.HasName("PK_dbo_Meeting");

		builder.Property(m => m.MeetName)
			.IsUnicode(true)
			.HasMaxLength(200);

		builder.Property(m => m.BotName)
			.IsUnicode(true)
			.HasMaxLength(200);

		builder.Property(m => m.Platform)
			.HasMaxLength(20);

		builder.Property(m => m.HashToken)
			.IsUnicode(false)
			.HasMaxLength(100);

		// Multi-tenancy: Organization relationship
		builder.Property(m => m.OrganizationId)
			.IsRequired();

		builder.HasOne(m => m.Organization)
			.WithMany(o => o.Meetings)
			.HasForeignKey(m => m.OrganizationId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(m => m.OrganizationId)
			.HasDatabaseName("IX_Meeting_OrganizationId");
	}
}

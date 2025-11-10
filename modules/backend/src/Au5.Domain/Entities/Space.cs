using Au5.Domain.Common;

namespace Au5.Domain.Entities;

[Entity]
public class Space : ITenantEntity
{
	public Guid Id { get; set; }

	public string Name { get; set; }

	public string Description { get; set; }

	public bool IsActive { get; set; }

	// Multi-tenancy: Every space belongs to one organization
	public Guid OrganizationId { get; set; }

	public Organization Organization { get; set; }

	public ICollection<UserSpace> UserSpaces { get; set; }

	public ICollection<MeetingSpace> MeetingSpaces { get; set; }
}

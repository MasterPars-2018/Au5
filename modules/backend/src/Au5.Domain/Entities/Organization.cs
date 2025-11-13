namespace Au5.Domain.Entities;

/// <summary>
/// Represents a tenant organization in the multi-tenant system.
/// Each organization is a separate tenant with isolated data.
/// </summary>
[Entity]
public class Organization
{
	public Guid Id { get; set; }

	public string Name { get; set; }

	public string? Domain { get; set; }

	public bool IsActive { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime? UpdatedAt { get; set; }

	// Navigation properties
	public ICollection<User> Users { get; set; }

	public ICollection<Space> Spaces { get; set; }

	public ICollection<Meeting> Meetings { get; set; }
}

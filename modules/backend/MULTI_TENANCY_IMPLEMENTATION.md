# Multi-Tenancy Implementation Guide

## Overview

This document describes the complete multi-tenancy (multi-tenant architecture) implementation for the Au5 backend system. The implementation provides full data isolation between organizations (tenants) using a shared database with row-level security.

## Architecture

### Multi-Tenancy Model: Shared Database, Shared Schema

- **Single Database**: All tenants share the same database
- **Row-Level Isolation**: Each row is tagged with `OrganizationId` (tenant identifier)
- **Automatic Filtering**: EF Core global query filters ensure automatic tenant isolation
- **JWT-Based Resolution**: Tenant context is resolved from JWT token claims

---

## Core Components

### 1. Organization Entity (`Organization.cs`)

**Location**: `/Au5.Domain/Entities/Organization.cs`

The root entity representing a tenant:

```csharp
[Entity]
public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Domain { get; set; }        // Optional subdomain
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<User> Users { get; set; }
    public ICollection<Space> Spaces { get; set; }
    public ICollection<Meeting> Meetings { get; set; }
}
```

### 2. Tenant-Aware Entities

All entities that should be isolated by tenant implement `ITenantEntity`:

**Location**: `/Au5.Domain/Common/ITenantEntity.cs`

```csharp
public interface ITenantEntity
{
    Guid OrganizationId { get; set; }
}
```

**Modified Entities**:
- `User` - Every user belongs to one organization
- `Space` - Workspaces are organization-specific
- `Meeting` - Meetings are scoped to an organization

### 3. Tenant Provider Service

**Interface**: `/Au5.Application/Common/Abstractions/ITenantProvider.cs`

```csharp
public interface ITenantProvider
{
    Guid? TenantId { get; }
    bool HasTenantContext { get; }
    Guid GetRequiredTenantId();
}
```

**Implementation**: `/Au5.BackEnd/Services/TenantProvider.cs`

Extracts the tenant ID from the JWT token's `tenant_id` claim.

### 4. Tenant Resolution Middleware

**Location**: `/Au5.BackEnd/Middlewares/TenantResolutionMiddleware.cs`

Resolves tenant context from:
1. JWT token (`tenant_id` claim) - primary method
2. Custom header (`X-Tenant-Id`) - for testing/admin scenarios
3. Subdomain (planned for future)

**Exempt Paths** (no tenant required):
- `/setup` - Initial configuration
- `/authentication/login` - Login endpoint
- `/health` - Health checks
- `/swagger` - API documentation

---

## Authentication & JWT Integration

### JWT Token Claims

**Location**: `/Au5.Shared/ClaimConstants.cs`

Added `tenant_id` claim:

```csharp
public const string TenantId = "tenant_id";
```

### Token Generation

**Location**: `/Au5.Infrastructure/Authentication/TokenService.cs`

Updated `GenerateToken` to include organization ID:

```csharp
public TokenResponse GenerateToken(Guid userId, string fullName, RoleTypes role, Guid organizationId)
{
    var claims = new[]
    {
        new Claim(ClaimConstants.UserId, userId.ToString()),
        new Claim(ClaimConstants.Name, fullName ?? string.Empty),
        new Claim(ClaimConstants.Role, ((byte)role).ToString()),
        new Claim(ClaimConstants.Jti, jti),
        new Claim(ClaimConstants.TenantId, organizationId.ToString())  // ← New
    };
    // ...
}
```

### Login Flow Update

**Location**: `/Au5.Application/Features/Authentication/Login/LoginCommand.Handler.cs`

```csharp
return _tokenService.GenerateToken(
    user.Id,
    user.FullName,
    user.Role,
    user.OrganizationId  // ← Pass organization ID
);
```

---

## Data Isolation

### Global Query Filters

**Location**: `/Au5.Infrastructure/Persistence/Context/ApplicationDbContext.cs`

Automatically filters all queries by the current tenant:

```csharp
private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
        {
            // Filter: entity => !_tenantProvider.HasTenantContext
            //                   || entity.OrganizationId == _tenantProvider.TenantId
            // ...
        }
    }
}
```

**How it works**:
- Every query on `User`, `Space`, `Meeting` automatically includes:
  ```sql
  WHERE OrganizationId = @CurrentTenantId
  ```
- Filters are applied at the EF Core level (cannot be bypassed accidentally)
- If no tenant context exists (e.g., during setup), filter is disabled

### Entity Configurations

**Locations**:
- `/Au5.Infrastructure/Persistence/Config/OrganizationConfig.cs`
- `/Au5.Infrastructure/Persistence/Config/UserConfig.cs` (updated)
- `/Au5.Infrastructure/Persistence/Config/SpaceConfig.cs` (updated)
- `/Au5.Infrastructure/Persistence/Config/MeetingConfig.cs` (updated)

Each tenant-aware entity has:
- Required `OrganizationId` foreign key
- Index on `OrganizationId` for performance
- `Restrict` delete behavior to prevent cascading deletes

---

## Database Migration

### Migration File

**Location**: `/Au5.Infrastructure/Persistence/Migrations/20251110000000_AddMultiTenancySupport.cs`

**Changes**:
1. Creates `Organization` table
2. Adds `OrganizationId` columns to `User`, `Space`, `Meeting`
3. Migrates existing data to default organization
4. Adds foreign keys and indexes

**Safe Migration Strategy**:
- Columns added as nullable first
- Existing data updated to default organization (`00000000-0000-0000-0000-000000000001`)
- Columns altered to required after data migration
- Rollback support via `Down()` method

### Seeded Data

**Location**: `/Au5.Infrastructure/Persistence/Extensions/ModelBuilderExtentions.cs`

```csharp
var defaultOrgId = new Guid("00000000-0000-0000-0000-000000000001");
modelBuilder.Entity<Organization>().HasData(
    new Organization
    {
        Id = defaultOrgId,
        Name = "Default Organization",
        IsActive = true,
        CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });
```

---

## Middleware Pipeline

**Location**: `/Au5.BackEnd/Program.cs`

```csharp
app.UseAuthentication();                        // 1. Authenticate user
app.UseMiddleware<JwtBlacklistMiddleware>();    // 2. Check token blacklist
app.UseMiddleware<TenantResolutionMiddleware>(); // 3. Resolve tenant context ← New
app.UseAuthorization();                         // 4. Authorize user
```

---

## Usage Examples

### Example 1: Querying Tenant-Specific Data

```csharp
// In any command/query handler or repository
public async Task<List<Meeting>> GetUserMeetings(Guid userId, CancellationToken ct)
{
    // This query automatically filters by current tenant
    return await _dbContext.Set<Meeting>()
        .Where(m => m.BotInviterUserId == userId)
        .ToListAsync(ct);

    // Generated SQL includes:
    // WHERE OrganizationId = @TenantId AND BotInviterUserId = @UserId
}
```

### Example 2: Creating Tenant-Scoped Entities

```csharp
public async Task<Result> CreateSpace(CreateSpaceCommand command, CancellationToken ct)
{
    var tenantId = _tenantProvider.GetRequiredTenantId();

    var space = new Space
    {
        Id = Guid.NewGuid(),
        Name = command.Name,
        Description = command.Description,
        OrganizationId = tenantId,  // Set tenant context
        IsActive = true
    };

    _dbContext.Set<Space>().Add(space);
    return await _dbContext.SaveChangesAsync(ct);
}
```

### Example 3: Bypassing Tenant Filter (Admin Scenarios)

```csharp
// For system-wide operations (admin only, use with caution)
public async Task<int> GetAllOrganizationsCount()
{
    return await _dbContext.Set<Organization>()
        .IgnoreQueryFilters()  // Explicitly bypass tenant filter
        .CountAsync();
}
```

### Example 4: Testing with Custom Tenant

```http
# For testing, you can override tenant via header
GET /api/meetings
Authorization: Bearer <jwt_token>
X-Tenant-Id: 12345678-1234-1234-1234-123456789012
```

---

## Security Considerations

### ✅ Implemented Protections

1. **Automatic Query Filtering**
   - All queries filtered by default
   - Cannot be accidentally bypassed
   - Applied at EF Core expression tree level

2. **JWT-Based Tenant Resolution**
   - Tenant ID embedded in cryptographically signed token
   - Cannot be tampered with by client
   - Validated on every request

3. **Explicit Tenant Assignment**
   - All entity creations must explicitly set `OrganizationId`
   - No implicit/default tenant assignment

4. **Restrict Delete Behavior**
   - Prevents cascading deletes across tenant boundaries
   - Requires explicit cleanup logic

### ⚠️ Important Guidelines

1. **Always use `GetRequiredTenantId()`** when creating entities
   - Throws exception if tenant context is missing
   - Prevents data leakage

2. **Audit `IgnoreQueryFilters()` usage**
   - Only use for system-wide admin operations
   - Add authorization checks before use

3. **Validate tenant context** in sensitive operations
   - Double-check `_tenantProvider.HasTenantContext`
   - Log tenant-related errors

4. **Test with multiple tenants**
   - Verify data isolation in integration tests
   - Ensure queries return correct tenant data

---

## Testing Multi-Tenancy

### Unit Tests

```csharp
[Fact]
public async Task QueryMeetings_OnlyReturnsCurrentTenantData()
{
    // Arrange
    var tenant1Id = Guid.NewGuid();
    var tenant2Id = Guid.NewGuid();

    var tenantProviderMock = new Mock<ITenantProvider>();
    tenantProviderMock.Setup(x => x.TenantId).Returns(tenant1Id);
    tenantProviderMock.Setup(x => x.HasTenantContext).Returns(true);

    var dbContext = CreateDbContext(tenantProviderMock.Object);

    // Create test data for both tenants
    dbContext.Meetings.AddRange(
        new Meeting { Id = Guid.NewGuid(), OrganizationId = tenant1Id },
        new Meeting { Id = Guid.NewGuid(), OrganizationId = tenant2Id }
    );
    await dbContext.SaveChangesAsync();

    // Act
    var results = await dbContext.Meetings.ToListAsync();

    // Assert
    Assert.All(results, m => Assert.Equal(tenant1Id, m.OrganizationId));
}
```

### Integration Tests

```csharp
[Fact]
public async Task Login_IncludesTenantIdInToken()
{
    // Arrange
    var client = _factory.CreateClient();
    var organization = await CreateTestOrganization();
    var user = await CreateTestUser(organization.Id);

    // Act
    var response = await client.PostAsJsonAsync("/authentication/login", new
    {
        Username = user.Email,
        Password = "test123"
    });

    // Assert
    var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
    var token = new JwtSecurityTokenHandler().ReadJwtToken(tokenResponse.AccessToken);

    var tenantClaim = token.Claims.First(c => c.Type == "tenant_id");
    Assert.Equal(organization.Id.ToString(), tenantClaim.Value);
}
```

---

## Performance Considerations

### Indexes

All tenant-aware entities have indexes on `OrganizationId`:

```sql
CREATE INDEX IX_User_OrganizationId ON [User] (OrganizationId)
CREATE INDEX IX_Space_OrganizationId ON [Space] (OrganizationId)
CREATE INDEX IX_Meeting_OrganizationId ON [Meeting] (OrganizationId)
```

### Query Optimization

Global query filters are translated to SQL `WHERE` clauses:

```sql
-- Before (vulnerable)
SELECT * FROM Meetings WHERE UserId = @UserId

-- After (tenant-safe)
SELECT * FROM Meetings
WHERE OrganizationId = @TenantId AND UserId = @UserId
```

### Caching Considerations

When caching data, include tenant ID in cache key:

```csharp
var cacheKey = $"meetings_{userId}_{tenantId}";
```

---

## Future Enhancements

### 1. Database-Per-Tenant Model

For larger tenants requiring dedicated databases:

```csharp
public interface ITenantConnectionProvider
{
    string GetConnectionString(Guid tenantId);
}
```

### 2. Subdomain-Based Tenant Resolution

```
https://acme.au5.com → Tenant: Acme Corp
https://contoso.au5.com → Tenant: Contoso Ltd
```

### 3. Tenant-Specific Configuration

```csharp
public class OrganizationSettings
{
    public Guid OrganizationId { get; set; }
    public JsonDocument CustomSettings { get; set; }
    public string Theme { get; set; }
    public bool EnableFeatureX { get; set; }
}
```

### 4. Cross-Tenant Reporting (Admin)

```csharp
[Authorize(Roles = "SuperAdmin")]
public async Task<TenantUsageReport> GetCrossTenantReport()
{
    return await _dbContext.Meetings
        .IgnoreQueryFilters()
        .GroupBy(m => m.OrganizationId)
        .Select(g => new { TenantId = g.Key, Count = g.Count() })
        .ToListAsync();
}
```

---

## Troubleshooting

### Issue: "Tenant context is required but not available"

**Cause**: Attempting to query tenant-aware entities without authentication or tenant context.

**Solution**:
- Ensure user is authenticated
- Verify JWT token includes `tenant_id` claim
- Check if endpoint is exempt from tenant resolution
- Add endpoint to `TenantResolutionMiddleware.TenantExemptPaths` if needed

### Issue: Queries return empty results despite data existing

**Cause**: User's tenant ID doesn't match data's `OrganizationId`.

**Solution**:
- Verify user's `OrganizationId` in database
- Check JWT token's `tenant_id` claim
- Ensure data was created with correct `OrganizationId`

### Issue: Migration fails on existing database

**Cause**: Existing data without `OrganizationId`.

**Solution**: Migration handles this automatically by:
1. Adding columns as nullable
2. Updating existing rows to default organization
3. Making columns required

---

## API Changes

### Breaking Changes

1. **Token Generation**: `ITokenService.GenerateToken()` now requires `organizationId` parameter
2. **User Entity**: Added required `OrganizationId` property
3. **Space/Meeting Entities**: Added required `OrganizationId` property

### Migration Path for Existing Clients

1. **Run Migration**: Database migration is backward-compatible
2. **Update Token Consumers**: Parse `tenant_id` claim from JWT if needed
3. **Entity Creation**: Always set `OrganizationId` when creating entities

---

## File Structure Summary

```
Au5.Domain/
├── Common/
│   ├── ITenantEntity.cs                  ← NEW: Marker interface
│   └── ...
└── Entities/
    ├── Organization.cs                   ← NEW: Tenant entity
    ├── User.cs                           ← MODIFIED: +OrganizationId
    ├── Space.cs                          ← MODIFIED: +OrganizationId
    └── Meeting.cs                        ← MODIFIED: +OrganizationId

Au5.Application/
└── Common/Abstractions/
    ├── ITenantProvider.cs                ← NEW: Tenant context provider
    └── ITokenService.cs                  ← MODIFIED: +organizationId param

Au5.Infrastructure/
├── Authentication/
│   └── TokenService.cs                   ← MODIFIED: Include tenant_id claim
└── Persistence/
    ├── Context/
    │   └── ApplicationDbContext.cs       ← MODIFIED: Global query filters
    ├── Config/
    │   ├── OrganizationConfig.cs         ← NEW: EF configuration
    │   ├── UserConfig.cs                 ← MODIFIED: +Organization FK
    │   ├── SpaceConfig.cs                ← MODIFIED: +Organization FK
    │   └── MeetingConfig.cs              ← MODIFIED: +Organization FK
    ├── Extensions/
    │   └── ModelBuilderExtentions.cs     ← MODIFIED: Seed default org
    └── Migrations/
        └── 20251110000000_AddMultiTenancySupport.cs  ← NEW

Au5.BackEnd/
├── Services/
│   └── TenantProvider.cs                 ← NEW: Tenant resolution
├── Middlewares/
│   └── TenantResolutionMiddleware.cs     ← NEW: Tenant middleware
├── ConfigServices.cs                     ← MODIFIED: Register TenantProvider
└── Program.cs                            ← MODIFIED: Add middleware

Au5.Shared/
└── ClaimConstants.cs                     ← MODIFIED: +TenantId const
```

---

## Summary

This multi-tenancy implementation provides:

✅ **Complete Data Isolation** - Row-level security with EF Core query filters
✅ **Automatic Tenant Resolution** - JWT-based, transparent to business logic
✅ **Production-Ready Security** - Cannot be bypassed accidentally
✅ **Clean Architecture** - Minimal changes to existing code
✅ **Backward Compatible** - Existing data migrated automatically
✅ **Performance Optimized** - Indexed tenant columns, efficient queries
✅ **Extensible** - Ready for database-per-tenant, subdomain routing, etc.

All queries, inserts, and updates automatically respect tenant boundaries without explicit filtering in business logic.

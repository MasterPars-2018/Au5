using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Au5.Infrastructure.Migrations
{
	/// <inheritdoc />
	public partial class AddMultiTenancySupport : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Create Organization table
			migrationBuilder.CreateTable(
				name: "Organization",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
					Name = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
					Domain = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
					IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
					CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
					UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_dbo_Organization", x => x.Id);
				});

			migrationBuilder.CreateIndex(
				name: "IX_Organization_Domain",
				table: "Organization",
				column: "Domain",
				unique: true,
				filter: "[Domain] IS NOT NULL");

			// Insert default organization
			migrationBuilder.InsertData(
				table: "Organization",
				columns: new[] { "Id", "Name", "Domain", "IsActive", "CreatedAt", "UpdatedAt" },
				values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "Default Organization", null, true, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), null });

			// Add OrganizationId column to User table
			// First add as nullable to allow existing data
			migrationBuilder.AddColumn<Guid>(
				name: "OrganizationId",
				table: "User",
				type: "uniqueidentifier",
				nullable: true);

			// Update existing users to belong to default organization
			migrationBuilder.Sql(@"
				UPDATE [User]
				SET [OrganizationId] = '00000000-0000-0000-0000-000000000001'
				WHERE [OrganizationId] IS NULL
			");

			// Now make it required
			migrationBuilder.AlterColumn<Guid>(
				name: "OrganizationId",
				table: "User",
				type: "uniqueidentifier",
				nullable: false,
				oldClrType: typeof(Guid),
				oldType: "uniqueidentifier",
				oldNullable: true);

			migrationBuilder.CreateIndex(
				name: "IX_User_OrganizationId",
				table: "User",
				column: "OrganizationId");

			migrationBuilder.AddForeignKey(
				name: "FK_User_Organization_OrganizationId",
				table: "User",
				column: "OrganizationId",
				principalTable: "Organization",
				principalColumn: "Id",
				onDelete: ReferentialAction.Restrict);

			// Add OrganizationId column to Space table
			migrationBuilder.AddColumn<Guid>(
				name: "OrganizationId",
				table: "Space",
				type: "uniqueidentifier",
				nullable: true);

			// Update existing spaces to belong to default organization
			migrationBuilder.Sql(@"
				UPDATE [Space]
				SET [OrganizationId] = '00000000-0000-0000-0000-000000000001'
				WHERE [OrganizationId] IS NULL
			");

			migrationBuilder.AlterColumn<Guid>(
				name: "OrganizationId",
				table: "Space",
				type: "uniqueidentifier",
				nullable: false,
				oldClrType: typeof(Guid),
				oldType: "uniqueidentifier",
				oldNullable: true);

			migrationBuilder.CreateIndex(
				name: "IX_Space_OrganizationId",
				table: "Space",
				column: "OrganizationId");

			migrationBuilder.AddForeignKey(
				name: "FK_Space_Organization_OrganizationId",
				table: "Space",
				column: "OrganizationId",
				principalTable: "Organization",
				principalColumn: "Id",
				onDelete: ReferentialAction.Restrict);

			// Add OrganizationId column to Meeting table
			migrationBuilder.AddColumn<Guid>(
				name: "OrganizationId",
				table: "Meeting",
				type: "uniqueidentifier",
				nullable: true);

			// Update existing meetings to belong to default organization
			migrationBuilder.Sql(@"
				UPDATE [Meeting]
				SET [OrganizationId] = '00000000-0000-0000-0000-000000000001'
				WHERE [OrganizationId] IS NULL
			");

			migrationBuilder.AlterColumn<Guid>(
				name: "OrganizationId",
				table: "Meeting",
				type: "uniqueidentifier",
				nullable: false,
				oldClrType: typeof(Guid),
				oldType: "uniqueidentifier",
				oldNullable: true);

			migrationBuilder.CreateIndex(
				name: "IX_Meeting_OrganizationId",
				table: "Meeting",
				column: "OrganizationId");

			migrationBuilder.AddForeignKey(
				name: "FK_Meeting_Organization_OrganizationId",
				table: "Meeting",
				column: "OrganizationId",
				principalTable: "Organization",
				principalColumn: "Id",
				onDelete: ReferentialAction.Restrict);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			// Remove foreign keys
			migrationBuilder.DropForeignKey(
				name: "FK_Meeting_Organization_OrganizationId",
				table: "Meeting");

			migrationBuilder.DropForeignKey(
				name: "FK_Space_Organization_OrganizationId",
				table: "Space");

			migrationBuilder.DropForeignKey(
				name: "FK_User_Organization_OrganizationId",
				table: "User");

			// Remove indexes
			migrationBuilder.DropIndex(
				name: "IX_Meeting_OrganizationId",
				table: "Meeting");

			migrationBuilder.DropIndex(
				name: "IX_Space_OrganizationId",
				table: "Space");

			migrationBuilder.DropIndex(
				name: "IX_User_OrganizationId",
				table: "User");

			// Remove columns
			migrationBuilder.DropColumn(
				name: "OrganizationId",
				table: "Meeting");

			migrationBuilder.DropColumn(
				name: "OrganizationId",
				table: "Space");

			migrationBuilder.DropColumn(
				name: "OrganizationId",
				table: "User");

			// Remove Organization table
			migrationBuilder.DropTable(
				name: "Organization");
		}
	}
}

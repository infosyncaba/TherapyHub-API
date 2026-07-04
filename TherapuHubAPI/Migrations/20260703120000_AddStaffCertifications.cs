using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TherapuHubAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffCertifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Certifications",
                table: "Staff",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Certifications",
                table: "Staff");
        }
    }
}

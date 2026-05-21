using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Patients_ExternalId_TenantId",
                table: "Patients",
                columns: new[] { "ExternalId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ExternalId_TenantId",
                table: "Appointments",
                columns: new[] { "ExternalId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_ExternalId_TenantId",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_ExternalId_TenantId",
                table: "Appointments");
        }
    }
}

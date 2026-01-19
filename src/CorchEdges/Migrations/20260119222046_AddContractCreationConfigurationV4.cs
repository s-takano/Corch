using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorchEdges.Migrations
{
    /// <inheritdoc />
    public partial class AddContractCreationConfigurationV4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "定期借家",
                schema: "corch_edges_raw",
                table: "contract_creation",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "定期借家",
                schema: "corch_edges_raw",
                table: "contract_creation");
        }
    }
}

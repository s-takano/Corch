using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorchEdges.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceIdToContractCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "新規ID",
                schema: "corch_edges_raw",
                table: "contract_creation",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "新規ID",
                schema: "corch_edges_raw",
                table: "contract_creation");
        }
    }
}

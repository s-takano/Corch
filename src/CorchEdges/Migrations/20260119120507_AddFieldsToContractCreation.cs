using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorchEdges.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldsToContractCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "契約始期",
                schema: "corch_edges_raw",
                table: "contract_creation",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "契約終期",
                schema: "corch_edges_raw",
                table: "contract_creation",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "契約始期",
                schema: "corch_edges_raw",
                table: "contract_creation");

            migrationBuilder.DropColumn(
                name: "契約終期",
                schema: "corch_edges_raw",
                table: "contract_creation");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorchEdges.Migrations
{
    /// <inheritdoc />
    public partial class AddRelationBetweenProcessedFileAndProcessingLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "processing_log_id",
                schema: "corch_edges_raw",
                table: "processed_file",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_processed_file_processing_log_id",
                schema: "corch_edges_raw",
                table: "processed_file",
                column: "processing_log_id");

            migrationBuilder.AddForeignKey(
                name: "FK_processed_file_processing_log_processing_log_id",
                schema: "corch_edges_raw",
                table: "processed_file",
                column: "processing_log_id",
                principalSchema: "corch_edges_raw",
                principalTable: "processing_log",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_processed_file_processing_log_processing_log_id",
                schema: "corch_edges_raw",
                table: "processed_file");

            migrationBuilder.DropIndex(
                name: "IX_processed_file_processing_log_id",
                schema: "corch_edges_raw",
                table: "processed_file");

            migrationBuilder.DropColumn(
                name: "processing_log_id",
                schema: "corch_edges_raw",
                table: "processed_file");
        }
    }
}

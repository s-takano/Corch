using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorchEdges.Migrations
{
    /// <inheritdoc />
    public partial class LinkContractsToProcessedFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "更新後バイク",
                schema: "corch_edges_raw",
                table: "contract_current");

            migrationBuilder.DropColumn(
                name: "更新後水道",
                schema: "corch_edges_raw",
                table: "contract_current");

            migrationBuilder.DropColumn(
                name: "更新後給湯",
                schema: "corch_edges_raw",
                table: "contract_current");

            migrationBuilder.DropColumn(
                name: "更新後自転車",
                schema: "corch_edges_raw",
                table: "contract_current");

            migrationBuilder.DropColumn(
                name: "更新後電気",
                schema: "corch_edges_raw",
                table: "contract_current");

            migrationBuilder.DropColumn(
                name: "更新後電話",
                schema: "corch_edges_raw",
                table: "contract_current");

            migrationBuilder.AddColumn<int>(
                name: "ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_termination",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_renewal",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_current",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_creation",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_contract_termination_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_termination",
                column: "ProcessedFileId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_renewal_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_renewal",
                column: "ProcessedFileId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_current_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_current",
                column: "ProcessedFileId");

            migrationBuilder.CreateIndex(
                name: "IX_contract_creation_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_creation",
                column: "ProcessedFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_contract_creation_processed_file_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_creation",
                column: "ProcessedFileId",
                principalSchema: "corch_edges_raw",
                principalTable: "processed_file",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_current_processed_file_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_current",
                column: "ProcessedFileId",
                principalSchema: "corch_edges_raw",
                principalTable: "processed_file",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_renewal_processed_file_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_renewal",
                column: "ProcessedFileId",
                principalSchema: "corch_edges_raw",
                principalTable: "processed_file",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_termination_processed_file_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_termination",
                column: "ProcessedFileId",
                principalSchema: "corch_edges_raw",
                principalTable: "processed_file",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contract_creation_processed_file_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_creation");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_current_processed_file_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_current");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_renewal_processed_file_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_renewal");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_termination_processed_file_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_termination");

            migrationBuilder.DropIndex(
                name: "IX_contract_termination_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_termination");

            migrationBuilder.DropIndex(
                name: "IX_contract_renewal_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_renewal");

            migrationBuilder.DropIndex(
                name: "IX_contract_current_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_current");

            migrationBuilder.DropIndex(
                name: "IX_contract_creation_ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_creation");

            migrationBuilder.DropColumn(
                name: "ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_termination");

            migrationBuilder.DropColumn(
                name: "ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_renewal");

            migrationBuilder.DropColumn(
                name: "ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_current");

            migrationBuilder.DropColumn(
                name: "ProcessedFileId",
                schema: "corch_edges_raw",
                table: "contract_creation");

            migrationBuilder.AddColumn<decimal>(
                name: "更新後バイク",
                schema: "corch_edges_raw",
                table: "contract_current",
                type: "numeric(12,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "更新後水道",
                schema: "corch_edges_raw",
                table: "contract_current",
                type: "numeric(12,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "更新後給湯",
                schema: "corch_edges_raw",
                table: "contract_current",
                type: "numeric(12,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "更新後自転車",
                schema: "corch_edges_raw",
                table: "contract_current",
                type: "numeric(12,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "更新後電気",
                schema: "corch_edges_raw",
                table: "contract_current",
                type: "numeric(12,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "更新後電話",
                schema: "corch_edges_raw",
                table: "contract_current",
                type: "numeric(12,0)",
                nullable: true);
        }
    }
}

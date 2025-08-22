using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CorchEdges.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "corch_edges_raw");

            migrationBuilder.CreateTable(
                name: "contract_creation",
                schema: "corch_edges_raw",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    契約ID = table.Column<string>(type: "text", nullable: true),
                    物件No = table.Column<int>(type: "integer", nullable: true),
                    部屋No = table.Column<int>(type: "integer", nullable: true),
                    契約者1No = table.Column<int>(type: "integer", nullable: true),
                    物件名 = table.Column<string>(type: "text", nullable: true),
                    契約者名 = table.Column<string>(type: "text", nullable: true),
                    進捗管理ステータス = table.Column<string>(type: "text", nullable: true),
                    契約ステータス = table.Column<string>(type: "text", nullable: true),
                    入居申込日 = table.Column<DateOnly>(type: "date", nullable: true),
                    入居予定日 = table.Column<DateOnly>(type: "date", nullable: true),
                    鍵引渡日 = table.Column<DateOnly>(type: "date", nullable: true),
                    契約日 = table.Column<DateOnly>(type: "date", nullable: true),
                    礼金_家 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    広告料 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    販路 = table.Column<string>(type: "text", nullable: true),
                    販路その他 = table.Column<string>(type: "text", nullable: true),
                    上司報告者 = table.Column<string>(type: "text", nullable: true),
                    上司確認日 = table.Column<DateOnly>(type: "date", nullable: true),
                    敷金_家 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    仲介手数料 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    保証料 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    アパート保険代 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    鍵交換費 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    用紙印紙代 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    引落手数料 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    自転車登録事務手数料 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    バイク登録事務手数料 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    インターネット申込金 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    極度額 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    出力日時 = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_creation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contract_current",
                schema: "corch_edges_raw",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    契約ID = table.Column<string>(type: "text", nullable: true),
                    入居者コード = table.Column<string>(type: "text", nullable: true),
                    契約分類名 = table.Column<string>(type: "text", nullable: true),
                    物件名 = table.Column<string>(type: "text", nullable: true),
                    部屋分類 = table.Column<string>(type: "text", nullable: true),
                    物件No = table.Column<int>(type: "integer", nullable: true),
                    部屋No = table.Column<int>(type: "integer", nullable: true),
                    契約者1No = table.Column<int>(type: "integer", nullable: true),
                    契約者_名 = table.Column<string>(type: "text", nullable: true),
                    入居者1_名 = table.Column<string>(type: "text", nullable: true),
                    連帯保証人1_名 = table.Column<string>(type: "text", nullable: true),
                    緊急連絡先_名 = table.Column<string>(type: "text", nullable: true),
                    契約状態 = table.Column<string>(type: "text", nullable: true),
                    解約日 = table.Column<DateOnly>(type: "date", nullable: true),
                    契約改定日 = table.Column<DateOnly>(type: "date", nullable: true),
                    入居日_新規契約開始日 = table.Column<DateOnly>(type: "date", nullable: true),
                    契約開始日 = table.Column<DateOnly>(type: "date", nullable: true),
                    期日 = table.Column<DateOnly>(type: "date", nullable: true),
                    予告 = table.Column<int>(type: "integer", nullable: true),
                    家賃 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    管理費 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    水道 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    給湯 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    電気 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    電話 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    自転車 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    バイク = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    駐車料 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    敷金_家 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    ガス保証金 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    RC = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    AC = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    更新契約始期 = table.Column<DateOnly>(type: "date", nullable: true),
                    更新契約終期 = table.Column<DateOnly>(type: "date", nullable: true),
                    更新後家賃 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    更新後管理費 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    更新後水道 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    更新後給湯 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    更新後電気 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    更新後自転車 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    更新後バイク = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    更新後電話 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    更新後駐車料 = table.Column<decimal>(type: "numeric(12,0)", nullable: true),
                    保証会社No = table.Column<int>(type: "integer", nullable: true),
                    保証会社名 = table.Column<string>(type: "text", nullable: true),
                    保証会社ID = table.Column<string>(type: "text", nullable: true),
                    備考1 = table.Column<string>(type: "text", nullable: true),
                    備考2 = table.Column<string>(type: "text", nullable: true),
                    ペット = table.Column<string>(type: "text", nullable: true),
                    生活保護 = table.Column<string>(type: "text", nullable: true),
                    法人契約 = table.Column<string>(type: "text", nullable: true),
                    定期借家 = table.Column<bool>(type: "boolean", nullable: true),
                    契約者_カナ = table.Column<string>(type: "text", nullable: true),
                    契約者_郵便番号 = table.Column<string>(type: "text", nullable: true),
                    契約者_住所 = table.Column<string>(type: "text", nullable: true),
                    契約者_電話番号 = table.Column<string>(type: "text", nullable: true),
                    契約者_携帯電話番号 = table.Column<string>(type: "text", nullable: true),
                    契約者_メールアドレス = table.Column<string>(type: "text", nullable: true),
                    契約者_性別 = table.Column<string>(type: "text", nullable: true),
                    契約者_生年月日 = table.Column<DateOnly>(type: "date", nullable: true),
                    契約者_勤務先名 = table.Column<string>(type: "text", nullable: true),
                    契約者_勤務先電話番号 = table.Column<string>(type: "text", nullable: true),
                    連帯保証人1_続柄 = table.Column<string>(type: "text", nullable: true),
                    連帯保証人1_郵便番号 = table.Column<string>(type: "text", nullable: true),
                    連帯保証人1_住所 = table.Column<string>(type: "text", nullable: true),
                    連帯保証人1_電話番号 = table.Column<string>(type: "text", nullable: true),
                    連帯保証人1_メールアドレス = table.Column<string>(type: "text", nullable: true),
                    連帯保証人1_勤務先名 = table.Column<string>(type: "text", nullable: true),
                    連帯保証人1_勤務先電話番号 = table.Column<string>(type: "text", nullable: true),
                    緊急連絡先_続柄 = table.Column<string>(type: "text", nullable: true),
                    緊急連絡先_郵便番号 = table.Column<string>(type: "text", nullable: true),
                    緊急連絡先_住所 = table.Column<string>(type: "text", nullable: true),
                    緊急連絡先_電話番号 = table.Column<string>(type: "text", nullable: true),
                    出力日時 = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_current", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contract_renewal",
                schema: "corch_edges_raw",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    契約ID = table.Column<string>(type: "text", nullable: true),
                    物件No = table.Column<int>(type: "integer", nullable: true),
                    部屋No = table.Column<int>(type: "integer", nullable: true),
                    契約者1No = table.Column<int>(type: "integer", nullable: true),
                    物件名 = table.Column<string>(type: "text", nullable: true),
                    契約者_名 = table.Column<string>(type: "text", nullable: true),
                    進捗管理ステータス = table.Column<string>(type: "text", nullable: true),
                    更新日 = table.Column<DateOnly>(type: "date", nullable: true),
                    前契約始期 = table.Column<DateOnly>(type: "date", nullable: true),
                    前契約終期 = table.Column<DateOnly>(type: "date", nullable: true),
                    次契約始期 = table.Column<DateOnly>(type: "date", nullable: true),
                    次契約終期 = table.Column<DateOnly>(type: "date", nullable: true),
                    契約更新日 = table.Column<DateOnly>(type: "date", nullable: true),
                    出力日時 = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_renewal", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contract_termination",
                schema: "corch_edges_raw",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    契約ID = table.Column<string>(type: "text", nullable: true),
                    物件No = table.Column<int>(type: "integer", nullable: true),
                    部屋No = table.Column<int>(type: "integer", nullable: true),
                    契約者1No = table.Column<int>(type: "integer", nullable: true),
                    物件名 = table.Column<string>(type: "text", nullable: true),
                    契約者_名 = table.Column<string>(type: "text", nullable: true),
                    部屋分類 = table.Column<string>(type: "text", nullable: true),
                    進捗管理ステータス = table.Column<string>(type: "text", nullable: true),
                    _受付日 = table.Column<DateOnly>(type: "date", nullable: true),
                    届受取日 = table.Column<DateOnly>(type: "date", nullable: true),
                    _転出予定日 = table.Column<DateOnly>(type: "date", nullable: true),
                    変更月 = table.Column<DateOnly>(type: "date", nullable: true),
                    _転出日 = table.Column<DateOnly>(type: "date", nullable: true),
                    _日割日 = table.Column<DateOnly>(type: "date", nullable: true),
                    転出点検日 = table.Column<DateOnly>(type: "date", nullable: true),
                    転出点検者 = table.Column<string>(type: "text", nullable: true),
                    精算書作成日 = table.Column<DateOnly>(type: "date", nullable: true),
                    精算書作成者 = table.Column<string>(type: "text", nullable: true),
                    打合せ日 = table.Column<DateOnly>(type: "date", nullable: true),
                    最終決裁日 = table.Column<DateOnly>(type: "date", nullable: true),
                    精算書送付日 = table.Column<DateOnly>(type: "date", nullable: true),
                    精算書送付者 = table.Column<string>(type: "text", nullable: true),
                    精算金送金日 = table.Column<DateOnly>(type: "date", nullable: true),
                    逆請求入金日 = table.Column<DateOnly>(type: "date", nullable: true),
                    書類スキャン日 = table.Column<DateOnly>(type: "date", nullable: true),
                    工事No = table.Column<string>(type: "text", nullable: true),
                    出力日時 = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_termination", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_file",
                schema: "corch_edges_raw",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_name = table.Column<string>(type: "varchar(500)", nullable: false),
                    share_point_item_id = table.Column<string>(type: "varchar(100)", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<string>(type: "varchar(50)", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    record_count = table.Column<int>(type: "integer", nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_file", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processing_log",
                schema: "corch_edges_raw",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    site_id = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    list_id = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    delta_link = table.Column<string>(type: "text", maxLength: 2000, nullable: true),
                    last_processed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    last_processed_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    last_error = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    subscription_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    successful_runs = table.Column<int>(type: "integer", nullable: false),
                    failed_runs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_processing_log_last_processed_at",
                schema: "corch_edges_raw",
                table: "processing_log",
                column: "last_processed_at");

            migrationBuilder.CreateIndex(
                name: "IX_processing_log_list_id",
                schema: "corch_edges_raw",
                table: "processing_log",
                column: "list_id");

            migrationBuilder.CreateIndex(
                name: "IX_processing_log_site_id",
                schema: "corch_edges_raw",
                table: "processing_log",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_processing_log_status",
                schema: "corch_edges_raw",
                table: "processing_log",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_processing_log_subscription_id",
                schema: "corch_edges_raw",
                table: "processing_log",
                column: "subscription_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contract_creation",
                schema: "corch_edges_raw");

            migrationBuilder.DropTable(
                name: "contract_current",
                schema: "corch_edges_raw");

            migrationBuilder.DropTable(
                name: "contract_renewal",
                schema: "corch_edges_raw");

            migrationBuilder.DropTable(
                name: "contract_termination",
                schema: "corch_edges_raw");

            migrationBuilder.DropTable(
                name: "processed_file",
                schema: "corch_edges_raw");

            migrationBuilder.DropTable(
                name: "processing_log",
                schema: "corch_edges_raw");
        }
    }
}

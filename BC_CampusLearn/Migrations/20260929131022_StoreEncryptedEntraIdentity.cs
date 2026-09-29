using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BC_CampusLearn.Migrations
{
    /// <inheritdoc />
    public partial class StoreEncryptedEntraIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BcUsers_PersonnelNumber",
                table: "BcUsers");

            migrationBuilder.AlterColumn<string>(
                name: "PersonnelNumber",
                table: "BcUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedEntraObjectId",
                table: "BcUsers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedEntraTenantId",
                table: "BcUsers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntraIdentityLookupHash",
                table: "BcUsers",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BcUsers_EntraIdentityLookupHash",
                table: "BcUsers",
                column: "EntraIdentityLookupHash",
                unique: true,
                filter: "[EntraIdentityLookupHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BcUsers_PersonnelNumber",
                table: "BcUsers",
                column: "PersonnelNumber",
                unique: true,
                filter: "[PersonnelNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BcUsers_EntraIdentityLookupHash",
                table: "BcUsers");

            migrationBuilder.DropIndex(
                name: "IX_BcUsers_PersonnelNumber",
                table: "BcUsers");

            migrationBuilder.DropColumn(
                name: "EncryptedEntraObjectId",
                table: "BcUsers");

            migrationBuilder.DropColumn(
                name: "EncryptedEntraTenantId",
                table: "BcUsers");

            migrationBuilder.DropColumn(
                name: "EntraIdentityLookupHash",
                table: "BcUsers");

            migrationBuilder.Sql(
                "UPDATE [BcUsers] SET [PersonnelNumber] = CONCAT('OID-', [BcUserId]) WHERE [PersonnelNumber] IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "PersonnelNumber",
                table: "BcUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BcUsers_PersonnelNumber",
                table: "BcUsers",
                column: "PersonnelNumber",
                unique: true);
        }
    }
}

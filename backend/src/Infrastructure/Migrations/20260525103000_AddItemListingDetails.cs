using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class AddItemListingDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Condition",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "MeetupLocation",
                table: "items",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "North Terrace Campus");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "items",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNegotiable",
                table: "items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Brand",
                table: "items");

            migrationBuilder.DropColumn(
                name: "Condition",
                table: "items");

            migrationBuilder.DropColumn(
                name: "IsNegotiable",
                table: "items");

            migrationBuilder.DropColumn(
                name: "MeetupLocation",
                table: "items");
        }
    }
}

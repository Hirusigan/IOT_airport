using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LegOrder",
                table: "Journeys",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TripId",
                table: "Journeys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RunId = table.Column<string>(type: "text", nullable: false),
                    LuggageId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                });

            // Preserve each pre-existing journey as an independent single-leg trip.
            migrationBuilder.Sql("INSERT INTO \"Trips\" (\"Id\", \"RunId\", \"LuggageId\") SELECT \"Id\", \"RunId\", \"LuggageId\" FROM \"Journeys\"");
            migrationBuilder.Sql("UPDATE \"Journeys\" SET \"TripId\" = \"Id\", \"LegOrder\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Journeys_TripId_LegOrder",
                table: "Journeys",
                columns: new[] { "TripId", "LegOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_RunId_LuggageId",
                table: "Trips",
                columns: new[] { "RunId", "LuggageId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Journeys_Trips_TripId",
                table: "Journeys",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Journeys_Trips_TripId",
                table: "Journeys");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Journeys_TripId_LegOrder",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "LegOrder",
                table: "Journeys");

            migrationBuilder.DropColumn(
                name: "TripId",
                table: "Journeys");
        }
    }
}

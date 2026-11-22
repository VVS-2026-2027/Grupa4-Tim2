using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eVrticSistem.Migrations
{
    /// <inheritdoc />
    public partial class Task3NajaveOdsustva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NajavaOdsustva",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoditeljId = table.Column<int>(type: "int", nullable: false),
                    DijeteId = table.Column<int>(type: "int", nullable: false),
                    DatumOd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DatumDo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Razlog = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DatumKreiranja = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NajavaOdsustva", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NajavaOdsustva_Dijete_DijeteId",
                        column: x => x.DijeteId,
                        principalTable: "Dijete",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NajavaOdsustva_Roditelj_RoditeljId",
                        column: x => x.RoditeljId,
                        principalTable: "Roditelj",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NajavaOdsustva_DijeteId",
                table: "NajavaOdsustva",
                column: "DijeteId");

            migrationBuilder.CreateIndex(
                name: "IX_NajavaOdsustva_RoditeljId",
                table: "NajavaOdsustva",
                column: "RoditeljId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NajavaOdsustva");
        }
    }
}

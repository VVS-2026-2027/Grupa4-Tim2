using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eVrticSistem.Migrations
{
    /// <inheritdoc />
    public partial class Task3ZakazaneObavijesti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZakazanaObavijest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OdgajateljId = table.Column<int>(type: "int", nullable: false),
                    RoditeljId = table.Column<int>(type: "int", nullable: false),
                    Naslov = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Sadrzaj = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    VrijemeSlanjaUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DatumSlanjaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BrojPokusaja = table.Column<int>(type: "int", nullable: false),
                    Greska = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZakazanaObavijest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZakazanaObavijest_Odgajatelj_OdgajateljId",
                        column: x => x.OdgajateljId,
                        principalTable: "Odgajatelj",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ZakazanaObavijest_Roditelj_RoditeljId",
                        column: x => x.RoditeljId,
                        principalTable: "Roditelj",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZakazanaObavijest_OdgajateljId",
                table: "ZakazanaObavijest",
                column: "OdgajateljId");

            migrationBuilder.CreateIndex(
                name: "IX_ZakazanaObavijest_RoditeljId",
                table: "ZakazanaObavijest",
                column: "RoditeljId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZakazanaObavijest");
        }
    }
}

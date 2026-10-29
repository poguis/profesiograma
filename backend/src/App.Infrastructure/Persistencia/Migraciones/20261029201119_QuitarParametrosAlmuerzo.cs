using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace App.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class QuitarParametrosAlmuerzo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Parametro",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Parametro",
                keyColumn: "Id",
                keyValue: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Parametro",
                columns: new[] { "Id", "Clave", "CreadoPorId", "Descripcion", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "TipoDato", "Valor" },
                values: new object[,]
                {
                    { 6, "ALMUERZO_SALIDA_OPCIONES", 1, "Horas permitidas de salida a almuerzo", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "LIST", "11:00,12:00,13:00,14:00" },
                    { 7, "ALMUERZO_REGRESO_OPCIONES", 1, "Horas permitidas de regreso de almuerzo", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "LIST", "12:00,13:00,14:00,15:00" }
                });
        }
    }
}

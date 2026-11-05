using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarUpnUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Upn",
                schema: "dbo",
                table: "Usuario",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            // TAREA-7b-2: se quitó el UpdateData de la semilla SISTEMA (Upn = NULL): la columna nace NULL y, en el script
            // sin GO, un UPDATE de la columna recién agregada en el mismo lote falla en SSMS ("Invalid column name").

            migrationBuilder.CreateIndex(
                name: "UX_Usuario_Upn",
                schema: "dbo",
                table: "Usuario",
                column: "Upn",
                unique: true,
                filter: "[Upn] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Usuario_Upn",
                schema: "dbo",
                table: "Usuario");

            migrationBuilder.DropColumn(
                name: "Upn",
                schema: "dbo",
                table: "Usuario");
        }
    }
}

using App.Infrastructure.Persistencia.Vistas;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Vistas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"EXEC(N'{VistasSql.VwProyectoResumen_V1.Replace("'", "''")}');");
            migrationBuilder.Sql($"EXEC(N'{VistasSql.VwNovedadDiaVigente_V1.Replace("'", "''")}');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(VistasSql.EliminarVwNovedadDiaVigente);
            migrationBuilder.Sql(VistasSql.EliminarVwProyectoResumen);
        }
    }
}

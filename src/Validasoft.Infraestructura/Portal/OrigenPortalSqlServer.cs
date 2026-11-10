using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha.Portal;

namespace Validasoft.Infraestructura.Portal;

/// <summary>
/// Configuración del origen del Portal (<c>Validasoft:Importacion:Portal</c>): <c>CadenaDeConexion</c> (SQL Server del
/// Portal, solo lectura; sin valor por omisión) y <c>Archivo</c> (ruta de un JSON con la forma de <see cref="ArchivoDelPortal"/>,
/// para desarrollo, pruebas o un sincronizador sin red). Si están las dos, manda el archivo.
/// </summary>
public sealed class FabricaDeOrigenesPortal(IConfiguration configuracion) : IFabricaDeOrigenesPortal
{
    public const string Seccion = "Validasoft:Importacion:Portal";

    private string? Cadena => configuracion[$"{Seccion}:CadenaDeConexion"] is { Length: > 0 } c ? c : null;

    private string? Archivo => configuracion[$"{Seccion}:Archivo"] is { Length: > 0 } a ? a : null;

    public bool SqlServerConfigurado => Cadena is not null;

    public IOrigenPortal Configurado()
    {
        if (Archivo is { } ruta)
        {
            if (!File.Exists(ruta))
                throw ReglaDeNegocioException.Conflicto("origen-portal-no-configurado", $"No existe el archivo del Portal configurado ({Seccion}:Archivo).");
            return OrigenPortalArchivo.DesdeJson(File.ReadAllText(ruta));
        }

        return SqlServer();
    }

    public IOrigenPortal SqlServer() =>
        new OrigenPortalSqlServer(Cadena ?? throw ReglaDeNegocioException.Conflicto("origen-portal-no-configurado",
            $"No hay cadena de conexión del Portal ({Seccion}:CadenaDeConexion)."));
}

/// <summary>
/// Lee el Portal de Proveedores en SQL Server (FM-39): solo <c>SELECT</c>, siempre parametrizados (el cursor y el rango de
/// <c>Terid</c> van como parámetros), por lotes de terceros contiguos en <c>Terid</c>. Recomendado: un usuario de solo lectura
/// y <c>ApplicationIntent=ReadOnly</c> en la cadena. De las tablas con varias filas por tercero que deberían tener una
/// (<c>TerInformacionGeneral</c>, <c>TerInfoTributaria</c>, <c>TerDireccionEstructurada</c>, <c>TerSISO</c>,
/// <c>TerTransaccionMonedaExtranjera</c>) toma la de mayor id (el Portal no tiene índice único).
/// </summary>
public sealed class OrigenPortalSqlServer(string cadena) : IOrigenPortal
{
    private const int TiempoDeEspera = 120;

    public async Task<CatalogosDelPortal> CatalogosAsync(CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);
        var ciudades = await LeerAsync(conexion, "SELECT CiudID, CiuNombre, CiuCodDepartamento, CiuCodMunicipio FROM dbo.Ciudades", [],
            r => new Ciudades(Int(r, 0), Str(r, 1), Str(r, 2), Str(r, 3)), ct);
        var actividades = await LeerAsync(conexion, "SELECT ActEcId, ActEcCodigo, ActECtexto FROM dbo.ActividadEconomica", [],
            r => new ActividadEconomica(Int(r, 0), Str(r, 1), Str(r, 2)), ct);
        var bancos = await LeerAsync(conexion, "SELECT CONVERT(nvarchar(50), Id), Codigo, Texto, Estado FROM dbo.Bancos", [],
            r => new Bancos(Str(r, 0) ?? string.Empty, Str(r, 1), Str(r, 2), Bool(r, 3)), ct);
        var grupos = await LeerAsync(conexion, "SELECT GruId, GruTexto FROM dbo.GruposTercero", [], r => new GruposTercero(Int(r, 0), Str(r, 1)), ct);
        var categorias = await LeerAsync(conexion, "SELECT CatId, CatIdGrupo, CatTexto FROM dbo.CategoriasTercero", [],
            r => new CategoriasTercero(Int(r, 0), Int(r, 1), Str(r, 2)), ct);
        var especialidades = await LeerAsync(conexion, "SELECT EspId, EspIdCategoria, EspIdGrupo, EspTexto FROM dbo.EspecialidadesTercero", [],
            r => new EspecialidadesTercero(Int(r, 0), Int(r, 1), Int(r, 2), Str(r, 3)), ct);
        var paises = await LeerAsync(conexion, "SELECT Id, Sigla, Nombre FROM dbo.Paises", [], r => new Paises(Int(r, 0), Str(r, 1), Str(r, 2)), ct);
        return new CatalogosDelPortal(ciudades, actividades, bancos, grupos, categorias, especialidades, paises);
    }

    public async Task<IReadOnlyList<TerceroDelPortal>> TercerosAsync(int? despuesDe, int tamano, CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);
        var terceros = await LeerAsync(conexion,
            "SELECT TOP (@tamano) Terid, TerEstado, TerFechaRegistro FROM dbo.Terceros WHERE (@despuesDe IS NULL OR Terid > @despuesDe) ORDER BY Terid",
            [new SqlParameter("@tamano", tamano), new SqlParameter("@despuesDe", (object?)despuesDe ?? DBNull.Value)],
            r => new TercerosPortal(Int(r, 0), Int(r, 1), FechaN(r, 2)), ct);
        return terceros.Count == 0 ? [] : await SeccionesAsync(conexion, terceros, ct);
    }

    public async Task<TerceroDelPortal?> TerceroAsync(int terid, CancellationToken ct)
    {
        await using var conexion = await AbrirAsync(ct);
        var terceros = await LeerAsync(conexion, "SELECT Terid, TerEstado, TerFechaRegistro FROM dbo.Terceros WHERE Terid = @terid",
            [new SqlParameter("@terid", terid)], r => new TercerosPortal(Int(r, 0), Int(r, 1), FechaN(r, 2)), ct);
        return terceros.Count == 0 ? null : (await SeccionesAsync(conexion, terceros, ct)).Single();
    }

    /// <summary>Las secciones de los terceros del lote (contiguos en <c>Terid</c>: el rango [primero, último] es exactamente el lote).</summary>
    private static async Task<IReadOnlyList<TerceroDelPortal>> SeccionesAsync(SqlConnection conexion, IReadOnlyList<TercerosPortal> terceros, CancellationToken ct)
    {
        SqlParameter[] Rango() => [new("@desde", terceros[0].Terid), new("@hasta", terceros[^1].Terid)];

        var generales = (await LeerAsync(conexion,
                """
                SELECT TigTerceroId, TigId, TigNombre, TerApellido, TigTipoPersona, TigTipoDocumento, TigNumeroIdentificacion, TerDigitoVerificacion,
                       TigCorreo, TigCiudad, TigDireccion, TigActEconomicaPri, TigTelefono, TigPaginaWeb, TigCertificadoISO, TigTipoEmpresa, TigPais,
                       TigAnioInicioActividades
                FROM dbo.TerInformacionGeneral WHERE TigTerceroId BETWEEN @desde AND @hasta
                """, Rango(),
                r => (Terid: Int(r, 0), Fila: new TerInformacionGeneral(Int(r, 1), Str(r, 2), Str(r, 3), Str(r, 4), Str(r, 5), Str(r, 6), IntN(r, 7), Str(r, 8),
                    Int(r, 9), Str(r, 10), Int(r, 11), Str(r, 12), Str(r, 13), Bool(r, 14), Str(r, 15), Str(r, 16), IntN(r, 17))), ct))
            .GroupBy(x => x.Terid).ToDictionary(g => g.Key, g => g.MaxBy(x => x.Fila.TigId).Fila);

        var direcciones = (await LeerAsync(conexion,
                """
                SELECT TigTerceroId, Id, ViaCodigo, ViaNumero, ViaInterseccion, SecNumero, SecInterseccion, SecNumero2, UnidadCodigo, UnidadNumero
                FROM dbo.TerDireccionEstructurada WHERE TigTerceroId BETWEEN @desde AND @hasta
                """, Rango(),
                r => (Terid: Int(r, 0), Id: Int(r, 1), Fila: new TerDireccionEstructurada(Str(r, 2), Str(r, 3), Str(r, 4), Str(r, 5), Str(r, 6), Str(r, 7),
                    Str(r, 8), Str(r, 9))), ct))
            .GroupBy(x => x.Terid).ToDictionary(g => g.Key, g => g.MaxBy(x => x.Id).Fila);

        var tributarias = (await LeerAsync(conexion,
                """
                SELECT IdTercero, Id, ResponsableIVA, Autorretenedor, Declarante, GranContribuyente, AutoRetenedorICA, RegimenSimple, GranContribuyenteBta,
                       RegimenEspecial, AutoretenedorRFT, DescuentoSENAFIC, ProfesionalIndependiente, ExentoRetencionFuente, AgenteRetenedorIVA, NoObligFacturar,
                       ResolucionGC, FechaGC, ResolucionAutoRete, FechaAutoRete
                FROM dbo.TerInfoTributaria WHERE IdTercero BETWEEN @desde AND @hasta
                """, Rango(),
                r => (Terid: Int(r, 0), Id: Int(r, 1), Fila: new TerInfoTributaria(Bool(r, 2), Bool(r, 3), Bool(r, 4), Bool(r, 5), Bool(r, 6), Bool(r, 7),
                    Bool(r, 8), Bool(r, 9), Bool(r, 10), Bool(r, 11), Bool(r, 12), Bool(r, 13), Bool(r, 14), Bool(r, 15), IntN(r, 16), FechaN(r, 17),
                    IntN(r, 18), FechaN(r, 19))), ct))
            .GroupBy(x => x.Terid).ToDictionary(g => g.Key, g => g.MaxBy(x => x.Id).Fila);

        var ica = (await LeerAsync(conexion,
                "SELECT IdTercero, IdTerIca, IdCiudad, IdActividadEconomica FROM dbo.TerRetencionICA WHERE IdTercero BETWEEN @desde AND @hasta", Rango(),
                r => (Terid: Int(r, 0), Fila: new TerRetencionICA(Int(r, 1), Int(r, 2), Int(r, 3))), ct))
            .ToLookup(x => x.Terid, x => x.Fila);

        var financieras = (await LeerAsync(conexion,
                """
                SELECT IdTercero, Id, Anio, ActivosTotales, PasivosTotales, Patrimonio, IngresosBrutos, IngresosNetos, GastosOperaciones, GastosNoOperaciones,
                       OtrosIngresos
                FROM dbo.TerInfoFinanciera WHERE IdTercero BETWEEN @desde AND @hasta
                """, Rango(),
                r => (Terid: Int(r, 0), Fila: new TerInfoFinanciera(Int(r, 1), Int(r, 2), Dec(r, 3), Dec(r, 4), Dec(r, 5), Dec(r, 6), Dec(r, 7), Dec(r, 8),
                    Dec(r, 9), Dec(r, 10))), ct))
            .ToLookup(x => x.Terid, x => x.Fila);

        var monedas = (await LeerAsync(conexion,
                "SELECT IdTercero, Id, RealizaTransacciones FROM dbo.TerTransaccionMonedaExtranjera WHERE IdTercero BETWEEN @desde AND @hasta", Rango(),
                r => (Terid: Int(r, 0), Id: Int(r, 1), Realiza: Bool(r, 2)), ct))
            .GroupBy(x => x.Terid).ToDictionary(g => g.Key, g => g.MaxBy(x => x.Id).Realiza);
        var paisesPorTipo = (await LeerAsync(conexion,
                """
                SELECT p.IdTransaccionTipo, p.IdPais
                FROM dbo.TerTransaccionMonedaExtranjeraPais p
                JOIN dbo.TerTransaccionMonedaExtranjeraTipo t ON t.Id = p.IdTransaccionTipo
                WHERE t.IdTercero BETWEEN @desde AND @hasta
                """, Rango(), r => (Tipo: Int(r, 0), Pais: Int(r, 1)), ct))
            .ToLookup(x => x.Tipo, x => x.Pais);
        var tipos = (await LeerAsync(conexion,
                "SELECT IdTercero, Id, IdTipoTransaccion, OtraCual FROM dbo.TerTransaccionMonedaExtranjeraTipo WHERE IdTercero BETWEEN @desde AND @hasta",
                Rango(), r => (Terid: Int(r, 0), Fila: new TerTransaccionMonedaExtranjeraTipo(Int(r, 1), Int(r, 2), Str(r, 3))), ct))
            .ToLookup(x => x.Terid, x => x.Fila with { Paises = [.. paisesPorTipo[x.Fila.Id]] });

        var camara = (await LeerAsync(conexion,
                """
                SELECT TerCamTerId, TerCamId, TerTipoRegistro, TerCamTipoDoc, TerCamDocumento, TerCapNombre, TerCapApellido, TerCamCargo, TerCamPorParticipa,
                       TerCamEsPep, TerCamVinculoPep, TerTipoRepresentacion, TerFechaExpedicion, TerLugarExpedicion, TerFechaNacimiento, TerLugarNacimiento,
                       TerLugarResidencia, TerPaisResidencia, TerDireccion, TerNacionalidad, TerRazonSocialNombre, TerRazonSocialTipoDoc, TerRazonSocialDoc,
                       TerCelular, TerCorreo
                FROM dbo.TerCamaraComercio WHERE TerCamTerId BETWEEN @desde AND @hasta
                """, Rango(),
                r => (Terid: Int(r, 0), Fila: new TerCamaraComercio(Int(r, 1), Int(r, 2), Str(r, 3), Str(r, 4), Str(r, 5), Str(r, 6), Str(r, 7), Str(r, 8),
                    BoolN(r, 9), BoolN(r, 10), IntN(r, 11), Str(r, 12), Str(r, 13), Str(r, 14), Str(r, 15), Str(r, 16), Str(r, 17), Str(r, 18), Str(r, 19),
                    Str(r, 20), Str(r, 21), Str(r, 22), Str(r, 23), Str(r, 24))), ct))
            .ToLookup(x => x.Terid, x => x.Fila);

        var sisos = (await LeerAsync(conexion,
                """
                SELECT IdTercero, Id, ProgramaSaludOcupacional, ProgramaFactoresRiesgo, TieneComiteSO, ProgramaSeguridadEhigiene, ProgramaAmbiental,
                       ReglaIntVigente, PoliticaHSEQ, LicPermAutorizaciones
                FROM dbo.TerSISO WHERE IdTercero BETWEEN @desde AND @hasta
                """, Rango(),
                r => (Terid: Int(r, 0), Id: Int(r, 1), Fila: new TerSISO(Bool(r, 2), Bool(r, 3), Bool(r, 4), Bool(r, 5), Bool(r, 6), Bool(r, 7), Bool(r, 8),
                    Bool(r, 9))), ct))
            .GroupBy(x => x.Terid).ToDictionary(g => g.Key, g => g.MaxBy(x => x.Id).Fila);

        var especialidades = (await LeerAsync(conexion, "SELECT TerId, EspId FROM dbo.TerEspecialidad WHERE TerId BETWEEN @desde AND @hasta", Rango(),
                r => (Terid: Int(r, 0), EspId: Int(r, 1)), ct))
            .ToLookup(x => x.Terid, x => x.EspId);

        var constructoras = (await LeerAsync(conexion,
                "SELECT IdTercero, IdConstructora, Estado, FechaRegistro FROM dbo.TercerosConstructora WHERE IdTercero BETWEEN @desde AND @hasta", Rango(),
                r => (Terid: Int(r, 0), Fila: new TercerosConstructora(Int(r, 1), Int(r, 2), FechaN(r, 3))), ct))
            .ToLookup(x => x.Terid, x => x.Fila);

        return
        [
            .. terceros.Select(t => new TerceroDelPortal(t,
                generales.GetValueOrDefault(t.Terid),
                direcciones.GetValueOrDefault(t.Terid),
                tributarias.GetValueOrDefault(t.Terid),
                [.. ica[t.Terid]],
                [.. financieras[t.Terid]],
                monedas.TryGetValue(t.Terid, out var realiza) ? new TerTransaccionMonedaExtranjera(realiza, [.. tipos[t.Terid]]) : null,
                [.. camara[t.Terid]],
                sisos.GetValueOrDefault(t.Terid),
                [.. especialidades[t.Terid]],
                [.. constructoras[t.Terid]]))
        ];
    }

    private async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(ct);
        return conexion;
    }

    private static async Task<List<T>> LeerAsync<T>(SqlConnection conexion, string sql, SqlParameter[] parametros, Func<SqlDataReader, T> fila,
        CancellationToken ct)
    {
        await using var comando = new SqlCommand(sql, conexion) { CommandTimeout = TiempoDeEspera };
        comando.Parameters.AddRange(parametros);
        await using var lector = await comando.ExecuteReaderAsync(ct);
        var filas = new List<T>();
        while (await lector.ReadAsync(ct))
            filas.Add(fila(lector));
        return filas;
    }

    // Lectores tolerantes al tipo exacto de la columna (int, smallint, tinyint; bit; decimal o money): el Portal no es uniforme.
    private static int Int(SqlDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture);

    private static int? IntN(SqlDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture);

    private static string? Str(SqlDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture);

    private static bool Bool(SqlDataReader r, int i) => !r.IsDBNull(i) && Convert.ToBoolean(r.GetValue(i), CultureInfo.InvariantCulture);

    private static bool? BoolN(SqlDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToBoolean(r.GetValue(i), CultureInfo.InvariantCulture);

    private static decimal Dec(SqlDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToDecimal(r.GetValue(i), CultureInfo.InvariantCulture);

    private static DateTime? FechaN(SqlDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToDateTime(r.GetValue(i), CultureInfo.InvariantCulture);
}

using System.Globalization;
using Microsoft.Data.SqlClient;





namespace Validasoft.Adaptadores.Portal;

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

        // HU 49093 del Portal: las tablas de la declaración PEP solo existen si esa rama está desplegada. Con SQL dinámico guardado por OBJECT_ID, un
        // Portal sin ellas no falla ni devuelve filas (una consulta con un nombre inexistente ni siquiera compilaría).
        var declaracionesPep = await DeclaracionesPepAsync(conexion, Rango, ct);

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
                [.. constructoras[t.Terid]],
                [.. declaracionesPep[t.Terid]]))
        ];
    }

    /// <summary>
    /// Las declaraciones PEP <b>activas</b> del lote con sus familiares y sociedades. Las tablas son del Portal con la HU 49093: si no existen
    /// (<c>OBJECT_ID</c> nulo) no se lee nada. Solo columnas de texto o fecha: la imagen de la firma no está en la base, sino en el disco del Portal.
    /// </summary>
    private static async Task<ILookup<int, TerDeclaracionPep>> DeclaracionesPepAsync(SqlConnection conexion, Func<SqlParameter[]> rango, CancellationToken ct)
    {
        const string Param = "N'@desde INT, @hasta INT', @desde, @hasta";
        var cabeceras = await LeerAsync(conexion,
            $$"""
            IF OBJECT_ID(N'dbo.TerDeclaracionPep', N'U') IS NOT NULL
            EXEC sp_executesql N'
                SELECT IdTercero, Id, TerCamId, RelacionEmpresa, RelacionOtroCual, Nacionalidad, Direccion, CargoDesempenado, Institucion, PaisInstitucion,
                       FechaVinculacion, FechaDesvinculacion, CargoVigente, TieneCuentasExterior, PaisesCuentasExterior, ConyugeNombre, ConyugeTipoDocumento,
                       ConyugeNumeroDocumento, TieneVinculoPep, VinculoPepNombre, VinculoPepParentesco, VinculoPepCargoInstitucion, VinculoPepFechaVinculacion,
                       VinculoPepFechaDesvinculacion, VinculoPepVigente, AceptaJuramento, FirmaEnCalidad, FirmanteNombre, FirmanteTipoDocumento,
                       FirmanteNumeroDocumento, FechaFirma, FirmaHash
                FROM dbo.TerDeclaracionPep WHERE IdTercero BETWEEN @desde AND @hasta AND Activa = 1', {{Param}}
            """, rango(),
            r => (Terid: Int(r, 0), Id: Int(r, 1), Fila: new TerDeclaracionPep(Int(r, 2), Int(r, 3), Str(r, 4), Str(r, 5), Str(r, 6), Str(r, 7), Str(r, 8), Str(r, 9),
                FechaN(r, 10), FechaN(r, 11), Bool(r, 12), Bool(r, 13), Str(r, 14), Str(r, 15), Str(r, 16), Str(r, 17), Bool(r, 18), Str(r, 19), Str(r, 20),
                Str(r, 21), FechaN(r, 22), FechaN(r, 23), Bool(r, 24), Bool(r, 25), Int(r, 26), Str(r, 27), Str(r, 28), Str(r, 29), FechaN(r, 30), Str(r, 31))), ct);
        if (cabeceras.Count == 0)
            return Enumerable.Empty<TerDeclaracionPep>().ToLookup(d => d.TerCamId);

        var familiares = (await LeerAsync(conexion,
                $$"""
                IF OBJECT_ID(N'dbo.TerDeclaracionPepFamiliar', N'U') IS NOT NULL
                EXEC sp_executesql N'
                    SELECT f.IdDeclaracionPep, f.NombreCompleto, f.Parentesco, f.TipoDocumento, f.NumeroDocumento
                    FROM dbo.TerDeclaracionPepFamiliar f JOIN dbo.TerDeclaracionPep d ON d.Id = f.IdDeclaracionPep
                    WHERE d.IdTercero BETWEEN @desde AND @hasta AND d.Activa = 1 ORDER BY f.Id', {{Param}}
                """, rango(), r => (Id: Int(r, 0), Fila: new TerDeclaracionPepFamiliar(Str(r, 1), Str(r, 2), Str(r, 3), Str(r, 4))), ct))
            .ToLookup(x => x.Id, x => x.Fila);
        var sociedades = (await LeerAsync(conexion,
                $$"""
                IF OBJECT_ID(N'dbo.TerDeclaracionPepSociedad', N'U') IS NOT NULL
                EXEC sp_executesql N'
                    SELECT s.IdDeclaracionPep, s.NombreRazonSocial, s.Identificacion, s.Calidad, s.CalidadOtraCual
                    FROM dbo.TerDeclaracionPepSociedad s JOIN dbo.TerDeclaracionPep d ON d.Id = s.IdDeclaracionPep
                    WHERE d.IdTercero BETWEEN @desde AND @hasta AND d.Activa = 1 ORDER BY s.Id', {{Param}}
                """, rango(), r => (Id: Int(r, 0), Fila: new TerDeclaracionPepSociedad(Str(r, 1), Str(r, 2), Int(r, 3), Str(r, 4))), ct))
            .ToLookup(x => x.Id, x => x.Fila);

        // Una por contacto (UNIQUE(TerCamId) en el Portal); por tercero, para entregarlas con el resto de sus filas.
        return cabeceras
            .Select(c => (c.Terid, Fila: c.Fila with { Familiares = [.. familiares[c.Id]], Sociedades = [.. sociedades[c.Id]] }))
            .ToLookup(x => x.Terid, x => x.Fila);
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

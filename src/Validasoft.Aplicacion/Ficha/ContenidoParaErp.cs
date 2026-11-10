using System.Text.Json;
using Marten;
using Validasoft.Contratos;
using Validasoft.Dominio.Ficha;

namespace Validasoft.Aplicacion.Ficha;

/// <summary>
/// Lo aceptado de una sección, normalizado para el ERP (F3, FM-32): una función pura de los datos canónicos de la versión y
/// los catálogos de plataforma. La usan el evento <c>seccion.aceptada</c> y <c>GET /api/v1/vinculos/{id}/ficha</c>, así los dos
/// dicen exactamente lo mismo. Los códigos llevan su nombre del catálogo (nulo si el código ya no está activo); las calidades
/// tributarias salen de <see cref="CalidadesTributarias"/> y el representante de <see cref="RepresentanteLegalPrincipal"/>.
/// </summary>
public static class ContenidoParaErp
{
    /// <summary>Datos tipados de la versión aceptada (JSON canónico en snake_case).</summary>
    public static object Datos(string seccion, JsonElement canonico) => CatalogoDeSecciones.Requerida(seccion).Esquema.Deserializar(canonico);

    /// <summary>Las entradas de catálogo a las que refieren los datos (para cargarlas en una sola consulta).</summary>
    public static IEnumerable<ReferenciaACatalogo> Referencias(string seccion, object datos) =>
        CatalogoDeSecciones.Requerida(seccion).Esquema.Referencias(datos);

    /// <summary>Carga los catálogos que necesitan varias secciones (una sola consulta a <c>plataforma</c>).</summary>
    public static Task<ICatalogosDeFicha> CatalogosAsync(IQuerySession global, IEnumerable<(string Seccion, object Datos)> secciones, CancellationToken ct) =>
        ImportacionDeCatalogos.CargarAsync(global, secciones.SelectMany(s => Referencias(s.Seccion, s.Datos)), ct);

    /// <summary>El contenido de la sección: solo la propiedad que le corresponde.</summary>
    public static ContenidoDeSeccionDto Normalizar(object datos, ICatalogosDeFicha catalogos) => datos switch
    {
        DatosGenerales g => new ContenidoDeSeccionDto(General: General(g, catalogos)),
        DatosTributarios t => new ContenidoDeSeccionDto(Tributaria: Tributaria(t, catalogos)),
        DatosLegales l => new ContenidoDeSeccionDto(Legal: Legal(l)),
        DatosFinancieros f => new ContenidoDeSeccionDto(Financiera: Financiera(f)),
        DatosSst s => new ContenidoDeSeccionDto(Sst: new ContenidoSstDto(s.ProgramaSaludOcupacional, s.IdentificaFactoresDeRiesgo, s.ComiteParitario,
            s.ProgramaHigieneYSeguridad, s.GestionAmbiental, s.ReglamentoInterno, s.PoliticaHseq, s.LicenciasYPermisos)),
        DatosEspecialidades e => new ContenidoDeSeccionDto(Especialidades: new ContenidoEspecialidadesDto(
            [.. (e.Especialidades ?? []).Select(c => Codigo(catalogos, CatalogoDePlataforma.Especialidades, c)!)])),
        _ => throw new InvalidOperationException($"Datos de sección no reconocidos: {datos.GetType().Name}.")
    };

    public static ContenidoDeSeccionDto Normalizar(string seccion, JsonElement canonico, ICatalogosDeFicha catalogos) =>
        Normalizar(Datos(seccion, canonico), catalogos);

    /// <summary>El representante legal que va a AyF, como DTO (nulo si no hay).</summary>
    public static RepresentanteLegalErpDto? Representante(RepresentanteLegalPrincipal? r) => r is null
        ? null
        : new RepresentanteLegalErpDto(r.Origen == OrigenDelRepresentante.General ? CodigosDeSeccion.General : CodigosDeSeccion.Legal, r.Indice, r.Nombre,
            r.Persona.Nombres, r.Persona.Apellidos, r.Persona.TipoIdentificacion, r.Persona.Numero, r.Persona.Dv, r.Persona.TipoRepresentacion,
            r.Persona.Cargo, r.Persona.FechaExpedicion, r.Persona.Correo, r.Persona.Telefono);

    private static ContenidoGeneralDto General(DatosGenerales g, ICatalogosDeFicha catalogos)
    {
        var nombre = g.RazonSocial ?? (string.Join(' ', new[] { g.Nombres, g.Apellidos }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } n ? n : null);
        // En persona natural el representante que recibe AyF es el propio tercero (FM-34); en jurídica sale de la legal.
        var representante = g.TipoPersona == Dominio.Terceros.TipoPersona.Natural
            ? Representante(RepresentanteLegalPrincipal.Elegir(g.TipoPersona, g, null))
            : null;
        var d = g.DireccionEstructurada;
        return new ContenidoGeneralDto(g.TipoPersona?.ToString(), g.TipoIdentificacion, g.Numero, g.Dv, nombre, g.RazonSocial, g.Nombres, g.Apellidos,
            g.Direccion,
            d is null ? null : new DireccionEstructuradaDto(d.TipoVia, d.NumeroVia, d.LetraVia, d.Bis, d.Cuadrante, d.NumeroCruce, d.LetraCruce, d.Placa,
                d.Complemento),
            Ciudad(catalogos, g.Ciudad), g.Pais, g.Telefono, g.Correo, Codigo(catalogos, CatalogoDePlataforma.ActividadesEconomicas, g.CiiuPrincipal),
            [.. (g.CiiuSecundarias ?? []).Select(c => Codigo(catalogos, CatalogoDePlataforma.ActividadesEconomicas, c)!)], g.PaginaWeb, g.CertificadoIso,
            g.AnioInicio, representante);
    }

    private static ContenidoTributarioDto Tributaria(DatosTributarios t, ICatalogosDeFicha catalogos)
    {
        var c = CalidadesTributarias.Derivar(t);
        return new ContenidoTributarioDto(
            [.. (t.Responsabilidades ?? []).Select(r => Codigo(catalogos, CatalogoDePlataforma.ResponsabilidadesDian, r)!)],
            new CalidadesTributariasDto(c.ResponsableIva, c.Autorretenedor, c.DeclaranteRenta, c.GranContribuyente, c.AutorretenedorIca, c.RegimenSimple,
                c.GranContribuyenteBogota, c.RegimenEspecial, c.AutorretenedorRft, c.DescuentoSenaFic, c.ProfesionalIndependiente, c.ExentoRetencion,
                c.AgenteRetenedorIva, c.ObligadoAFacturar),
            [
                .. CalidadesTributarias.Tabla
                    .Where(r => r.Origen == OrigenDeCalidad.NoDerivable || (r.Origen == OrigenDeCalidad.DatoDeLaSeccion && t.AutorretenedorIca is null))
                    .Select(r => new CalidadSinDerivarDto(r.Calidad, r.Nota))
            ],
            Resolucion(t.ResolucionGranContribuyente), Resolucion(t.ResolucionAutorretenedor), t.ResponsableIca, t.AutorretenedorIca,
            [.. (t.Ica ?? []).Select(f => new IcaErpDto(Ciudad(catalogos, f.Municipio), Codigo(catalogos, CatalogoDePlataforma.ActividadesEconomicas, f.Ciiu),
                f.TarifaPorMil))]);
    }

    private static ContenidoLegalDto Legal(DatosLegales l) =>
        new(Representante(RepresentanteLegalPrincipal.Elegir(null, null, l)),
            [
                .. (l.Personas ?? []).Select(p => new PersonaRelacionadaDto(p.Rol?.ToString(), p.TipoRepresentacion, p.TipoIdentificacion, p.Numero, p.Dv,
                    p.Nombres, p.Apellidos, p.Cargo, p.Porcentaje, p.Pep, p.VinculoPep, p.FechaExpedicion, p.FechaNacimiento, p.Nacionalidad, p.Residencia,
                    p.Correo, p.Telefono))
            ],
            l.RevisorFiscalFirma is { } firma ? new FirmaRevisoraDto(firma.Nit, firma.RazonSocial) : null);

    private static ContenidoFinancieroDto Financiera(DatosFinancieros f) =>
        new(f.Periodo, f.FechaCorte, f.Activos, f.Pasivos, f.Patrimonio, f.IngresosBrutos, f.IngresosNetos, f.GastosOperacionales, f.GastosNoOperacionales,
            f.OtrosIngresos,
            f.MonedaExtranjera is { } m
                ? new MonedaExtranjeraDto(m.Realiza, [.. (m.Tipos ?? []).Select(x => x.ToString())], [.. m.Paises ?? []])
                : null);

    private static ResolucionDto? Resolucion(Resolucion? r) => r is null ? null : new ResolucionDto(r.Numero, r.Fecha);

    private static CodigoNombreDto? Codigo(ICatalogosDeFicha catalogos, CatalogoDePlataforma catalogo, string? codigo) =>
        codigo is null ? null : new CodigoNombreDto(codigo, catalogos.Buscar(catalogo, codigo)?.Nombre);

    private static CiudadErpDto? Ciudad(ICatalogosDeFicha catalogos, string? codigo)
    {
        if (codigo is null)
            return null;
        var entrada = catalogos.Buscar(CatalogoDePlataforma.Ciudades, codigo);
        return new CiudadErpDto(codigo, entrada?.Nombre, entrada?.Padre ?? codigo[..Math.Min(2, codigo.Length)], entrada?.Departamento, entrada?.Pais);
    }
}

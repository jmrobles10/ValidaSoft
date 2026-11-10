namespace Validasoft.Dominio.Ficha.Portal;

/// <summary>
/// Fila de la tabla inversa de las calidades (FM-40): la columna S/N de <c>dbo.TerInfoTributaria</c>, la calidad de la ficha
/// (<see cref="CalidadesTributarias"/>), el código DIAN de la casilla 53 que da el «sí» y el que da el «no» (si lo hay), y la nota.
/// Sin código ⇒ la calidad no cabe en la casilla 53: si el Portal dice «sí», se anota (advertencia) y no se inventa un código.
/// </summary>
public sealed record ReglaInversaDeCalidad(string CampoPortal, string Calidad, string? CodigoSi, string? CodigoNo, string Nota);

/// <summary>
/// Inverso de <see cref="CalidadesTributarias.Derivar(IEnumerable{string}?, bool?)"/> (F3, FM-33): las calidades S/N del Portal
/// ⇒ códigos DIAN de responsabilidades. Función pura. Lo que se deriva de vuelta de los códigos da lo mismo que el Portal,
/// salvo las incoherencias del legado, que se anotan (p. ej. régimen especial sin declarante).
/// </summary>
public static class CalidadesDelPortal
{
    /// <summary>
    /// La tabla. Elecciones: «responsable de IVA» = no ⇒ 49 (no responsable); «declarante» ⇒ 05 (régimen ordinario) y
    /// «régimen especial» ⇒ 04, que ya implica declarante; «agente retenedor de IVA» ⇒ 09 (el que homologa el OCR del Portal;
    /// el 07 es retención a título de renta y no se usa); <c>NoObligFacturar</c> guarda «obligado a facturar» (semántica
    /// invertida del legado) ⇒ 52 facturador electrónico.
    /// </summary>
    public static readonly IReadOnlyList<ReglaInversaDeCalidad> Tabla =
    [
        new("ResponsableIVA", "responsableIva", "48", "49", "Sí ⇒ 48 (IVA); no ⇒ 49 (no responsable de IVA)."),
        new("Autorretenedor", "autorretenedor", "15", null, "Sí ⇒ 15 (autorretenedor)."),
        new("Declarante", "declaranteRenta", "05", null, "Sí ⇒ 05 (renta régimen ordinario), salvo con régimen especial: el 04 ya lo implica."),
        new("GranContribuyente", "granContribuyente", "13", null, "Sí ⇒ 13 (gran contribuyente)."),
        new("AutoRetenedorICA", "autorretenedorIca", null, null, "Va al dato autorretenedor_ica (ICA es municipal; no está en la casilla 53)."),
        new("RegimenSimple", "regimenSimple", "47", null, "Sí ⇒ 47 (régimen simple de tributación)."),
        new("GranContribuyenteBta", "granContribuyenteBogota", null, null, "Calidad distrital de Bogotá: sin código en la casilla 53."),
        new("RegimenEspecial", "regimenEspecial", "04", null, "Sí ⇒ 04 (renta régimen tributario especial; implica declarante)."),
        new("AutoretenedorRFT", "autorretenedorRft", null, null, "La autorretención especial de renta (Decreto 2201 de 2016) no tiene código propio."),
        new("DescuentoSENAFIC", "descuentoSenaFic", null, null, "Aportes parafiscales SENA/FIC: sin código en la casilla 53."),
        new("ProfesionalIndependiente", "profesionalIndependiente", null, null, "Condición del pago, no una responsabilidad del RUT."),
        new("ExentoRetencionFuente", "exentoRetencion", null, null, "La exención la soporta un certificado o la ley, no la casilla 53."),
        new("AgenteRetenedorIVA", "agenteRetenedorIva", "09", null, "Sí ⇒ 09 (retención en la fuente en el impuesto sobre las ventas)."),
        new("NoObligFacturar", "obligadoAFacturar", "52", null,
            "El Portal guarda «obligado a facturar» en NoObligFacturar (semántica invertida): sí ⇒ 52 (facturador electrónico).")
    ];

    /// <summary>Resultado: códigos DIAN (ordenados, sin repetir), <c>autorretenedor_ica</c> y las notas (calidad, mensaje) de lo que no tiene código.</summary>
    public sealed record Traduccion(IReadOnlyList<string> Responsabilidades, bool AutorretenedorIca, IReadOnlyList<(string Campo, string Mensaje)> Notas);

    public static Traduccion Traducir(TerInfoTributaria t)
    {
        var valores = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["ResponsableIVA"] = t.ResponsableIVA, ["Autorretenedor"] = t.Autorretenedor, ["Declarante"] = t.Declarante,
            ["GranContribuyente"] = t.GranContribuyente, ["AutoRetenedorICA"] = t.AutoRetenedorICA, ["RegimenSimple"] = t.RegimenSimple,
            ["GranContribuyenteBta"] = t.GranContribuyenteBta, ["RegimenEspecial"] = t.RegimenEspecial, ["AutoretenedorRFT"] = t.AutoretenedorRFT,
            ["DescuentoSENAFIC"] = t.DescuentoSENAFIC, ["ProfesionalIndependiente"] = t.ProfesionalIndependiente,
            ["ExentoRetencionFuente"] = t.ExentoRetencionFuente, ["AgenteRetenedorIVA"] = t.AgenteRetenedorIVA, ["NoObligFacturar"] = t.NoObligFacturar
        };

        var codigos = new SortedSet<string>(StringComparer.Ordinal);
        var notas = new List<(string, string)>();
        foreach (var regla in Tabla)
        {
            var valor = valores[regla.CampoPortal];
            if (regla.CampoPortal == "Declarante" && t.RegimenEspecial)
                continue; // el 04 del régimen especial ya lo implica
            if (valor && regla.CodigoSi is { } si)
                codigos.Add(si);
            else if (!valor && regla.CodigoNo is { } no)
                codigos.Add(no);
            else if (valor && regla.CodigoSi is null && regla.CampoPortal != "AutoRetenedorICA")
                notas.Add((regla.CampoPortal, $"«{regla.CampoPortal}» = sí no tiene código DIAN: no se lleva a las responsabilidades. {regla.Nota}"));
        }

        if (t.RegimenEspecial && !t.Declarante)
            notas.Add(("Declarante", "El Portal dice régimen especial sin declarante: el 04 implica declarante de renta."));
        return new Traduccion([.. codigos], t.AutoRetenedorICA, notas);
    }
}

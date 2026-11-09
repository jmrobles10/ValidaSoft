using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Expedientes;

/// <summary>Formato de folios (T-9): <c>{PrefijoEmpresa}-E-{Año}-{Secuencia}</c> con la secuencia en 6 dígitos.</summary>
public static class Folios
{
    public static string DeExpediente(string prefijoEmpresa, int anio, long secuencia)
    {
        if (string.IsNullOrWhiteSpace(prefijoEmpresa))
            throw ReglaDeNegocioException.Validacion("prefijo-folio-invalido", "La empresa no tiene prefijo de folio.");
        if (secuencia < 1)
            throw new ArgumentOutOfRangeException(nameof(secuencia), "La secuencia empieza en 1.");

        return $"{prefijoEmpresa}-E-{anio}-{secuencia:D6}";
    }
}

/// <summary>
/// Último número usado por <c>(empresa, tipo de folio, año)</c> (esquema <c>validacion</c>, multi-tenant). Se guarda con
/// concurrencia optimista en la misma transacción que el expediente: si dos aperturas leen el mismo valor, la segunda
/// falla y se reintenta; si la transacción se revierte, el número no se consume. Así no hay huecos ni duplicados.
/// </summary>
public sealed class ContadorDeFolio
{
    public string Id { get; set; } = string.Empty;

    public Guid EmpresaId { get; set; }

    public int Anio { get; set; }

    public long Ultimo { get; set; }

    public static string Clave(Guid empresaId, string tipo, int anio) => $"{empresaId:N}|{tipo}|{anio}";

    public static ContadorDeFolio Nuevo(Guid empresaId, string tipo, int anio) =>
        new() { Id = Clave(empresaId, tipo, anio), EmpresaId = empresaId, Anio = anio };

    public long Siguiente() => ++Ultimo;
}

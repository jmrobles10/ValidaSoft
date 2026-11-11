using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>
/// Contactos del tercero con cada empresa (IO-3): el mismo tercero tiene contactos distintos para cada empresa, cada uno con su
/// nombre, cargo, correo, teléfono y rol (comercial, pagos, cumplimiento, notificaciones). Los edita la empresa desde el vínculo y el
/// tercero desde su ficha; salen en el vínculo (<c>GET /vinculos/{id}</c>), en la ficha para el ERP y en el evento
/// <c>vinculo.contactos_actualizados</c>. El cambio reemplaza la lista completa y es idempotente.
/// </summary>
public static class ContactosDeVinculoEndpoints
{
    /// <summary>
    /// La empresa reemplaza los contactos del tercero con ella (<c>vinculos:escribir</c>). 422 <c>contactos-demasiados</c>,
    /// <c>contacto-nombre-requerido</c>, <c>contacto-demasiado-largo</c>, <c>correo-invalido</c>, <c>contacto-rol-invalido</c>,
    /// <c>contacto-repetido</c>; 409 <c>vinculo-anulado</c>.
    /// </summary>
    [WolverinePut("/api/v1/vinculos/{id}/contactos")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VinculoDto> Actualizar(Guid id, ActualizarContactosDto dto, SesionDeEmpresa empresa, TimeProvider reloj, CancellationToken ct)
    {
        var stream = await empresa.VinculoParaEscribirAsync(id, null, ct);
        if (stream.Aggregate!.ActualizarContactos(dto.Contactos.ADominio(), reloj.GetUtcNow()) is not { } evento)
        {
            var actual = await empresa.CargarVinculoAsync(id, ct);
            return actual.ADto(await empresa.Sesion.LoadAsync<TipoVinculo>(actual.TipoVinculoId, ct));
        }

        return await VinculosEndpoints.GuardarAsync(empresa, stream, evento, ct);
    }
}

/// <summary>El tercero edita sus contactos con cada empresa desde su ficha (sesión de tercero; solo sus vínculos, el resto 404).</summary>
public static class ContactosPublicosEndpoints
{
    /// <summary>
    /// El tercero reemplaza los contactos que tiene con la empresa de ese vínculo y recibe la lista resultante. Solo después de aceptar
    /// la invitación (409 <c>invitacion-no-aceptada</c>): hasta entonces la empresa no ve nada de él (B-1). Mismas validaciones que la
    /// empresa.
    /// </summary>
    [WolverinePut("/public/v1/vinculos/{id}/contactos")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IReadOnlyList<ContactoDeEmpresaDto>> Actualizar(Guid id, ActualizarContactosDto dto, HttpContext http, ISesiones sesiones,
        TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var enSesion = tercero.VinculoRequerido(id);
        var nuevos = dto.Contactos.ADominio();

        return await Peticiones.ConReintentosAsync<IReadOnlyList<ContactoDeEmpresaDto>>(async () =>
        {
            await using var sesion = sesiones.ParaTenant(enSesion.TenantId);
            var stream = await sesion.Events.FetchForWriting<Vinculo>(id, ct);
            if (stream.Aggregate is not { } vinculo || vinculo.TerceroId != tercero.TerceroId)
                throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo.");
            if (!vinculo.InvitacionAceptada)
                throw ReglaDeNegocioException.Conflicto("invitacion-no-aceptada", "Acepta la invitación de la empresa antes de indicarle tus contactos.");

            if (vinculo.ActualizarContactos(nuevos, reloj.GetUtcNow()) is not { } evento)
                return vinculo.Contactos.ADto();

            stream.AppendOne(evento);
            await sesion.SaveChangesAsync(ct);
            return evento.Despues.ADto();
        }, 5);
    }
}

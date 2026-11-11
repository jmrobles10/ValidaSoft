namespace Validasoft.Aplicacion.Verificaciones;

/// <summary>
/// La ficha legal del tercero tiene una versión nueva (CU-15): puede haber cambiado su representante legal, un suplente, un socio o
/// quien se declaró PEP. Lo publica el guardado de la sección en el outbox de la bóveda; el manejador expira las consultas de
/// listas de las empresas que ya no cubren a las personas actuales.
/// </summary>
public sealed record PersonasDeFichaCambiaron(Guid TerceroId);

public static class PersonasDeFichaCambiaronHandler
{
    public static Task Handle(PersonasDeFichaCambiaron mensaje, TareasDeVerificaciones tareas, CancellationToken ct) =>
        tareas.ExpirarPorCambioDePersonasAsync(mensaje.TerceroId, ct);
}

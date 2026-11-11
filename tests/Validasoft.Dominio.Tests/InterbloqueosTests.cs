using Npgsql;
using Validasoft.Aplicacion.Comun;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Reintento de lo que PostgreSQL aborta por un interbloqueo o una llave foránea: el reinicio del sandbox borra flujos de eventos mientras webhooks y
/// procesos en segundo plano les agregan eventos, y cuando los bloqueos se cruzan la base aborta una de las dos transacciones (40P01).
/// </summary>
public class InterbloqueosTests
{
    private static PostgresException Error(string estado) => new("error", "ERROR", "ERROR", estado);

    [Fact]
    public void Reconoce_el_interbloqueo_y_el_fallo_de_serializacion_aunque_vengan_envueltos()
    {
        Peticiones.EsInterbloqueo(Error(PostgresErrorCodes.DeadlockDetected)).ShouldBeTrue();
        Peticiones.EsInterbloqueo(Error(PostgresErrorCodes.SerializationFailure)).ShouldBeTrue();
        Peticiones.EsInterbloqueo(new InvalidOperationException("Marten", Error(PostgresErrorCodes.DeadlockDetected))).ShouldBeTrue();
        Peticiones.EsInterbloqueo(new AggregateException(new InvalidOperationException("otro"), Error(PostgresErrorCodes.DeadlockDetected))).ShouldBeTrue();
    }

    [Fact]
    public void No_confunde_otros_errores_con_un_interbloqueo()
    {
        Peticiones.EsInterbloqueo(Error(PostgresErrorCodes.UniqueViolation)).ShouldBeFalse();
        Peticiones.EsInterbloqueo(Error(PostgresErrorCodes.ForeignKeyViolation)).ShouldBeFalse();
        Peticiones.EsInterbloqueo(new InvalidOperationException("otro")).ShouldBeFalse();
        Peticiones.EsInterbloqueo(new TimeoutException()).ShouldBeFalse();
    }

    [Fact]
    public void Un_borrado_masivo_tambien_reintenta_cuando_otro_proceso_agrego_un_evento_al_flujo_que_iba_a_borrar()
    {
        Peticiones.EsConflictoDeBorrado(Error(PostgresErrorCodes.DeadlockDetected)).ShouldBeTrue();
        Peticiones.EsConflictoDeBorrado(Error(PostgresErrorCodes.ForeignKeyViolation)).ShouldBeTrue();
        Peticiones.EsConflictoDeBorrado(new InvalidOperationException("Marten", Error(PostgresErrorCodes.ForeignKeyViolation))).ShouldBeTrue();
        Peticiones.EsConflictoDeBorrado(Error(PostgresErrorCodes.UniqueViolation)).ShouldBeFalse();
        Peticiones.EsConflictoDeBorrado(new InvalidOperationException("otro")).ShouldBeFalse();
    }

    [Fact]
    public async Task Repite_la_operacion_hasta_que_deja_de_fallar()
    {
        var intentos = 0;
        await Peticiones.ReintentarSiHayConflictoAsync(() =>
        {
            intentos++;
            return intentos < 4 ? Task.FromException(Error(PostgresErrorCodes.DeadlockDetected)) : Task.CompletedTask;
        });
        intentos.ShouldBe(4);
    }

    [Fact]
    public async Task Un_error_distinto_se_propaga_sin_repetir_y_el_ultimo_intento_tambien()
    {
        var intentos = 0;
        await Should.ThrowAsync<InvalidOperationException>(() => Peticiones.ReintentarSiHayConflictoAsync(() =>
        {
            intentos++;
            return Task.FromException(new InvalidOperationException("no es un interbloqueo"));
        }));
        intentos.ShouldBe(1);

        intentos = 0;
        await Should.ThrowAsync<PostgresException>(() => Peticiones.ReintentarSiHayConflictoAsync(() =>
        {
            intentos++;
            return Task.FromException(Error(PostgresErrorCodes.DeadlockDetected));
        }, intentos: 3));
        intentos.ShouldBe(3);
    }
}

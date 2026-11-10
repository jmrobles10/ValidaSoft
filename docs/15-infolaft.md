# 15 — Aprovechar la suscripción de Infolaft

> **Estado:** propuesta (2026-11-10). **Quién decide:** Producto con Cumplimiento y Operaciones.
> **Contexto:** Sinco paga una suscripción de Infolaft (listas restrictivas SARLAFT/SAGRILAFT) que hoy nadie usa. Este
> documento explica por qué y propone cómo aprovecharla desde Validasoft (hito H7, [11 §H7](11-trazabilidad-por-hito.md)).

## 1. Qué hay hoy

| Hallazgo | Evidencia | Verificado |
|---|---|---|
| El SRM tiene la URL de Infolaft configurada y un `HttpClient` registrado, pero **ningún código lo usa**: no hay adaptador, DTO, SQL ni pantalla. | `SRM\BackEnd\SRM.API\appsettings.json:10` (`"infolab"`), `SRM.API\ServicesHTTP\httpclientServices.cs:23-28` (commit `f20456eb8`, 2024-09-30); `git log -S 'CreateClient("InfoLabService")'` no da resultados | Sí |
| **Infolaft no se puede elegir.** En el SRM el proveedor SARLAFT lo elige `adpconfig.AplicaSarlaft`, con valores 1 Compliance, 2 Stradata, 3 Informa, 4 Auco, 6 TusDatos, 7 Inspektor y 8 TransUnion. Cualquier otro valor responde «no configurado». | `SRM.Services.Proveedores\Integraciones\ApiSagrilaft.cs:101-132` | Sí |
| La única prueba real de la API es una página HTML de prueba en ADPRO y ADPRO-jerarquia. Autentica con Basic (usuario de servicio) y llama `Search/searchPaginated` por nombre exacto. | `ADPRO\Web\Sitio\Views\Test\TestInfoLab.html` | Sí |
| **Esa página tiene la contraseña del usuario de servicio en claro**, versionada en dos repositorios. | ídem, líneas 70-72 y 88 | Sí |
| El log de observabilidad busca `infolab` en el host, pero el host real es `infolaftsearch.com`: nunca coincidiría. | `SRM.Infrastructure\Observability\Handlers\HttpObservabilityHandler.cs:146` | Sí |
| Infolaft ofrece búsquedas individuales y masivas por nombre y cédula, PEP, más de 7.800 fuentes, monitoreo con alertas de lo consultado en el último año y trazabilidad. Los planes Profesional y Enterprise incluyen API y consultas ilimitadas. | https://infolaftsearch.co/ · https://infolaftsearch.co/planes/ · https://www.infolaft.com/productos/listas-sagrilaft-y-sarlaft-para-prevenir-lavado-de-activos | Web pública |
| No hay documentación técnica pública: endpoints, formato de respuesta, PDF por API, webhooks y límites. | `/infolaft/v2/api-docs` responde 404 | Supuesto |

**Por qué nadie la usa:** la integración se empezó en 2024 (URL y cliente) y nunca se terminó. Sin adaptador ni valor
en `AplicaSarlaft`, ningún cliente pudo seleccionarla. Luego el esfuerzo se fue a TusDatos, Inspektor y TransUnion.

## 2. Acciones inmediatas (fuera de Validasoft)

1. **Rotar la contraseña del usuario de servicio de Infolaft** y borrar `TestInfoLab.html` de ADPRO y ADPRO-jerarquia,
   incluido su historial si el repositorio es accesible fuera del equipo. Confirmar que `Views/Test` no se publica.
2. Pedir a Infolaft:
   - el plan exacto contratado;
   - la documentación de la API: búsqueda por documento, masiva, monitoreo y webhooks, certificado PDF por consulta,
     límites de velocidad y si se puede buscar por POST con cuerpo (hoy el nombre va en la URL);
   - un usuario de pruebas.

## 3. Propuesta en Validasoft

- **Proveedor principal de listas para Sinco: Infolaft.** Se implementa detrás de `IVerificadorDeListas` (H7), con el
  proveedor simulado para desarrollo y pruebas y los demás adaptadores (Stradata, Compliance) opcionales.
- **Credenciales por empresa.** La configuración de cada empresa y proveedor guarda usuario, clave cifrada con
  `IProtectorDeSecretos` (como la API key de OpenAI), URL, plan y cuota mensual opcional. La clave nunca vuelve en
  claro; se muestran solo los últimos cuatro caracteres. Es por empresa aunque hoy la suscripción sea de Sinco, para que
  otro cliente pueda traer su propia cuenta.
- **Cuándo consultar:**
  - al activarse la verificación del expediente, después de la invitación y del consentimiento del tercero (ADR-0009):
    al tercero, a sus representantes y socios de la ficha, y a los PEP;
  - antes de aprobar, si la consulta tiene más días que su vigencia;
  - en monitoreo de los vínculos activos, con una reconsulta periódica en lote (mensual, configurable), que además
    mantiene a esos terceros dentro de la ventana de alertas de Infolaft;
  - cuando cambia un representante legal.
- **Evidencia.** Se guarda el JSON de la respuesta con su hash y un PDF propio de la consulta (fecha, quién o el sistema,
  criterio, listas, coincidencias), retenido 10 años (P-14-1). Una coincidencia va a la bandeja de Cumplimiento y nunca
  se aprueba sola.
- **Medir el uso de la suscripción.** Un contador de consultas por empresa, proveedor y mes, idempotente por referencia
  para no contar reintentos, y un tablero de consumo contra el plan. Sirve para justificar o renegociar el contrato.
- **Resiliencia.** Concurrencia máxima configurable, timeouts, circuit breaker y reintentos idempotentes.
- **Riesgos:**
  - búsqueda por nombre exacto (falsos positivos y negativos): pedir la búsqueda por documento;
  - límites desconocidos;
  - autenticación por cookie frágil (preferir Basic);
  - datos personales en la URL (Habeas Data, Ley 1581);
  - costo si el plan contratado no incluye API.

## 4. Pasos priorizados

1. Rotar la credencial y borrar la página de prueba (Operaciones, hoy).
2. Pedir a Infolaft el plan, la documentación y el usuario de pruebas (Producto).
3. En Validasoft H7: verificador con proveedor simulado y evidencia manual, adaptador de Infolaft con configuración
   cifrada por empresa y contador de uso (en curso, rama `feature/verificaciones`, filas VE del registro).
4. Validar el adaptador contra el ambiente de pruebas de Infolaft y ajustar el parser al formato real.
5. Monitoreo y reconsulta de los vínculos activos, y tablero de consumo.
6. Opcional, durante la coexistencia: si el SRM debe usarla antes de migrar, agregar `AplicaSarlaft = 5` con un
   `ApiInfolaft` que reutilice `InfoLabService`, y corregir el filtro del log.

## Decisiones abiertas

1. ¿Infolaft será el proveedor único para todos los clientes de Sinco o cada cliente elige el suyo? Propuesta: por
   empresa, con Infolaft por defecto cuando el cliente no tenga contrato propio.
2. ¿Se usa en el SRM durante la coexistencia (paso 6) o solo desde Validasoft? Propuesta: solo desde Validasoft, salvo
   que un cliente lo necesite antes de migrar.

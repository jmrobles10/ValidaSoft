# Registros de decisiones de arquitectura (ADR)

Cada decisión estructural se registra una vez y no se reescribe: si cambia, se crea un ADR nuevo que *reemplaza* al anterior y ambos quedan enlazados.

| ADR | Título | Estado |
|---|---|---|
| [0001](0001-monolito-modular.md) | Monolito modular con dos roles de host (Api y Worker) | Aceptado |
| [0002](0002-marten-wolverine-event-sourcing.md) | Marten + Wolverine sobre PostgreSQL para event sourcing, mensajería y outbox | Aceptado |
| [0003](0003-openiddict-identidad-propia.md) | OpenIddict embebido + ASP.NET Identity en lugar de Keycloak | Aceptado |
| [0004](0004-boveda-global-y-tenancy-conjoined.md) | Bóveda global del tercero (`boveda`) + multi-tenant conjoined (`validacion`) | Aceptado |
| [0005](0005-version-unica-y-revalidacion.md) | Una sola versión vigente por documento global, compartición automática y revalidación obligatoria | Aceptado |
| [0006](0006-dos-apps-react.md) | Dos aplicaciones React (`web` y `public`) con paquetes compartidos | Aceptado |
| [0007](0007-rest-webhooks-sse-sin-grpc.md) | API REST + webhooks firmados + SSE; sin gRPC en el MVP | Aceptado |
| [0008](0008-tenant-grupo-empresas-internas.md) | Tenant = grupo empresarial con empresas internas aisladas (precisa 0004) | Aceptado |
| [0009](0009-consentimiento-antes-de-compartir.md) | Consentimiento antes de compartir: la bóveda se abre a una empresa solo al aceptar su invitación (precisa 0005) | Aceptado |
| [0010](0010-personas-relacionadas-con-boveda-propia.md) | Personas relacionadas (representantes, socios, UBO, miembros) son terceros con bóveda propia (precisa 0004) | Aceptado |

Plantilla: `Contexto · Decisión · Alternativas descartadas · Consecuencias · Cómo se revierte`.

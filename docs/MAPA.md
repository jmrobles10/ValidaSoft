# Mapa del proyecto: cómo leer los documentos y cómo fluye el desarrollo

| Estado | Fecha | Responsable |
|---|---|---|
| Vigente | 2026-10-28 | Arquitectura |

**Propósito.** Orientar a quien llega al repositorio: en qué orden leer según su rol, cómo se relacionan los documentos entre sí, y cómo avanza el desarrollo de hito en hito con sus puertas de aprobación y sus bucles de corrección.

---

## 1. Flujo de lectura

### 1.1 Lectura mínima común (todos, ~40 min)

```mermaid
flowchart LR
  R[README<br/>reglas de oro] --> A[00 Visión y alcance<br/>por qué, para quién, qué entra]
  A --> B[02 Modelo documental compartido<br/>el corazón: bóveda, versión única,<br/>decisiones por empresa, vigencias]
  B --> C[10 Glosario<br/>vocabulario y equivalencias con el legado]
  C --> D[decisiones/registro.md<br/>qué se decidió y por qué]
  D --> E[09 Plan de hitos<br/>qué se construye y cuándo]
```

### 1.2 Lectura por rol (después de la mínima común)

```mermaid
flowchart TD
  subgraph P[Producto / Negocio]
    P1[01 Análisis del legado<br/>qué se reemplaza] --> P2[11 Trazabilidad por hito<br/>qué entrega cada hito] --> P3[decisiones/cuestionario.md<br/>las 97 preguntas y sus opciones]
  end
  subgraph AR[Arquitecto / Tech lead]
    A1[03 Dominio y eventos<br/>+ Anexo A semilla canónica] --> A2[04 Arquitectura de plataforma] --> A3[05 API, contratos y webhooks] --> A4[07 Seguridad, identidad y tenancy] --> A5[adr/0001–0010<br/>0009 consentimiento · 0010 personas relacionadas] --> A6[11 Trazabilidad]
  end
  subgraph BE[Backend A · dominio]
    B1[03 Dominio y eventos<br/>+ Anexo A] --> B2[05 API y contratos] --> B3[11 Trazabilidad · hitos H3, H4, H6]
  end
  subgraph BB[Backend B · documentos e integración]
    C1[04 Arquitectura] --> C2[05 API y contratos] --> C3[07 Seguridad] --> C4[08 Integración y migración] --> C5[11 · H1, H2, H5, H7, H9, H10]
  end
  subgraph FE[Frontend A / B]
    F1[06 Frontend y UX] --> F2[05 API · contratos de lectura y SSE] --> F3[11 · pantallas por hito]
  end
  subgraph IN[Integrador SRM / Portal / Marco]
    I1[01 Análisis del legado · mapa de endpoints] --> I2[05 API · webhooks y SDK] --> I3[08 Integración y migración]
  end
  subgraph QA[QA / DevOps]
    Q1[04 · testing, compose, CI] --> Q2[09 · criterios de aceptación] --> Q3[11 · pruebas por hito]
  end
  subgraph LG[Legal / Cumplimiento]
    L1[07 · retención, habeas data, consentimiento IA] --> L2[02 §10.3 retención] --> L3[registro · P-6-5, P-14-x, P-3-5]
  end
```

### 1.3 Cómo se relacionan los documentos

```mermaid
flowchart TB
  REG[(decisiones/registro.md<br/>fuente de las decisiones)]
  ADR[(adr/0001–0010<br/>por qué estructural)]
  D00[00 Visión y alcance]
  D01[01 Análisis del legado]
  D02[02 Modelo documental]
  D03[03 Dominio y eventos<br/>DUEÑO de enums, eventos, proyecciones]
  D04[04 Plataforma]
  D05[05 API<br/>DUEÑO de endpoints, DTOs, eventos públicos, alertas]
  D06[06 Frontend]
  D07[07 Seguridad]
  D08[08 Integración]
  D09[09 Hitos]
  D10[10 Glosario]
  D11[11 Trazabilidad<br/>une 03+05+06 con 09]

  D01 --> D00
  REG --> D00 & D02 & D03 & D04 & D05 & D06 & D07 & D08 & D09
  ADR <--> D02 & D03 & D04 & D07
  D02 --> D03
  D03 --> D05 --> D06
  D03 --> D04
  D04 --> D07
  D05 --> D08
  D07 --> D08
  D03 & D05 & D06 & D09 --> D11
  D03 --> D10
  D11 --> D09
```

Regla práctica: si dos documentos dicen cosas distintas, manda **03** para el dominio y **05** para los contratos; el **registro** manda sobre ambos; un cambio de decisión siempre pasa por el registro y, si es estructural, por un ADR nuevo.

---

## 2. Mapa del desarrollo

### 2.1 Flujo general (del legado al producto en operación)

```mermaid
flowchart LR
  L[Legado SRM + Portal] -->|análisis 01| H0
  subgraph DOC[Hito 0 · Documentación]
    H0[Docs 00–11 + ADRs] --> Q[Cuestionario 97 preguntas] --> REG[Registro de decisiones] --> RL[Relectura cruzada] --> OK0{Producto aprueba<br/>00, 02, 09, 11}
  end
  OK0 -->|sí| H1
  OK0 -->|no| H0
  subgraph BASE[Base]
    H1[H1 Fundación<br/>tenant·empresa, identidad,<br/>medición, CI, design system]
    H2[H2 Bóveda del tercero<br/>versiones, hechos, compartición, IA]
    H3[H3 Configuración y exigencia<br/>catálogo, flujos, reglas, selección de flujo]
    H1 --> H2
    H1 --> H3
  end
  subgraph MVP[MVP]
    H4[H4 Expediente y veredictos<br/>áreas, etapas, observar, override]
    H5[H5 Certificado, alertas, webhooks]
    H2 --> H4
    H3 --> H4
    H4 --> H5
  end
  H5 --> OKM{Demo MVP<br/>5 pasos de la marca}
  subgraph PAR[Paridad con el legado]
    H6[H6 Vigencias y revalidación<br/>dos capas, Desactualizada, cuentas designadas]
    H7[H7 Verificaciones y revisión automática<br/>listas, cumplimiento, UBO/PEP, niveles IA]
    H8[H8 Operación masiva e informes<br/>lotes, campañas, auditoría exportable, planes]
    OKM --> H6
    OKM --> H7
    H6 --> H8
    H7 --> H8
  end
  subgraph INT[Integración y migración]
    H9[H9 SDKs + SRM/Portal/Marco]
    H10[H10 Importador y piloto<br/>corte por empresa, inversa 90 días]
    H11[H11 Endurecimiento<br/>carga, backups, retiro del legado]
    H6 --> H9
    H7 -->|"SAGRILAFT antes del flag"| H9
    H5 -.contratos v1.-> H9
    H9 --> H10 --> H11
  end
  H11 --> OPS[Producto en operación<br/>legado apagado tenant a tenant]
```

### 2.2 Qué entra y qué sale de cada hito

```mermaid
flowchart TB
  subgraph E1[H1 Fundación]
    direction LR
    I1[Entra: 04, 07, ADR-0001/0002/0003/0008] --> O1[Sale: repo ejecutable, tenant+empresa, login 2FA,<br/>API keys, medición de uso, GitHub Actions, tema UI]
  end
  subgraph E2[H2 Bóveda]
    direction LR
    I2[Entra: 02, 03 §Documento, 05 §bóveda, semilla P-2-3] --> O2[Sale: Tercero multipaís, versiones, hechos sin/con IA,<br/>VersionActualCambiada, compartición, BovedaTercero]
  end
  subgraph E3[H3 Configuración]
    direction LR
    I3[Entra: 03 §Configuración, 05 §flujos, 06 editor] --> O3[Sale: catálogo tenant, FlujoVersion, reglas de exigencia<br/>y de selección de flujo, áreas, motivos, auditoría de configuración]
  end
  subgraph E4[H4 Expediente]
    direction LR
    I4[Entra: O2 + O3, 03 §Vinculo/Expediente, 06 bandejas] --> O4[Sale: expediente inicial, veredictos por requisito y área,<br/>observar/reenviar, override, bloqueo, timeline, apps web+public]
  end
  subgraph E5[H5 MVP]
    direction LR
    I5[Entra: O4, 05 §certificados/webhooks/alertas] --> O5[Sale: certificado por vínculo, verificador público,<br/>catálogo de alertas, webhooks firmados + pull, eventos v1 congelados]
  end
  subgraph E6[H6 Vigencias]
    direction LR
    I6[Entra: O5, 02 §7–8, 03 §vigencias] --> O6[Sale: vigencia en dos capas, avisos consolidados,<br/>revalidación obligatoria + bloqueo a 15 días, ratificación, cuentas designadas]
  end
  subgraph E7[H7 Cumplimiento]
    direction LR
    I7[Entra: O5, 04 §IA, 03 §verificaciones] --> O7[Sale: listas restrictivas, estudio de cumplimiento, UBO/PEP,<br/>niveles Manual/Asistido/Automático, excepciones, equidad]
  end
  subgraph E8[H8 Operación]
    direction LR
    I8[Entra: O6 + O7] --> O8[Sale: lotes, campañas, delegación, ficha 360, informes,<br/>auditoría exportable, habeas data, planes, marca blanca, embed]
  end
  subgraph E9[H9 Integración]
    direction LR
    I9[Entra: O5 contratos, O6, O7, 08] --> O9[Sale: SDK .NET/TS, SRM con flag y receptor,<br/>Portal embebido, Marco con consulta y respaldo]
  end
  subgraph E10[H10 Migración]
    direction LR
    I10[Entra: O9, 08 §importador, datos legado] --> O10[Sale: importador idempotente, piloto por empresa,<br/>sincronización inversa, rollback ensayado]
  end
  E1 --> E2 & E3 --> E4 --> E5 --> E6 & E7 --> E8
  E6 & E7 --> E9 --> E10
```

### 2.3 El ciclo dentro de cada hito

```mermaid
flowchart LR
  A[Issue por entregable<br/>GitHub Projects · épica = hito] --> B[Rama feature/H n -tema]
  B --> C[Implementar + pruebas<br/>unit · integración · contract · e2e]
  C --> D[PR: 2 aprobaciones + CI verde<br/>OpenAPI vs 11 trazabilidad]
  D --> E{¿Tocó un contrato,<br/>estado o decisión?}
  E -->|sí| F[Actualizar 03/05/06 + 11<br/>y registro/ADR si aplica]
  E -->|no| G
  F --> G[Merge a main]
  G --> H{Fin del hito}
  H -->|criterios + demo + docs| I[Revisión con Producto<br/>semanal / cierre de hito]
  I -->|cambia el alcance| J[Registro de decisión<br/>→ efectos hacia atrás y adelante en 09 §5]
  I -->|aprobado| K[Siguiente hito]
  J --> K
```

### 2.4 Ciclo de una decisión

```mermaid
flowchart LR
  P[Pregunta nueva<br/>o cambio de criterio] --> Q{¿Está en el<br/>cuestionario / registro?}
  Q -->|sí, cerrada| R[Se respeta; reabrir exige ADR]
  Q -->|no| S[Producto decide<br/>opciones + recomendación]
  S --> T[Fila en registro.md<br/>id, fecha, decisión, origen]
  T --> U{¿Estructural?}
  U -->|sí| V[ADR nuevo que reemplaza/precisa]
  U -->|no| W
  V --> W[Propagar a 00–11<br/>relectura cruzada]
  W --> X[Trazar en 11 y en el hito afectado]
```

---

## 3. Dónde está cada cosa (índice rápido)

| Busco… | Documento |
|---|---|
| Por qué existe el producto y qué no hace | 00 |
| Cómo funciona hoy el SRM/Portal y qué se apaga | 01 |
| Qué pasa con un RUT compartido por tres empresas | 02 §2, §7, §11 |
| Cuándo empieza a ver una empresa la bóveda del tercero | 02 §6.1, ADR-0009 |
| Cómo se modela el representante legal, los socios o el beneficiario final | 02 §4.5, 03 §4.5, ADR-0010 |
| Qué códigos de documento existen y sus defaults | 03 Anexo A |
| Qué pasa cuando un documento vence para una empresa y no para otra | 02 §8 |
| Cuándo la IA aprueba sola | 02 §9, 04 §IA |
| Estados y eventos exactos | 03 §3 |
| Quién aprueba, en qué orden, con cuántos aprobadores | 03 §etapas/áreas, registro R-2 |
| Cómo se elige el flujo de un expediente | 03 §ReglaDeSeleccionDeFlujo, 11 H3 |
| Un endpoint, un DTO, un webhook, una alerta | 05 (catálogos), 11 (por hito) |
| Qué ve el tercero / el revisor / el verificador | 06 |
| Quién puede ver qué, retención, consentimiento IA | 07 |
| Cómo se conecta el SRM / Portal / Marco y cómo se migra | 08 |
| Qué se entrega en cada hito y quién responde | 09, 11 |
| Qué significa una palabra | 10 |
| Qué se decidió, cuándo y por qué | decisiones/registro.md, adr/ |
| Qué se corrigió en la relectura | decisiones/relectura-2026-10-28.md |

## Decisiones abiertas
Ninguna propia; ver `decisiones/relectura-2026-10-28.md` para la lista consolidada.

## Fuentes verificadas
Ver 01 y 09.

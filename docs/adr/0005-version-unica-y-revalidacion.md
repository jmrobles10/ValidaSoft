# ADR-0005 — Una sola versión vigente por documento global, compartición automática y revalidación obligatoria

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-10-28 (corregido por objeción del usuario el mismo día) | — |

## Contexto
Primera propuesta: cada empresa "aceptaba" la versión que quisiera y una versión nueva solo se *proponía*. Objeción del usuario: un RUT es un hecho del tercero; si subió uno nuevo, que A siga trabajando con el anterior es información no verídica. Además, en el Portal actual todas las constructoras ven el mismo documento, y la privacidad de los documentos financieros se resolvió aceptando compartición automática (decisión del usuario).

## Decisión
1. Un documento global tiene **una sola versión vigente** por tercero y tipo canónico.
2. Una versión nueva que pasa los **hechos compartidos** (tipo correcto, legible, antivirus, hash distinto; identidad coincidente según D-3) pasa a ser la actual y se **comparte automáticamente** con todos los vínculos **cuya invitación el tercero ya aceptó** y que exigen ese tipo (precisión de ADR-0009: nada se comparte con una empresa antes de `InvitacionAceptada` + consentimientos); **el tercero no puede excluir a ninguna empresa** (A-8): la compartición solo se **congela** hacia adelante por desvinculación o bloqueo del vínculo, y lo ya compartido sigue legible por retención.
3. La bóveda emite el **hecho** `VersionActualCambiada(CamposCambiados, CamposCriticosCambiados)` (T-7). Cada empresa afectada decide: revalida (siempre en archivos; en formularios si cambió un crítico canónico o uno de sus campos propios) y su `VersionAceptada` queda **`Desactualizada`** con un expediente de Actualización; o registra **cambio menor**. La aceptación anterior se conserva como histórico.
4. Las decisiones son **privadas por empresa**. Sobre una versión solo existen `Aprobado` y `Observado` (este último puede marcar `noAdmitirEstaVersion`); **`Rechazado` es solo del expediente** (B-7). Una observación de A se corrige con una versión nueva que vuelve a alertar a todas.
5. Versiones que no pasan los hechos no se vuelven actuales ni alertan: vuelven al tercero (`Devuelta`). Si el tercero retira la versión actual sin referencias, la anterior se restituye y las revalidaciones abiertas se cancelan.
6. Lo configurable es el **plazo de revalidación** (`diasRevalidacion`, 15 por defecto; 90 en aceptaciones importadas, B-8), la **política** al vencerlo (`politicaDesactualizada`: `BloquearTrasGracia` por defecto ⇒ `ConPendientes` bloqueante; A-2) y el **nivel de revisión** (Manual/Asistido/Automático). Se configura **por tenant con override por empresa y por regla del flujo** (B-4, #51). No existen políticas "proponer" o "ignorar".
7. La **ratificación** ("no cambió") renueva la vigencia en la empresa que la pidió sin pasar por ningún área y nunca la emite el sistema (A-7); reinicia la frescura desde la fecha de ratificación; si algo cambió, es una versión nueva y aplica el punto 3.
8. La lectura de hechos por IA es **coste de plataforma** y se hace una vez por versión (B-2); el presupuesto de cada tenant limita solo las decisiones automáticas.

## Alternativas descartadas
- **Aceptación libre por empresa (v2 en A, v3 en B)**: dos verdades paralelas; descartada por el usuario.
- **Compartición explícita por vínculo**: más privacidad, más fricción; descartada por el usuario a favor de la automática.
- **Bloquear siempre al cambiar la versión**: castiga a la empresa por diligencia del tercero; se dejó configurable.

## Consecuencias
- La veracidad la garantiza la bóveda única; la diligencia, cada empresa.
- La revisión automática hace que la revalidación sea casi gratuita para empresas con nivel `Automatico` (`SinCambios` ⇒ aprobado).
- El certificado de una empresa siempre habla de la información actual del tercero o declara "revalidación pendiente".

## Cómo se revierte
Un ADR posterior podría introducir una política por tipo canónico para documentos cuya versión anterior siga siendo válida (p. ej. pólizas históricas); hoy ese caso se cubre con tipos periódicos o `PorVinculo`.

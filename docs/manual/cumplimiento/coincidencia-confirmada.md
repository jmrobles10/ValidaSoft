---
id: "cumplimiento/coincidencia-confirmada"
titulo: "Confirma una coincidencia y rechaza el vínculo"
actor: "Oficial de cumplimiento"
dominio: "cumplimiento"
resumen: "La oficial de cumplimiento confirma que una coincidencia de listas es la misma persona. La verificación queda fallida, el expediente no se puede aprobar y Cumplimiento lo rechaza con motivos. La otra empresa del mismo tercero no cambia."
pasos: 5
---

# Confirma una coincidencia y rechaza el vínculo

**Actor:** Oficial de cumplimiento · **Dominio:** Cumplimiento y SAGRILAFT

La oficial de cumplimiento confirma que una coincidencia de listas es la misma persona. La verificación queda fallida, el expediente no se puede aprobar y Cumplimiento lo rechaza con motivos. La otra empresa del mismo tercero no cambia.

**Antes de empezar**

- Un grupo con Constructora Norte (flujo con listas restrictivas bloqueantes) y Constructora Sur (sin verificaciones).
- Un tercero invitado en ambas empresas, con una coincidencia simulada en OFAC en la consulta de Norte.
- Compras de Norte ya aprobó; Sur ya tiene su documento en revisión.

**Pasos**

1. Confirma la coincidencia
2. Aprueba tu área: el expediente sigue bloqueado
3. Rechaza el expediente con motivos
4. El tercero ve el cierre sin la coincidencia
5. Revisa el informe SAGRILAFT

## Paso 1. Confirma la coincidencia

Confirmar dice que la persona de la lista sí corresponde al tercero. La verificación de listas pasa a «Fallida» y el expediente no puede aprobarse. Confirmar no rechaza el vínculo por sí solo: alguien que puede rechazar en un área, o un administrador que bloquea el vínculo, debe hacerlo. La justificación queda en el historial y en el informe SAGRILAFT.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Confirma la coincidencia (escritorio)](../../../salida/manual/img/cumplimiento/coincidencia-confirmada/01-confirma-la-coincidencia-claro.webp)

**Variantes**

- **Descartas en lugar de confirmar:** La verificación queda satisfecha y el expediente puede aprobarse (ver «Analizar una coincidencia»).

## Paso 2. Aprueba tu área: el expediente sigue bloqueado

Aunque Compras y Cumplimiento aprueben, el expediente no se aprueba: la verificación bloqueante falló y nada la reemplaza. El aviso lo dice con el nombre de la verificación («Listas restrictivas (fallida)»). El paso siguiente es rechazar el expediente desde Cumplimiento.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Aprueba tu área: el expediente sigue bloqueado (escritorio)](../../../salida/manual/img/cumplimiento/coincidencia-confirmada/02-aprueba-tu-area-el-expediente-sigue-bloqueado-claro.webp)

## Paso 3. Rechaza el expediente con motivos

Cumplimiento rechaza con uno o más motivos y una justificación interna. El vínculo vuelve a «Invitado» y el tercero ve que Constructora Norte cerró la revisión sin aprobarla. El tercero no ve ni la coincidencia ni la justificación: esos datos son de la empresa.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Rechaza el expediente con motivos (escritorio)](../../../salida/manual/img/cumplimiento/coincidencia-confirmada/03-rechaza-el-expediente-con-motivos-claro.webp)

**Configuración que lo afecta**

- **Quién puede rechazar por cumplimiento** (`Configuración › Usuarios y roles (/usuarios)`): Solo quien tiene el permiso de decidir cumplimiento puede rechazar desde su área.

## Paso 4. El tercero ve el cierre sin la coincidencia

Desde su lado el tercero solo ve que Constructora Norte cerró la revisión sin aprobarla. No aparece la palabra OFAC, ni la coincidencia, ni la justificación interna de Cumplimiento.

> **Pantalla:** `/expediente/{id}`

![Paso 4: El tercero ve el cierre sin la coincidencia (escritorio)](../../../salida/manual/img/cumplimiento/coincidencia-confirmada/04-el-tercero-ve-el-cierre-sin-la-coincidencia-claro.webp)

## Paso 5. Revisa el informe SAGRILAFT

El informe de Norte cuenta al tercero con su situación y su coincidencia confirmada. En Constructora Sur, en cambio, el mismo tercero no tiene verificaciones: su revisión sigue igual y la aprueba Sofía como cualquier otro proveedor.

> **Pantalla:** `/e/{empresa}/cumplimiento`

![Paso 5: Revisa el informe SAGRILAFT (escritorio)](../../../salida/manual/img/cumplimiento/coincidencia-confirmada/05-revisa-el-informe-sagrilaft-claro.webp)

## Recorridos relacionados

- [Analiza una coincidencia en las listas y la descarta por homónimo](analizar-una-coincidencia.md)
- [Rechaza el expediente por cumplimiento](rechazo-directo-del-oficial.md)

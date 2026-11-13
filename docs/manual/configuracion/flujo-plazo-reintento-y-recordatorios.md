---
id: "configuracion/flujo-plazo-reintento-y-recordatorios"
titulo: "Fija el plazo de cargue, los recordatorios y el reintento"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora define en un flujo de proveedores cuántos días tiene el tercero para completar su expediente, cuándo se le recuerda, y si un tercero rechazado puede volver a postularse y tras cuántos días."
pasos: 4
---

# Fija el plazo de cargue, los recordatorios y el reintento

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora define en un flujo de proveedores cuántos días tiene el tercero para completar su expediente, cuándo se le recuerda, y si un tercero rechazado puede volver a postularse y tras cuántos días.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y su área «Revisión» con miembros.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Fija el plazo de cargue
2. Define cuándo se recuerda el plazo
3. Permite que un rechazado vuelva a postularse
4. Guarda el flujo con sus plazos

## Paso 1. Fija el plazo de cargue

«Plazo de cargue» son los días que tiene el tercero para completar el expediente inicial, y también para corregir cada devolución. Con 0 no hay plazo: el expediente no caduca. Si el plazo se cumple sin completar el expediente, este caduca: así funciona [Atiende un expediente caducado por falta de plazo y reábrelo](../operacion/plazo-de-cargue-y-caducidad.md).

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 1: Fija el plazo de cargue (escritorio)](../../../salida/manual/img/configuracion/flujo-plazo-reintento-y-recordatorios/01-fija-el-plazo-de-cargue-claro.webp)

**Variantes**

- **El plazo es 0:** Aparece «Sin plazo»: el expediente no caduca y los recordatorios no se pueden configurar.

**Configuración que lo afecta**

- **Plazo de cargue del flujo** (`Configuración › Flujos aprobatorios (/flujos)`): Define cuánto tiene el tercero para entregar el expediente antes de que caduque.

## Paso 2. Define cuándo se recuerda el plazo

En «Recordatorios» escribes cuántos días antes del vencimiento se le avisa al tercero, separados por coma. Con «10, 3» recibe un recordatorio diez días antes y otro tres días antes. Cada recordatorio debe ser menor que el plazo y son hasta cinco; si escribes uno inválido, el campo lo marca y no se puede guardar.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 2: Define cuándo se recuerda el plazo (escritorio)](../../../salida/manual/img/configuracion/flujo-plazo-reintento-y-recordatorios/02-define-cuando-se-recuerda-el-plazo-claro.webp)

**Variantes**

- **Un recordatorio es igual o mayor que el plazo:** El campo dice que cada recordatorio debe ser menor que el plazo.
- **El plazo es 0:** El campo de recordatorios queda desactivado porque no hay plazo que vigilar.

## Paso 3. Permite que un rechazado vuelva a postularse

Si el expediente se rechaza, por defecto solo tú puedes abrir uno nuevo desde el vínculo. Al permitir el reintento, el tercero ve los motivos en su portal y puede abrir un expediente nuevo por su cuenta. «Días de espera» cuenta desde el rechazo: con 15, el tercero debe esperar quince días; con 0, puede hacerlo de inmediato.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 3: Permite que un rechazado vuelva a postularse (escritorio)](../../../salida/manual/img/configuracion/flujo-plazo-reintento-y-recordatorios/03-permite-que-un-rechazado-vuelva-a-postularse-claro.webp)

**Variantes**

- **No permites el reintento:** Solo la empresa puede abrir un expediente nuevo desde el vínculo; el tercero no lo ve en su portal.
- **Los días de espera son 0:** El tercero puede volver a postularse en cuanto lo rechacen.

## Paso 4. Guarda el flujo con sus plazos

Con «Crear borrador» el plazo, los recordatorios y el reintento quedan en el borrador v1. Los expedientes que ya están abiertos conservan la configuración con la que empezaron: estos cambios aplican a los expedientes nuevos cuando publiques.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 4: Guarda el flujo con sus plazos (escritorio)](../../../salida/manual/img/configuracion/flujo-plazo-reintento-y-recordatorios/04-guarda-el-flujo-con-sus-plazos-claro.webp)

## Recorridos relacionados

- [Define la vigencia periódica del vínculo](flujo-vigencia-del-vinculo.md)
- [Atiende un expediente caducado por falta de plazo y reábrelo](../operacion/plazo-de-cargue-y-caducidad.md)

---
id: "informes/certificado-bloquear-y-revocar"
titulo: "Bloquea, desbloquea o revoca un certificado"
actor: "Administrador de empresa"
dominio: "informes"
resumen: "La administradora de Constructora Sur suspende el certificado de un proveedor mientras revisa una coincidencia, lo vuelve a activar y, finalmente, revoca otro certificado. En cada caso, quien verifica el certificado ve su estado actual sin el motivo interno."
pasos: 6
---

# Bloquea, desbloquea o revoca un certificado

**Actor:** Administrador de empresa · **Dominio:** Informes, auditoría y certificados

La administradora de Constructora Sur suspende el certificado de un proveedor mientras revisa una coincidencia, lo vuelve a activar y, finalmente, revoca otro certificado. En cada caso, quien verifica el certificado ve su estado actual sin el motivo interno.

**Antes de empezar**

- Un grupo con Constructora Sur y el área Revisión con Sofía como miembro; el RUT y el certificado de seguridad y salud en el trabajo son requisitos del flujo de proveedor.
- Un proveedor aprobado con su certificado vigente, y la página de verificación del certificado abierta como lo haría cualquier persona.

**Pasos**

1. Bloquea el vínculo mientras revisas
2. Mira lo que ve quien verifica el certificado
3. Desbloquea el vínculo cuando se descarte la coincidencia
4. Confirma que el certificado vuelve a estar vigente
5. Revoca el certificado
6. Mira el certificado revocado

## Paso 1. Bloquea el vínculo mientras revisas

Bloquear un vínculo suspende su certificado: mientras esté bloqueado, quien lo verifica ve que está suspendido. El «Motivo» que escribes es interno: el tercero y quien verifica no lo ven. Úsalo para una coincidencia que debes revisar, no como sanción.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Bloquea el vínculo mientras revisas (escritorio)](../../../salida/manual/img/informes/certificado-bloquear-y-revocar/01-bloquea-el-vinculo-mientras-revisas-claro.webp)

**Configuración que lo afecta**

- **Bloqueo del vínculo** (`Vínculos › detalle del vínculo › Más acciones`): Un vínculo bloqueado no recibe versiones nuevas y su certificado queda suspendido hasta que lo desbloquees.

## Paso 2. Mira lo que ve quien verifica el certificado

La página de verificación dice «Este certificado está suspendido» y que el PDF solo se entrega mientras el certificado esté vigente. El motivo del bloqueo no aparece en ninguna parte: es interno. El tercero recibe un correo que dice que suspendiste su certificado, sin el motivo.

> **Pantalla:** `/verificar/SC21C0-C-2026-000001-1`

![Paso 2: Mira lo que ve quien verifica el certificado (escritorio)](../../../salida/manual/img/informes/certificado-bloquear-y-revocar/02-mira-lo-que-ve-quien-verifica-el-certificado-claro.webp)

## Paso 3. Desbloquea el vínculo cuando se descarte la coincidencia

Al desbloquear, el certificado vuelve a quedar vigente. Escribe brevemente por qué se levanta el bloqueo; esa explicación es interna y no la ve quien verifica.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Desbloquea el vínculo cuando se descarte la coincidencia (escritorio)](../../../salida/manual/img/informes/certificado-bloquear-y-revocar/03-desbloquea-el-vinculo-cuando-se-descarte-la-coin-claro.webp)

## Paso 4. Confirma que el certificado vuelve a estar vigente

Quien verifica ya ve «Este certificado está vigente», y el PDF vuelve a estar disponible para descargarlo. El tercero no tiene que hacer nada para que se restablezca.

> **Pantalla:** `/verificar/SC21C0-C-2026-000001-1`

![Paso 4: Confirma que el certificado vuelve a estar vigente (escritorio)](../../../salida/manual/img/informes/certificado-bloquear-y-revocar/04-confirma-que-el-certificado-vuelve-a-estar-vigen-claro.webp)

## Paso 5. Revoca el certificado

Revocar el certificado es definitivo: deja de ser válido para quien lo verifique. Debes escribir la «Justificación», que es interna y no se le muestra a quien verifica. El tercero recibe un correo que dice que revocaste su certificado, sin la justificación.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 5: Revoca el certificado (escritorio)](../../../salida/manual/img/informes/certificado-bloquear-y-revocar/05-revoca-el-certificado-claro.webp)

## Paso 6. Mira el certificado revocado

La verificación dice «Este certificado fue revocado» y la fecha en que dejó de valer. Ni la justificación ni el PDF aparecen: sin vigencia no se entrega el documento. Para saber el motivo, quien verifica debe preguntarle a la empresa.

> **Pantalla:** `/verificar/SC21C0-C-2026-000001-1`

![Paso 6: Mira el certificado revocado (escritorio)](../../../salida/manual/img/informes/certificado-bloquear-y-revocar/06-mira-el-certificado-revocado-claro.webp)

## Recorridos relacionados

- [Encuentra los certificados de la empresa](certificados.md)
- [Verifica que un certificado es auténtico](../publico/verificar-certificado-autentico.md)
- [Lee el estado de un certificado suspendido o revocado](../publico/certificado-en-otros-estados.md)

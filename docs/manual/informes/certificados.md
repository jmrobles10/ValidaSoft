---
id: "informes/certificados"
titulo: "Encuentra los certificados de la empresa"
actor: "Administrador de empresa"
dominio: "informes"
resumen: "La administradora de Constructora Sur abre Certificados: ve los certificados que la empresa emitió a sus terceros aprobados, con su estado y su folio. Busca uno por el nombre del tercero y abre su ficha."
pasos: 3
---

# Encuentra los certificados de la empresa

**Actor:** Administrador de empresa · **Dominio:** Informes, auditoría y certificados

La administradora de Constructora Sur abre Certificados: ve los certificados que la empresa emitió a sus terceros aprobados, con su estado y su folio. Busca uno por el nombre del tercero y abre su ficha.

**Antes de empezar**

- Un grupo con Constructora Sur y el área Revisión con Sofía como miembro; el RUT y el certificado de seguridad y salud en el trabajo son requisitos del flujo de proveedor.
- Un proveedor cuyo expediente fue aprobado: la empresa le emitió un certificado vigente.

**Pasos**

1. Abre la lista de certificados
2. Busca un certificado por el nombre del tercero
3. Abre la ficha del tercero desde su certificado

## Paso 1. Abre la lista de certificados

Certificados reúne los certificados que la empresa emitió a sus terceros aprobados. Por cada fila ves el tercero, el estado (vigente, bloqueado, vencido o revocado) y el folio del certificado, que es el número que usa quien lo verifica. Un certificado se emite cuando el vínculo queda aprobado y se reemite en cada renovación.

> **Pantalla:** `/e/{empresa}/certificados`

![Paso 1: Abre la lista de certificados (escritorio)](../../../salida/manual/img/informes/certificados/01-abre-la-lista-de-certificados-claro.webp)

**Configuración que lo afecta**

- **Vigencia del vínculo (por flujo)** (`Configuración › Flujos aprobatorios (/flujos)`): La vigencia del vínculo decide cuándo un certificado vence y cuándo hay que renovarlo.

## Paso 2. Busca un certificado por el nombre del tercero

El buscador encuentra un certificado por el tercero, su identificación o su folio. Escribe parte del nombre y la lista se reduce a sus coincidencias. Borra el texto para volver a ver todos.

> **Pantalla:** `/e/{empresa}/certificados`

![Paso 2: Busca un certificado por el nombre del tercero (escritorio)](../../../salida/manual/img/informes/certificados/02-busca-un-certificado-por-el-nombre-del-tercero-claro.webp)

## Paso 3. Abre la ficha del tercero desde su certificado

Al hacer clic en el folio se abre la ficha del tercero (su vista de 360 grados). Desde ahí puedes revisar el estado de su vínculo antes de decidir qué hacer con el certificado.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Abre la ficha del tercero desde su certificado (escritorio)](../../../salida/manual/img/informes/certificados/03-abre-la-ficha-del-tercero-desde-su-certificado-claro.webp)

## Recorridos relacionados

- [Bloquea, desbloquea o revoca un certificado](certificado-bloquear-y-revocar.md)
- [Verifica que un certificado es auténtico](../publico/verificar-certificado-autentico.md)
- [Exporta un expediente completo](exportar-un-expediente.md)

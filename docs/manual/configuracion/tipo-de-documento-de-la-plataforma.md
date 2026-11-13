---
id: "configuracion/tipo-de-documento-de-la-plataforma"
titulo: "Crea un documento a partir de uno de la plataforma"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora parte de la Cédula de ciudadanía, un documento conocido de la plataforma. Validasoft fija el ámbito compartido y los techos de formato y peso; ella intenta un límite más laxo, recibe el error y lo corrige antes de crear el tipo."
pasos: 5
---

# Crea un documento a partir de uno de la plataforma

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora parte de la Cédula de ciudadanía, un documento conocido de la plataforma. Validasoft fija el ámbito compartido y los techos de formato y peso; ella intenta un límite más laxo, recibe el error y lo corrige antes de crear el tipo.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y el catálogo de documentos de la plataforma disponible.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Identifica los documentos de la plataforma en la tabla
2. Elige el documento de la plataforma
3. Revisa los formatos que admite la plataforma
4. Intenta un peso mayor que el de la plataforma
5. Corrige el peso y crea el tipo

## Paso 1. Identifica los documentos de la plataforma en la tabla

Los documentos marcados «de la plataforma», como la Cédula de ciudadanía o el RUT, son conocidos: la plataforma sabe qué formatos y pesos son razonables para ellos. Se comparten entre empresas y su ámbito no lo decide cada empresa. Para crear uno nuevo a partir de ellos, abre «Nuevo tipo de documento».

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 1: Identifica los documentos de la plataforma en la tabla (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-de-la-plataforma/01-identifica-los-documentos-de-la-plataforma-en-la-claro.webp)

**Configuración que lo afecta**

- **Documentos de la plataforma** (`Configuración › Tipos de documento (/tipos-documento)`): Un tipo creado desde uno de la plataforma hereda su ámbito y no puede ser más laxo que sus reglas.

## Paso 2. Elige el documento de la plataforma

El código y el nombre son tuyos: aquí se distingue como el certificado de la sucursal. Sin documento de la plataforma, el tipo es propio y defines el ámbito.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 2: Elige el documento de la plataforma (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-de-la-plataforma/02-elige-el-documento-de-la-plataforma-claro.webp)

**Variantes**

- **Eliges un documento de la plataforma:** El ámbito lo fija la plataforma y no lo puedes cambiar; el formato, el peso y la vigencia no pueden ser más laxos que los suyos.
- **Eliges «Ninguno: es un documento propio»:** Defines tú el ámbito, el formato, el peso y la vigencia.

## Paso 3. Revisa los formatos que admite la plataforma

La sección «Archivo» muestra lo que la plataforma permite para este documento. Los formatos que no están permitidos aparecen deshabilitados y el texto bajo ellos dice cuáles sí admite la plataforma. Puedes marcar menos formatos que los permitidos, pero no más: el tipo nunca puede aceptar algo que la plataforma no acepta para ese documento.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 3: Revisa los formatos que admite la plataforma (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-de-la-plataforma/03-revisa-los-formatos-que-admite-la-plataforma-claro.webp)

**Variantes**

- **El documento no tiene formatos fijados por la plataforma:** El texto dice «Ninguno marcado = PDF, JPG, PNG o WebP» y puedes elegir libremente.

## Paso 4. Intenta un peso mayor que el de la plataforma

La plataforma fija un máximo de 20 MB para este documento y no lo muestra en el campo. Escribir 999 MB es un límite más laxo: al crear el tipo, Validasoft lo rechaza, dice cuál es el máximo de la plataforma y el panel queda abierto en rojo. Nada se guarda hasta que corrijas el valor; así el proveedor nunca recibe una regla que la plataforma no permite.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 4: Intenta un peso mayor que el de la plataforma (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-de-la-plataforma/04-intenta-un-peso-mayor-que-el-de-la-plataforma-claro.webp)

**Variantes**

- **El peso es igual o menor que el de la plataforma:** El tipo se crea sin error.

## Paso 5. Corrige el peso y crea el tipo

Con un peso de 2 MB, dentro del límite de la plataforma, el tipo se crea. La tabla lo muestra con «· de la plataforma» junto al código y con su ámbito compartido. Desde ahora puedes pedirlo en los flujos como cualquier otro documento.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 5: Corrige el peso y crea el tipo (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-de-la-plataforma/05-corrige-el-peso-y-crea-el-tipo-claro.webp)

## Recorridos relacionados

- [Configura un tipo de documento y publica un flujo que lo pide](tipo-de-documento-y-flujo.md)
- [Crea un documento con formulario y plantilla descargable](tipo-de-documento-solo-formulario-y-plantilla.md)

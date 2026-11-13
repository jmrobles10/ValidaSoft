---
id: "configuracion/permisos-por-rol"
titulo: "Comprueba qué secciones ve cada rol"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora compara lo que ve una persona con rol Administrador, una Revisora y una Oficial de cumplimiento. Cada rol ve solo las secciones que le corresponden; las demás muestran un aviso de «Esta sección es de…» en lugar de su contenido."
pasos: 3
---

# Comprueba qué secciones ve cada rol

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora compara lo que ve una persona con rol Administrador, una Revisora y una Oficial de cumplimiento. Cada rol ve solo las secciones que le corresponden; las demás muestran un aviso de «Esta sección es de…» en lugar de su contenido.

**Antes de empezar**

- Un grupo empresarial con las empresas «Constructora Norte S.A.S.» y «Constructora Sur S.A.S.».
- Personas con roles distintos: Ana (Administradora del grupo), Sofía (Revisora de Constructora Sur) y Olga (Oficial de cumplimiento de Constructora Norte).

**Pasos**

1. Abre una sección que tu rol no incluye
2. Compara con lo que ve el Administrador
3. Revisa una sección de administración con otro rol

## Paso 1. Abre una sección que tu rol no incluye

Sofía es Revisora de Constructora Sur: su trabajo es la bandeja de revisión, no la configuración. Si abre una sección que su rol no incluye, la pantalla no muestra el contenido: dice «Esta sección es de…» y explica que su rol no la incluye y que debe pedir acceso a un administrador del grupo. Así no hay botones que aparezcan y fallen: lo que no le corresponde no se ofrece.

> **Pantalla:** `/e/{empresa}/usuarios`

![Paso 1: Abre una sección que tu rol no incluye (escritorio)](../../../salida/manual/img/configuracion/permisos-por-rol/01-abre-una-seccion-que-tu-rol-no-incluye-claro.webp)

**Variantes**

- **El rol es de todo el grupo:** La persona ve las secciones del grupo en todas las empresas, y las de cada empresa según el rol de esa empresa.

**Configuración que lo afecta**

- **Roles por empresa** (`Configuración › Usuarios y roles (/usuarios)`): Cada rol decide qué secciones de la empresa ve la persona.

## Paso 2. Compara con lo que ve el Administrador

El Administrador del grupo ve la misma sección completa, con el botón «Agregar persona» y las acciones de cada fila. Un rol que no la incluye, como el de Revisora, ve en cambio el aviso del paso anterior.

> **Pantalla:** `/e/{empresa}/usuarios`

![Paso 2: Compara con lo que ve el Administrador (escritorio)](../../../salida/manual/img/configuracion/permisos-por-rol/02-compara-con-lo-que-ve-el-administrador-claro.webp)

## Paso 3. Revisa una sección de administración con otro rol

Olga es Oficial de cumplimiento de Constructora Norte. Su rol atiende cumplimiento y estudios SAGRILAFT, no la administración del dominio de correo: la pantalla le muestra el mismo aviso que a Sofía. Con el rol correcto la sección aparece completa; si el rol cambia, cambia lo que ve en su próximo ingreso.

> **Pantalla:** `/e/{empresa}/dominio-correo`

![Paso 3: Revisa una sección de administración con otro rol (escritorio)](../../../salida/manual/img/configuracion/permisos-por-rol/03-revisa-una-seccion-de-administracion-con-otro-ro-claro.webp)

**Variantes**

- **Olga recibe el rol de Administrador en la empresa:** Ve la sección completa en su próximo ingreso desde su sistema.

## Recorridos relacionados

- [Agrega una persona, dale roles y desactívala](usuarios-y-roles.md)

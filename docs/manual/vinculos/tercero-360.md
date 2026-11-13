---
id: "vinculos/tercero-360"
titulo: "Abre la ficha de un tercero que trabaja con dos empresas del grupo"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "La administradora de Constructora Norte abre la ficha de un tercero que también tiene vínculo con Constructora Sur. Ve sus datos y sus vínculos con Norte, y solo el nombre de la otra empresa: lo que Sur decidió no se muestra."
pasos: 4
---

# Abre la ficha de un tercero que trabaja con dos empresas del grupo

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

La administradora de Constructora Norte abre la ficha de un tercero que también tiene vínculo con Constructora Sur. Ve sus datos y sus vínculos con Norte, y solo el nombre de la otra empresa: lo que Sur decidió no se muestra.

**Antes de empezar**

- Un grupo con Constructora Norte y Constructora Sur, y una administradora del grupo.
- Un mismo tercero invitado por Norte y por Sur, cada uno con su vínculo en Invitado.

**Pasos**

1. Abre el vínculo del tercero
2. Revisa la identificación del tercero
3. Revisa los vínculos con tu empresa
4. Mira las otras empresas del grupo

## Paso 1. Abre el vínculo del tercero

Desde el detalle de un vínculo, el nombre del tercero es un enlace a su ficha, que reúne todo lo que el grupo sabe del tercero. La ficha es la misma para cualquier empresa del grupo, pero lo que muestra de cada una depende de tu permiso para ver terceros.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Abre el vínculo del tercero (escritorio)](../../../salida/manual/img/vinculos/tercero-360/01-abre-el-vinculo-del-tercero-claro.webp)

**Configuración que lo afecta**

- **Usuarios y roles** (`Configuración › Usuarios y roles (/usuarios)`): El permiso de ver terceros decide si el nombre lleva a la ficha.

## Paso 2. Revisa la identificación del tercero

La primera sección reúne la identificación, el tipo de persona y los contactos registrados por el tercero. Esos datos son del tercero y son iguales para todas las empresas del grupo.

> **Pantalla:** `/e/{empresa}/terceros/{id}`

![Paso 2: Revisa la identificación del tercero (escritorio)](../../../salida/manual/img/vinculos/tercero-360/02-revisa-la-identificacion-del-tercero-claro.webp)

## Paso 3. Revisa los vínculos con tu empresa

Esta sección lista solo los vínculos con tu empresa: el estado, el tipo y el certificado si existe. Desde aquí puedes ir al vínculo para actuar sobre él.

> **Pantalla:** `/e/{empresa}/terceros/{id}`

![Paso 3: Revisa los vínculos con tu empresa (escritorio)](../../../salida/manual/img/vinculos/tercero-360/03-revisa-los-vinculos-con-tu-empresa-claro.webp)

**Variantes**

- **Tu permiso no incluye ver vínculos:** La sección no muestra los vínculos y dice que pidas acceso a un administrador.

## Paso 4. Mira las otras empresas del grupo

La sección «Otras empresas del grupo» indica que el tercero también tiene vínculo con otras empresas del grupo, como Constructora Sur. Por cada una ves el nombre y el estado del vínculo; las decisiones de esas empresas no se muestran.

> **Pantalla:** `/e/{empresa}/terceros/{id}`

![Paso 4: Mira las otras empresas del grupo (escritorio)](../../../salida/manual/img/vinculos/tercero-360/04-mira-las-otras-empresas-del-grupo-claro.webp)

## Recorridos relacionados

- [Revisa el detalle de un vínculo](detalle-del-vinculo.md)
- [Consulta la ficha aceptada de un vínculo](ficha-aceptada-del-vinculo.md)

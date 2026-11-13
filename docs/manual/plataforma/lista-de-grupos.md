---
id: "plataforma/lista-de-grupos"
titulo: "Busca un grupo y revisa sus empresas"
actor: "Plataforma"
dominio: "plataforma"
resumen: "El operador de Plataforma abre la lista de grupos (clientes de Validasoft), la busca por nombre, NIT o empresa y revisa en su tarjeta las empresas del grupo, con su identificación, su prefijo y su plan de IA."
pasos: 3
---

# Busca un grupo y revisa sus empresas

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

El operador de Plataforma abre la lista de grupos (clientes de Validasoft), la busca por nombre, NIT o empresa y revisa en su tarjeta las empresas del grupo, con su identificación, su prefijo y su plan de IA.

**Antes de empezar**

- Varios grupos creados, cada uno con sus empresas.
- Un operador de Plataforma con la consola abierta.

**Pasos**

1. Abre Grupos y empresas
2. Busca un grupo por una de sus empresas
3. Revisa las empresas del grupo

## Paso 1. Abre Grupos y empresas

Es la pantalla de inicio de la consola de Plataforma. Cada grupo es un cliente de Validasoft y aparece como una tarjeta con su número de empresas, su fecha de creación y sus acciones: «Plan y límites», «Sistemas» y «Agregar empresa». Con el buscador encuentras un grupo por su nombre, por el nombre o NIT de una de sus empresas.

> **Pantalla:** `/plataforma/grupos`

![Paso 1: Abre Grupos y empresas (escritorio)](../../../salida/manual/img/plataforma/lista-de-grupos/01-abre-grupos-y-empresas-claro.webp)

**Configuración que lo afecta**

- **Grupos y empresas de la plataforma** (`Plataforma › Grupos y empresas (/plataforma/grupos)`): Es la lista de clientes que administra la plataforma.

## Paso 2. Busca un grupo por una de sus empresas

Escribe parte del nombre del grupo, de una empresa o un NIT. La lista se filtra mientras escribes; si nada coincide, la pantalla dice «Ningún grupo coincide» y ofrece limpiar la búsqueda. Cada tarjeta muestra la tabla de sus empresas, así que basta con reconocer la que buscas.

> **Pantalla:** `/plataforma/grupos`

![Paso 2: Busca un grupo por una de sus empresas (escritorio)](../../../salida/manual/img/plataforma/lista-de-grupos/02-busca-un-grupo-por-una-de-sus-empresas-claro.webp)

**Variantes**

- **No coincide nada:** Aparece «Ningún grupo coincide con» lo escrito y el botón «Limpiar la búsqueda».

## Paso 3. Revisa las empresas del grupo

La tabla de cada grupo muestra sus empresas con la identificación (NIT), el prefijo de folio (el que aparece en los números de expediente) y el plan de IA de cada una. Desde la fila, «Plan de IA» abre la configuración del nivel de IA de esa empresa.

> **Pantalla:** `/plataforma/grupos`

![Paso 3: Revisa las empresas del grupo (escritorio)](../../../salida/manual/img/plataforma/lista-de-grupos/03-revisa-las-empresas-del-grupo-claro.webp)

**Configuración que lo afecta**

- **Plan de IA por empresa** (`Plataforma › Grupos y empresas › Plan de IA`): Define el nivel de IA que puede usar esa empresa.

## Recorridos relacionados

- [Crea un grupo con su primera empresa](crear-grupo-y-empresa.md)
- [Agrega una empresa a un grupo existente](agregar-empresa-a-un-grupo.md)

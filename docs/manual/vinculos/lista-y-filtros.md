---
id: "vinculos/lista-y-filtros"
titulo: "Revisa la lista de vínculos y filtra por estado y tipo"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "La administradora abre «Vínculos» para ver a todos los terceros con relación con su empresa, filtra por estado y tipo de vínculo, busca un tercero y abre uno de la lista."
pasos: 7
---

# Revisa la lista de vínculos y filtra por estado y tipo

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

La administradora abre «Vínculos» para ver a todos los terceros con relación con su empresa, filtra por estado y tipo de vínculo, busca un tercero y abre uno de la lista.

**Antes de empezar**

- Un grupo empresarial con las empresas «Constructora Norte» y «Constructora Sur», y una administradora del grupo.
- Varios vínculos invitados por el sistema integrado: proveedores personas jurídicas, un cliente y tres aliados que ya están en validación, activos y bloqueados.

**Pasos**

1. Abre la lista de vínculos
2. Elige un estado para filtrar
3. Mira solo los vínculos activos
4. Encuentra los vínculos bloqueados
5. Filtra por tipo de vínculo
6. Busca un vínculo por su referencia externa
7. Abre un vínculo de la lista

## Paso 1. Abre la lista de vínculos

La lista reúne a todos los terceros que tienen un vínculo con tu empresa. Por cada fila ves el tercero, el tipo de vínculo, su estado (Invitado, En validación, Activo, Bloqueado…) y su fecha de creación, y en la columna «Referencias externas» el identificador que le dio cada sistema integrado. Para abrir uno, haz clic en su fila.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 1: Abre la lista de vínculos (escritorio)](../../../salida/manual/img/vinculos/lista-y-filtros/01-abre-la-lista-de-vinculos-claro.webp)

**Variantes**

- **Eres administrador del grupo:** Ves los vínculos de la empresa activa; cambiar de empresa en el selector de la parte superior cambia la lista.
- **Tu rol no incluye la sección Vínculos:** La sección no aparece en la barra y no puedes abrirla.

**Configuración que lo afecta**

- **Usuarios y roles** (`Configuración › Usuarios y roles (/usuarios)`): El rol de cada persona decide si ve la sección Vínculos y qué puede hacer en ella.

## Paso 2. Elige un estado para filtrar

El filtro «Estado» (1) ofrece cada situación en que puede estar un vínculo: Invitado (esperan que el tercero acepte la invitación), En validación (su expediente está abierto), Activo, Por vencer, Bloqueado y los demás. «En revisión» no es un estado propio: es un vínculo activo cuya última actualización espera una decisión de las áreas. Los filtros se guardan en la dirección de la página, así que se mantienen al volver.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 2: Elige un estado para filtrar (escritorio)](../../../salida/manual/img/vinculos/lista-y-filtros/02-elige-un-estado-para-filtrar-claro.webp)

## Paso 3. Mira solo los vínculos activos

Con «Activo» (1) la lista deja solo a los terceros que ya aprobaron todas las áreas y pueden operar con la empresa: aquí, la metalúrgica. Los demás estados desaparecen hasta que cambies el filtro.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 3: Mira solo los vínculos activos (escritorio)](../../../salida/manual/img/vinculos/lista-y-filtros/03-mira-solo-los-vinculos-activos-claro.webp)

## Paso 4. Encuentra los vínculos bloqueados

Con «Bloqueado» (1) ves a los terceros a los que la empresa suspendió, por ejemplo por una coincidencia en una lista restrictiva. Un vínculo bloqueado no recibe documentos nuevos ni puede enviar a revisión hasta que se desbloquee.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 4: Encuentra los vínculos bloqueados (escritorio)](../../../salida/manual/img/vinculos/lista-y-filtros/04-encuentra-los-vinculos-bloqueados-claro.webp)

**Variantes**

- **El vínculo está en validación:** Aparece con el estado «En validación» mientras las áreas deciden; filtra por ese estado para verlos juntos.

## Paso 5. Filtra por tipo de vínculo

El filtro «Tipo» separa los vínculos según el tipo que tiene cada tercero con la empresa (Proveedor, Cliente u otro que hayas configurado). El mismo tercero puede tener dos vínculos distintos con la empresa, uno por tipo; cada uno aparece en su fila. «Limpiar filtros» quita todos a la vez.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 5: Filtra por tipo de vínculo (escritorio)](../../../salida/manual/img/vinculos/lista-y-filtros/05-filtra-por-tipo-de-vinculo-claro.webp)

**Configuración que lo afecta**

- **Tipos de vínculo** (`Configuración › Tipos de vínculo (/tipos-vinculo)`): Los tipos que existen en el grupo son las opciones de este filtro.

## Paso 6. Busca un vínculo por su referencia externa

El buscador encuentra un vínculo por el identificador que le dio un sistema integrado. Escribe el sistema, un punto medio y la referencia (por ejemplo «ERP · PRV-002») y pulsa Enter. Si no existe, la pantalla dice que ninguna referencia coincide; si escribes solo la referencia sin el sistema, te pide escribirla con el sistema delante.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 6: Busca un vínculo por su referencia externa (escritorio)](../../../salida/manual/img/vinculos/lista-y-filtros/06-busca-un-vinculo-por-su-referencia-externa-claro.webp)

**Variantes**

- **El sistema integrado no tiene referencias registradas:** La búsqueda solo devuelve coincidencias de los sistemas que ya crearon vínculos con la empresa.

## Paso 7. Abre un vínculo de la lista

Al hacer clic en la fila se abre el detalle del vínculo, con los datos del tercero, su estado, la invitación enviada y las acciones que el estado permite (reenviar, bloquear, desvincular, anular…). Desde el detalle se vuelve a la lista con la flecha de la parte superior.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 7: Abre un vínculo de la lista (escritorio)](../../../salida/manual/img/vinculos/lista-y-filtros/07-abre-un-vinculo-de-la-lista-claro.webp)

## Recorridos relacionados

- [Revisa el detalle de un vínculo](detalle-del-vinculo.md)
- [Acepta la invitación y completa tu información como persona jurídica](../tercero/aceptar-invitacion-persona-juridica.md)

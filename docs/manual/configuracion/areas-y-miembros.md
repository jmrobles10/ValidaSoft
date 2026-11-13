---
id: "configuracion/areas-y-miembros"
titulo: "Crea un área, asígnale personas y desactívala"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora de Constructora Sur crea el área Jurídica para una etapa de revisión, agrega a Sofía como miembro y, cuando ya no recibe trabajo, la desactiva. Las áreas son de la empresa y sus miembros deciden quién ve cada expediente."
pasos: 5
---

# Crea un área, asígnale personas y desactívala

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora de Constructora Sur crea el área Jurídica para una etapa de revisión, agrega a Sofía como miembro y, cuando ya no recibe trabajo, la desactiva. Las áreas son de la empresa y sus miembros deciden quién ve cada expediente.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» con Sofía como miembro.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Áreas
2. Crea el área
3. Agrega personas al área
4. Desactiva el área
5. Comprueba que el área quedó inactiva

## Paso 1. Abre Configuración › Áreas

Cada área revisa una etapa de los flujos. La lista muestra las áreas de esta empresa con cuántos miembros tiene cada una; las inactivas aparecen atenuadas con la etiqueta «Inactiva». Las áreas son de la empresa, no del grupo: otra empresa del mismo grupo tiene sus propias áreas.

> **Pantalla:** `/e/{empresa}/areas`

![Paso 1: Abre Configuración › Áreas (escritorio)](../../../salida/manual/img/configuracion/areas-y-miembros/01-abre-configuracion-areas-claro.webp)

**Configuración que lo afecta**

- **Áreas de la empresa y sus miembros** (`Configuración › Áreas (/areas)`): Solo se pueden elegir áreas activas en los flujos; quién decide en cada una sale de sus miembros.

## Paso 2. Crea el área

El código es corto y sin espacios (por ejemplo COMPRAS) y no se puede cambiar después; el nombre es el que ven los flujos y los expedientes. Al crearla, el área queda activa y sin miembros: todavía no decide nada hasta que le asignes personas.

> **Pantalla:** `/e/{empresa}/areas`

![Paso 2: Crea el área (escritorio)](../../../salida/manual/img/configuracion/areas-y-miembros/02-crea-el-area-claro.webp)

**Variantes**

- **El código ya lo usa otra área de la empresa:** La pantalla dice que la empresa ya tiene un área con ese código y no la crea.

## Paso 3. Agrega personas al área

Escribe el nombre o el correo de una persona y elígela. Solo aparecen personas que ya tienen un rol en esta empresa; si no encuentras a alguien, primero debes darle acceso desde «Usuarios y roles». Cada miembro aparece con su rol en la empresa. Un miembro del área ve en la bandeja los expedientes que le toca revisar a esa área; quien no es miembro no los ve.

> **Pantalla:** `/e/{empresa}/areas`

![Paso 3: Agrega personas al área (escritorio)](../../../salida/manual/img/configuracion/areas-y-miembros/03-agrega-personas-al-area-claro.webp)

**Variantes**

- **La persona ya es miembro del área:** No aparece en la lista de candidatos y la pantalla no la vuelve a agregar.

**Configuración que lo afecta**

- **Miembros del área** (`Configuración › Áreas (/areas)`): Quién es miembro decide qué expedientes aparecen en la bandeja de revisión.

## Paso 4. Desactiva el área

Al editar un área puedes desactivarla con el interruptor «Activa». Una área inactiva no recibe trabajo nuevo: los flujos no la ofrecen para etapas nuevas y sus miembros dejan de recibir expedientes. Los expedientes que ya tenía siguen su curso y puedes volver a activarla en cualquier momento.

> **Pantalla:** `/e/{empresa}/areas`

![Paso 4: Desactiva el área (escritorio)](../../../salida/manual/img/configuracion/areas-y-miembros/04-desactiva-el-area-claro.webp)

**Variantes**

- **El área está activa y tiene miembros:** Desactivarla no quita a sus miembros: al reactivarla siguen asignados.

## Paso 5. Comprueba que el área quedó inactiva

En la lista, el área aparece atenuada y con la etiqueta «Inactiva» en lugar del número de miembros. Su ficha muestra el aviso de que no recibe trabajo nuevo, y se reactiva con «Editar área».

> **Pantalla:** `/e/{empresa}/areas`

![Paso 5: Comprueba que el área quedó inactiva (escritorio)](../../../salida/manual/img/configuracion/areas-y-miembros/05-comprueba-que-el-area-quedo-inactiva-claro.webp)

## Recorridos relacionados

- [Arma una etapa con dos áreas en paralelo y una automática](flujo-etapas-y-areas-en-paralelo.md)
- [Agrega una persona, dale roles y desactívala](usuarios-y-roles.md)

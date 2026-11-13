---
id: "plataforma/ingresos-de-los-operadores"
titulo: "Revisa quién entró a la consola y filtra los ingresos fallidos"
actor: "Plataforma"
dominio: "plataforma"
resumen: "El operador de Plataforma revisa la bitácora de ingresos de los operadores: quién entró, quién falló al entrar, quién registró o revocó una passkey y quién confirmó una acción sensible. Filtra por resultado para ver solo los fallidos y luego limpia los filtros."
pasos: 3
---

# Revisa quién entró a la consola y filtra los ingresos fallidos

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

El operador de Plataforma revisa la bitácora de ingresos de los operadores: quién entró, quién falló al entrar, quién registró o revocó una passkey y quién confirmó una acción sensible. Filtra por resultado para ver solo los fallidos y luego limpia los filtros.

**Antes de empezar**

- Varios ingresos de operadores registrados (exitosos y fallidos).
- Un operador de Plataforma con la consola abierta.

**Pasos**

1. Abre la bitácora de ingresos
2. Filtra solo los ingresos fallidos
3. Limpia los filtros

## Paso 1. Abre la bitácora de ingresos

Cada fila es un evento de la consola: quién lo hizo, cuándo y con qué resultado. Aparecen primero los más recientes. Se registran los ingresos exitosos y fallidos, los códigos enviados o incorrectos, el registro y la revocación de passkeys, y las acciones sensibles confirmadas.

> **Pantalla:** `/plataforma/ingresos`

![Paso 1: Abre la bitácora de ingresos (escritorio)](../../../salida/manual/img/plataforma/ingresos-de-los-operadores/01-abre-la-bitacora-de-ingresos-claro.webp)

**Variantes**

- **Ingreso fallido:** La fila aparece con resultado «Fallido» y el evento que lo explica, por ejemplo un código incorrecto.
- **Ingreso exitoso:** La fila aparece con resultado «Exitoso».

**Configuración que lo afecta**

- **Bitácora de ingresos de los operadores** (`Plataforma › Ingresos (/plataforma/ingresos)`): Es el registro de quién entra a la consola y de sus acciones sensibles.

## Paso 2. Filtra solo los ingresos fallidos

Con el filtro «Resultado» eliges «Fallido» para ver solo los intentos que no pasaron. Los demás filtros, como operador, evento o fechas («Desde» y «Hasta»), se combinan con este.

> **Pantalla:** `/plataforma/ingresos`

![Paso 2: Filtra solo los ingresos fallidos (escritorio)](../../../salida/manual/img/plataforma/ingresos-de-los-operadores/02-filtra-solo-los-ingresos-fallidos-claro.webp)

**Configuración que lo afecta**

- **Filtros de la bitácora**: Los filtros solo cambian la vista; no modifican los eventos registrados.

## Paso 3. Limpia los filtros

El botón «Limpiar filtros» quita todos los filtros y vuelve a mostrar la bitácora completa, con los eventos más recientes primero.

> **Pantalla:** `/plataforma/ingresos`

![Paso 3: Limpia los filtros (escritorio)](../../../salida/manual/img/plataforma/ingresos-de-los-operadores/03-limpia-los-filtros-claro.webp)

## Recorridos relacionados

- [Crea un operador de la consola y le quita el acceso](operadores.md)
- [Entra a la consola de Plataforma con correo, código y passkey](ingreso-del-operador.md)

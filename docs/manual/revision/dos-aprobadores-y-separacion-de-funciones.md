---
id: "revision/dos-aprobadores-y-separacion-de-funciones"
titulo: "Aprueba un área que pide dos personas distintas"
actor: "Revisor/área"
dominio: "revision"
resumen: "El Comité de Norte necesita dos aprobaciones de personas distintas. Ana invitó al proveedor, así que la separación de funciones le impide decidir. Rubén aprueba la primera; Olga, la segunda, y el vínculo queda activo."
pasos: 3
---

# Aprueba un área que pide dos personas distintas

**Actor:** Revisor/área · **Dominio:** Revisión de expedientes

El Comité de Norte necesita dos aprobaciones de personas distintas. Ana invitó al proveedor, así que la separación de funciones le impide decidir. Rubén aprueba la primera; Olga, la segunda, y el vínculo queda activo.

**Antes de empezar**

- Un grupo con Constructora Norte y el área Comité con Ana, Rubén y Olga como miembros. El flujo de Aliado pide 2 aprobaciones del Comité y tiene separación de funciones activada.
- Ana, la administradora, crea la invitación: queda registrada como quien invitó al proveedor.

**Pasos**

1. Intenta aprobar siendo quien invitó
2. Aprueba como primera persona del área
3. Aprueba como segunda persona y activa el vínculo

## Paso 1. Intenta aprobar siendo quien invitó

Con la separación de funciones activa, quien invitó al proveedor no puede decidir sobre su expediente, aunque sea miembro del área. Al intentarlo, la pantalla muestra un aviso que lo explica y el veredicto no se registra. Cancela el diálogo y deja la decisión a otra persona del área.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Intenta aprobar siendo quien invitó (escritorio)](../../../salida/manual/img/revision/dos-aprobadores-y-separacion-de-funciones/01-intenta-aprobar-siendo-quien-invito-claro.webp)

## Paso 2. Aprueba como primera persona del área

La primera aprobación cuenta como «1 de 2». El área sigue pendiente y el mismo revisor ya no puede votar otra vez: la tarjeta del área dice que falta otra persona. El expediente sigue en revisión hasta que la segunda persona apruebe.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Aprueba como primera persona del área (escritorio)](../../../salida/manual/img/revision/dos-aprobadores-y-separacion-de-funciones/02-aprueba-como-primera-persona-del-area-claro.webp)

## Paso 3. Aprueba como segunda persona y activa el vínculo

La segunda persona distinta completa las dos aprobaciones: el área queda aprobada, el expediente se aprueba y el vínculo queda activo. Si la misma persona hubiera intentado aprobar dos veces, la segunda no habría contado.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Aprueba como segunda persona y activa el vínculo (escritorio)](../../../salida/manual/img/revision/dos-aprobadores-y-separacion-de-funciones/03-aprueba-como-segunda-persona-y-activa-el-vinculo-claro.webp)

**Configuración que lo afecta**

- **Aprobadores requeridos por área y separación de funciones (en el flujo)** (`Configuración › Flujos (/flujos)`): Define cuántas personas distintas deben aprobar y si quien invitó puede decidir.

## Recorridos relacionados

- [Sigue un expediente que pasa por tres etapas](etapas-en-secuencia.md)
- [Aprueba el área de Compras](aprobar-un-area.md)

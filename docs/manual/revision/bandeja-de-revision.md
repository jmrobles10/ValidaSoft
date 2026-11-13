---
id: "revision/bandeja-de-revision"
titulo: "Filtra la bandeja de revisión y abre un expediente"
actor: "Revisor/área"
dominio: "revision"
resumen: "Rubén revisa la bandeja de su área: ve una fila por expediente con su tercero, tipo, plazo y estado. Filtra por el estado de su área, limpia los filtros y abre uno de los expedientes."
pasos: 3
---

# Filtra la bandeja de revisión y abre un expediente

**Actor:** Revisor/área · **Dominio:** Revisión de expedientes

Rubén revisa la bandeja de su área: ve una fila por expediente con su tercero, tipo, plazo y estado. Filtra por el estado de su área, limpia los filtros y abre uno de los expedientes.

**Antes de empezar**

- Un grupo con Constructora Norte, con el área Compras y Rubén como miembro.
- Tres proveedores invitados por Norte que subieron su RUT y su Cámara de comercio; Rubén ya aprobó el área Compras de uno de ellos.

**Pasos**

1. Mira la bandeja de revisión
2. Filtra por el estado de tu área
3. Limpia los filtros y abre un expediente

## Paso 1. Mira la bandeja de revisión

La bandeja lista un expediente por cada área que debe decidir: el folio, el tercero, el tipo de vínculo, el área, el estado de tu área, las aprobaciones que lleva, los documentos por decidir, desde cuándo espera y su plazo de revisión. Ves solo los expedientes de las áreas de las que eres miembro.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 1: Mira la bandeja de revisión (escritorio)](../../../salida/manual/img/revision/bandeja-de-revision/01-mira-la-bandeja-de-revision-claro.webp)

**Variantes**

- **El área tiene plazo de revisión:** La columna «Plazo de revisión» muestra «A tiempo», «Por vencer» o «Vencido», con la fecha en que vence.
- **El área no tiene plazo:** La celda queda con un guion y no hay avisos de vencimiento para ese expediente.

**Configuración que lo afecta**

- **Plazos de revisión por área** (`Configuración › Plazos de revisión (/configuracion/plazos-de-revision)`): El plazo de cada área decide cuándo aparece «Por vencer» o «Vencido». Si el flujo define su propio plazo, ese manda.

## Paso 2. Filtra por el estado de tu área

El filtro «Estado del área» deja ver solo los expedientes en el estado que elijas: «En espera», «Pendiente», «Aprobada» u «Observada». Aquí quedó «Aprobada»: el expediente de Tejidos del Caribe, que ya aprobaste. El filtro «Área» hace lo mismo por área, y el botón «Limpiar filtros» los quita todos.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 2: Filtra por el estado de tu área (escritorio)](../../../salida/manual/img/revision/bandeja-de-revision/02-filtra-por-el-estado-de-tu-area-claro.webp)

## Paso 3. Limpia los filtros y abre un expediente

Al limpiar los filtros vuelven a aparecer todos los expedientes que esperan a tus áreas. Toca una fila para abrir el expediente y revisar sus documentos y decidir.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 3: Limpia los filtros y abre un expediente (escritorio)](../../../salida/manual/img/revision/bandeja-de-revision/03-limpia-los-filtros-y-abre-un-expediente-claro.webp)

## Recorridos relacionados

- [Revisa lo que te toca desde Inicio](inicio-lo-que-te-toca.md)
- [Aprueba el área de Compras](aprobar-un-area.md)

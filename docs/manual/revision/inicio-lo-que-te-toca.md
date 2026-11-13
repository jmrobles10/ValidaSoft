---
id: "revision/inicio-lo-que-te-toca"
titulo: "Revisa lo que te toca desde Inicio"
actor: "Revisor/área"
dominio: "revision"
resumen: "Rubén, miembro del área Compras, abre Inicio y ve de un vistazo los expedientes que esperan su decisión, los indicadores de la empresa y llega a la bandeja filtrada por su área."
pasos: 3
---

# Revisa lo que te toca desde Inicio

**Actor:** Revisor/área · **Dominio:** Revisión de expedientes

Rubén, miembro del área Compras, abre Inicio y ve de un vistazo los expedientes que esperan su decisión, los indicadores de la empresa y llega a la bandeja filtrada por su área.

**Antes de empezar**

- Un grupo con Constructora Norte, con el área Compras y Rubén como miembro.
- Dos proveedores invitados por Norte que ya aceptaron y subieron su RUT y su Cámara de comercio: sus expedientes están en revisión.

**Pasos**

1. Abre Inicio y mira lo que te toca
2. Revisa los indicadores de la empresa
3. Pasa a la bandeja de tu área

## Paso 1. Abre Inicio y mira lo que te toca

Inicio abre primero con «Lo que te toca»: una línea por cada área de la que eres miembro, con los expedientes que esperan una decisión suya y la antigüedad del más viejo. Si no hay nada pendiente, la pantalla dice que estás al día. Ves solo las áreas de las que eres miembro, no las de toda la empresa.

> **Pantalla:** `/e/{empresa}/inicio`

![Paso 1: Abre Inicio y mira lo que te toca (escritorio)](../../../salida/manual/img/revision/inicio-lo-que-te-toca/01-abre-inicio-y-mira-lo-que-te-toca-claro.webp)

**Variantes**

- **Eres miembro de varias áreas:** Cada área aparece en su propia línea, con su número de expedientes.
- **No tienes nada pendiente:** La sección muestra «Estás al día» en lugar de la lista.

## Paso 2. Revisa los indicadores de la empresa

Debajo de «Lo que te toca» están los indicadores: vínculos, expedientes y certificados al momento, y los aprobados, rechazados y los días hasta la aprobación en la ventana que elijas (7, 30 o 90 días). Las cifras de «al momento» no cambian con la ventana; las de tiempo sí. Tocar «Aprobados» lleva al informe de estado con la misma ventana.

> **Pantalla:** `/e/{empresa}/inicio`

![Paso 2: Revisa los indicadores de la empresa (escritorio)](../../../salida/manual/img/revision/inicio-lo-que-te-toca/02-revisa-los-indicadores-de-la-empresa-claro.webp)

**Configuración que lo afecta**

- **Ventana de los indicadores**: Cambia el periodo de los aprobados, rechazados y tiempos; la ventana por defecto es 30 días.

## Paso 3. Pasa a la bandeja de tu área

Al tocar la línea de Compras se abre la bandeja de revisión con el filtro de tu área ya puesto. Cada fila es un expediente que espera tu decisión: el tercero, el tipo de vínculo y el plazo. Desde aquí se abre el expediente para revisarlo y decidir.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 3: Pasa a la bandeja de tu área (escritorio)](../../../salida/manual/img/revision/inicio-lo-que-te-toca/03-pasa-a-la-bandeja-de-tu-area-claro.webp)

## Recorridos relacionados

- [Filtra la bandeja de revisión y abre un expediente](bandeja-de-revision.md)
- [Aprueba el área de Compras](aprobar-un-area.md)

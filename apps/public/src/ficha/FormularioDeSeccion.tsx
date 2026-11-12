import type { EstadoFichaDto } from '@validasoft/api-client';
import type { ReactNode } from 'react';
import { FormularioEspecialidades } from './especialidades/FormularioEspecialidades';
import { FormularioFinanciera } from './formularios/FormularioFinanciera';
import { FormularioGeneral } from './formularios/FormularioGeneral';
import { FormularioSst } from './formularios/FormularioSst';
import { FormularioTributaria } from './formularios/FormularioTributaria';
import { FormularioLegal } from './legal/FormularioLegal';
import type { CodigoSeccion } from './secciones';
import type { EditorSeccion } from './useEditorSeccion';

/** El formulario de cada sección sobre el mismo editor (datos en snake_case, mensajes del servidor por ruta). */
export function FormularioDeSeccion({ codigo, estado, editor, selectorPeriodo }: { codigo: CodigoSeccion; estado: EstadoFichaDto; editor: EditorSeccion; selectorPeriodo?: ReactNode }) {
  const comun = { onCambiar: editor.cambiar, mensaje: editor.mensaje, hayEnRuta: editor.hayEnRuta, deshabilitado: editor.guardando };
  switch (codigo) {
    case 'FICHA_GENERAL':
      return <FormularioGeneral {...comun} datos={editor.datos} tipoPersona={estado.tipoPersona} />;
    case 'FICHA_TRIBUTARIA':
      return <FormularioTributaria {...comun} datos={editor.datos} />;
    case 'FICHA_LEGAL':
      return <FormularioLegal {...comun} datos={editor.datos} />;
    case 'FICHA_FINANCIERA':
      return <FormularioFinanciera {...comun} datos={editor.datos} selectorPeriodo={selectorPeriodo} />;
    case 'FICHA_SST':
      return <FormularioSst {...comun} datos={editor.datos} />;
    case 'FICHA_ESPECIALIDADES':
      return <FormularioEspecialidades {...comun} datos={editor.datos} />;
  }
}

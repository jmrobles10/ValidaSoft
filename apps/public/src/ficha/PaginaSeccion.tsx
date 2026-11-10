import { Button } from '@mui/material';
import type { EstadoFichaDto } from '@validasoft/api-client';
import { PaginaEstado } from '@validasoft/ui';
import { ClipboardX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useParams, useSearchParams } from 'react-router';
import { ErrorRecuperable, EsqueletoTarjetas } from '../shared/componentes';
import { useEstadoFicha, useSeccionFicha } from './api';
import { EditorSeccion } from './EditorSeccion';
import { periodoPorOmision, seccionPorRuta, type DefinicionSeccion } from './secciones';

/**
 * Formulario de una sección del wizard (`/ficha/:seccion`, la financiera con `?periodo=`). Lee el estado de la ficha
 * (tipo de persona y orden) y la versión vigente de la sección; una sección que no está en el orden (la persona
 * natural no diligencia Legal ni Financiera) no se abre.
 */
export function PaginaSeccion() {
  const { t } = useTranslation();
  const { seccion: ruta } = useParams();
  const [parametros] = useSearchParams();
  const definicion = seccionPorRuta(ruta);
  const estado = useEstadoFicha();

  const volver = (
    <Button component={RouterLink} to="/ficha" variant="contained">
      {t('ficha.volverResumen')}
    </Button>
  );
  if (!definicion) return <PaginaEstado icono={ClipboardX} titulo={t('ficha.noExisteTitulo')} texto={t('ficha.noAplicaTexto')} acciones={volver} />;
  if (estado.isPending) return <EsqueletoTarjetas cantidad={3} />;
  if (estado.isError) return <ErrorRecuperable error={estado.error} onReintentar={() => void estado.refetch()} />;
  if (!estado.data.orden.includes(definicion.codigo)) {
    return <PaginaEstado icono={ClipboardX} titulo={t('ficha.noAplicaTitulo')} texto={t('ficha.noAplicaTexto')} acciones={volver} />;
  }

  const pedido = Number(parametros.get('periodo'));
  const periodo = definicion.porPeriodo ? (Number.isInteger(pedido) && pedido >= 1990 ? pedido : periodoPorOmision(estado.data)) : null;
  return <CargaSeccion key={`${definicion.codigo}-${String(periodo)}`} definicion={definicion} periodo={periodo} estado={estado.data} />;
}

function CargaSeccion({ definicion, periodo, estado }: { definicion: DefinicionSeccion; periodo: number | null; estado: EstadoFichaDto }) {
  const consulta = useSeccionFicha(definicion.codigo, periodo);
  if (consulta.isPending) return <EsqueletoTarjetas cantidad={3} />;
  if (consulta.isError) return <ErrorRecuperable error={consulta.error} onReintentar={() => void consulta.refetch()} />;
  return <EditorSeccion definicion={definicion} periodo={periodo} estado={estado} inicial={consulta.data} />;
}

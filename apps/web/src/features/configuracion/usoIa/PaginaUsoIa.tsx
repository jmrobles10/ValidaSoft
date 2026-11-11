import { MenuItem, Stack, TextField } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { EsqueletoTarjeta, EstadoVacio } from '@validasoft/ui';
import { Sparkles } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useSearchParams } from 'react-router';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { AyudaUsoIa } from './AyudaUsoIa';
import { useUsoIa } from './api';
import { etiquetaMes, leerMes, mesActual, tienePlanActivo, ultimosMeses } from './modelo';
import { SerieDiaria } from './SerieDiaria';
import { TarjetasUso } from './TarjetasUso';

/**
 * «Plan y uso de IA» de la empresa activa (ADR-0012, IA-8): validaciones incluidas y adicionales del mes, análisis
 * pagados con su llave, costo real y tokens, y la serie por día. El mes va en la URL (`?mes=`; el actual no se escribe).
 * El aviso de cupo del backend enlaza aquí (`/e/{empresa}/uso-ia`).
 */
export function PaginaUsoIa() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const [parametros, setParametros] = useSearchParams();
  const actual = mesActual();
  const meses = useMemo(() => ultimosMeses(actual), [actual]);
  const mes = leerMes(parametros, meses) ?? actual;
  const consulta = useUsoIa(empresa.id, mes, puedeLeer);
  const datos = consulta.data;

  if (!puedeLeer) return <SinPermiso seccion={t('nav.uso-ia')} />;

  const cambiarMes = (valor: string) =>
    setParametros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor === actual) nuevos.delete('mes');
        else nuevos.set('mes', valor);
        return nuevos;
      },
      { replace: true },
    );

  return (
    <>
      <EncabezadoPagina
        titulo={t('ia.uso.titulo')}
        subtitulo={t('ia.uso.subtitulo', { empresa: empresa.nombre })}
        acciones={
          <TextField select size="small" label={t('ia.uso.mes')} value={mes} onChange={(e) => cambiarMes(e.target.value)} sx={{ minWidth: 200 }}>
            {meses.map((m) => (
              <MenuItem key={m} value={m}>
                {etiquetaMes(m)}
              </MenuItem>
            ))}
          </TextField>
        }
      />
      {consulta.isError && !datos ? (
        <ErrorRecuperable mensaje={`${t('ia.uso.error')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />
      ) : !datos ? (
        <Stack spacing={1.5} aria-busy>
          <EsqueletoTarjeta alto={120} />
          <EsqueletoTarjeta alto={180} />
        </Stack>
      ) : !tienePlanActivo(datos) ? (
        <Tarjeta>
          <EstadoVacio icono={Sparkles} titulo={t('ia.uso.sinPlanTitulo')} texto={t('ia.uso.sinPlan')} />
        </Tarjeta>
      ) : (
        <Stack spacing={1.5} sx={{ opacity: consulta.isPlaceholderData ? 0.6 : 1, transition: 'opacity 120ms' }} aria-busy={consulta.isPlaceholderData || undefined}>
          <TarjetasUso uso={datos} />
          <SerieDiaria mes={datos.mes} porDia={datos.porDia} />
        </Stack>
      )}
      <AyudaUsoIa />
    </>
  );
}

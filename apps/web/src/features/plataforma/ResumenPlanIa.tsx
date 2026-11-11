import { Chip, Typography } from '@mui/material';
import type { PlanIaEmpresaDto } from '@validasoft/api-client';
import { Dato, formatearFechaHora, RejillaDatos } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../shared/componentes';
import { llaveEnmascarada, type BorradorPlanIa } from './modelo';

/**
 * Resumen del plan tal como quedaría con lo escrito (se actualiza mientras se edita): estado, cupo, adicionales, llave, modelos y
 * umbral, más cuándo se guardó por última vez. Llena la columna derecha de la página junto a la llave (UI-5) y deja ver de un
 * vistazo qué se va a guardar sin recorrer los campos.
 */
export function ResumenPlanIa({ plan, borrador }: { plan: PlanIaEmpresaDto; borrador: BorradorPlanIa }) {
  const { t } = useTranslation();
  const llave =
    borrador.llave.tipo === 'quitar'
      ? t('plataforma.planIa.resumen.llaveSeQuitara')
      : borrador.llave.tipo === 'reemplazar' && borrador.llave.valor
        ? t('plataforma.planIa.resumen.llaveNueva')
        : plan.llaveConfigurada
          ? t('plataforma.planIa.resumen.llaveCon', { llave: llaveEnmascarada(plan.ultimosCuatro) })
          : t('plataforma.planIa.resumen.sinLlave');
  const adicionales = !borrador.adicional
    ? t('plataforma.planIa.resumen.adicionalesNo')
    : borrador.tope.trim()
      ? t('plataforma.planIa.resumen.adicionalesConTope', { tope: borrador.tope.trim() })
      : t('plataforma.planIa.resumen.adicionalesSinTope');

  return (
    <Tarjeta titulo={t('plataforma.planIa.resumen.titulo')} etiqueta={t('plataforma.planIa.resumen.titulo')}>
      <RejillaDatos columnas={2}>
        <Dato etiqueta={t('plataforma.planIa.resumen.estado')}>
          <Chip size="small" color={borrador.activo ? 'success' : 'default'} variant="outlined" label={borrador.activo ? t('plataforma.planIa.resumen.activo') : t('plataforma.planIa.resumen.inactivo')} />
        </Dato>
        <Dato etiqueta={t('plataforma.planIa.resumen.llave')}>{llave}</Dato>
        <Dato etiqueta={t('plataforma.planIa.resumen.cupo')}>{t('plataforma.planIa.resumen.cupoValor', { cupo: borrador.incluidas.trim() || '—' })}</Dato>
        <Dato etiqueta={t('plataforma.planIa.resumen.adicionales')}>{adicionales}</Dato>
        <Dato etiqueta={t('plataforma.planIa.modeloPorDefecto')}>
          <Typography component="span" variant="mono">
            {borrador.modeloPorDefecto.trim() || '—'}
          </Typography>
        </Dato>
        <Dato etiqueta={t('plataforma.planIa.modeloEscalamiento')}>
          <Typography component="span" variant="mono">
            {borrador.modeloEscalamiento.trim() || '—'}
          </Typography>
        </Dato>
        <Dato etiqueta={t('plataforma.planIa.umbral')}>{borrador.umbral.trim() || '—'}</Dato>
        <Dato etiqueta={t('plataforma.planIa.resumen.actualizado')}>{plan.actualizadoEn ? formatearFechaHora(plan.actualizadoEn) : t('plataforma.planIa.resumen.sinGuardar')}</Dato>
      </RejillaDatos>
    </Tarjeta>
  );
}

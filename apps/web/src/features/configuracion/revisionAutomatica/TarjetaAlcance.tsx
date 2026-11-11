import { Box, Chip, Typography } from '@mui/material';
import type { RevisionAutomaticaConfigDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { CampoNumero } from '../../../shared/camposTransitorios';
import { MOTIVOS_QUE_LLEVAN_A_UNA_PERSONA } from '../../revisionAutomatica/modelo';
import { MAXIMO_ESPERA, MAXIMO_TOPE, type BorradorRevision, type ErroresDeRevision } from './modelo';
import { SelectorMultiple } from './SelectorMultiple';

export interface TarjetaAlcanceProps {
  borrador: BorradorRevision;
  onCambiar: (cambio: Partial<BorradorRevision>) => void;
  config: Pick<RevisionAutomaticaConfigDto, 'tiposAnalizables' | 'areas'>;
  errores: ErroresDeRevision;
  deshabilitado: boolean;
}

/**
 * Lo que el nivel Automático aprueba sin que una persona lo vea: los tipos de documento y las áreas autorizados, cuánto se
 * confirma por muestreo, el tope mensual y cuánto se espera el análisis. Lo que queda fuera lo revisa una persona.
 */
export function TarjetaAlcance({ borrador, onCambiar, config, errores, deshabilitado }: TarjetaAlcanceProps) {
  const { t } = useTranslation();
  return (
    <Tarjeta titulo={t('revisionAutomatica.config.alcance.titulo')}>
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
        {t('revisionAutomatica.config.alcance.ayuda')}
      </Typography>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: 'repeat(2, minmax(0, 1fr))' }, gap: 1.5 }}>
        <SelectorMultiple
          etiqueta={t('revisionAutomatica.config.alcance.tipos')}
          placeholder={t('revisionAutomatica.config.alcance.placeholderTipos')}
          opciones={config.tiposAnalizables.map((x) => ({ valor: x.codigo, etiqueta: x.nombre }))}
          elegidos={borrador.tipos}
          onCambiar={(tipos) => onCambiar({ tipos })}
          ayuda={errores.tipos ? t('revisionAutomatica.config.alcance.sinTipos') : undefined}
          deshabilitado={deshabilitado}
        />
        <SelectorMultiple
          etiqueta={t('revisionAutomatica.config.alcance.areas')}
          placeholder={t('revisionAutomatica.config.alcance.placeholderAreas')}
          opciones={config.areas.map((x) => ({ valor: x.id, etiqueta: x.nombre }))}
          elegidos={borrador.areas}
          onCambiar={(areas) => onCambiar({ areas })}
          ayuda={errores.areas ? t('revisionAutomatica.config.alcance.sinAreas') : undefined}
          deshabilitado={deshabilitado}
        />
      </Box>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: 'repeat(3, minmax(0, 1fr))' }, gap: 1.5, mt: 1.5 }}>
        <CampoNumero
          fullWidth
          etiqueta={t('revisionAutomatica.config.alcance.muestreo')}
          valor={borrador.muestreo}
          onCambiar={(muestreo) => onCambiar({ muestreo })}
          maxDigitos={3}
          error={Boolean(errores.muestreo)}
          ayuda={errores.muestreo ? t('revisionAutomatica.config.alcance.muestreoRango') : t('revisionAutomatica.config.alcance.muestreoAyuda')}
          deshabilitado={deshabilitado}
        />
        <CampoNumero
          fullWidth
          etiqueta={t('revisionAutomatica.config.alcance.tope')}
          valor={borrador.tope}
          onCambiar={(tope) => onCambiar({ tope })}
          maxDigitos={String(MAXIMO_TOPE).length}
          error={Boolean(errores.tope)}
          ayuda={errores.tope ? t('revisionAutomatica.config.alcance.topeRango', { maximo: MAXIMO_TOPE.toLocaleString('es-CO') }) : t('revisionAutomatica.config.alcance.topeAyuda')}
          deshabilitado={deshabilitado}
        />
        <CampoNumero
          fullWidth
          etiqueta={t('revisionAutomatica.config.alcance.espera')}
          valor={borrador.espera}
          onCambiar={(espera) => onCambiar({ espera })}
          maxDigitos={String(MAXIMO_ESPERA).length}
          error={Boolean(errores.espera)}
          ayuda={errores.espera ? t('revisionAutomatica.config.alcance.esperaRango', { maximo: String(MAXIMO_ESPERA) }) : t('revisionAutomatica.config.alcance.esperaAyuda')}
          deshabilitado={deshabilitado}
        />
      </Box>
      <Typography variant="body2" sx={{ mt: 2, mb: 0.75 }}>
        {t('revisionAutomatica.config.alcance.siempre')}
      </Typography>
      <Box component="ul" sx={{ listStyle: 'none', m: 0, p: 0, display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
        {MOTIVOS_QUE_LLEVAN_A_UNA_PERSONA.map((codigo) => (
          <Box component="li" key={codigo}>
            <Chip size="small" variant="outlined" label={t(`revisionAutomatica.motivos.${codigo}`)} sx={{ color: 'text.primary', borderRadius: (th) => `${String(th.radio.xs)}px` }} />
          </Box>
        ))}
      </Box>
    </Tarjeta>
  );
}

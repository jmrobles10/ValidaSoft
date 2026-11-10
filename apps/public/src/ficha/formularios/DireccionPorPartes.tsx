import { Accordion, AccordionDetails, AccordionSummary, Checkbox, FormControlLabel, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { ChevronDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { CampoTexto, Rejilla } from '../campos';
import type { DireccionEstructurada } from '../tipos';

const TIPOS_VIA = ['Calle', 'Carrera', 'Avenida', 'AvenidaCalle', 'AvenidaCarrera', 'Diagonal', 'Transversal', 'Circular', 'Autopista', 'Kilometro'] as const;
const CUADRANTES = ['Sur', 'Este', 'Norte', 'Oeste'] as const;
const BASE = 'direccion_estructurada';

interface Props {
  valor: DireccionEstructurada | null | undefined;
  onCambiar: (valor: DireccionEstructurada | null) => void;
  mensaje: (ruta: string) => string | null;
  deshabilitado: boolean;
}

/**
 * Dirección en el formato estructurado de AyF (opcional, FM-19): «Calle 45 # 12 - 08, Oficina 301». Va plegada; se abre
 * sola si ya tiene datos o si el servidor marcó alguno de sus campos.
 */
export function DireccionPorPartes({ valor, onCambiar, mensaje, deshabilitado }: Props) {
  const { t } = useTranslation();
  const actual = valor ?? {};
  const campos = ['tipo_via', 'numero_via', 'letra_via', 'cuadrante', 'numero_cruce', 'letra_cruce', 'placa', 'complemento'] as const;
  const conError = campos.some((c) => mensaje(`${BASE}.${c}`));
  const cambiar = (cambio: DireccionEstructurada) => {
    const nueva = { ...actual, ...cambio };
    onCambiar(Object.values(nueva).some((v) => v !== null) ? nueva : null);
  };
  const error = (campo: (typeof campos)[number]) => mensaje(`${BASE}.${campo}`);

  return (
    <Accordion variant="outlined" disableGutters defaultExpanded={Boolean(valor) || conError}>
      <AccordionSummary expandIcon={<ChevronDown size={18} aria-hidden />}>
        <Stack>
          <Typography sx={{ fontWeight: 500 }}>{t('ficha.general.estructurada')}</Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('ficha.general.estructuradaAyuda')}</Typography>
        </Stack>
      </AccordionSummary>
      <AccordionDetails>
        <Stack spacing={2}>
          <Rejilla>
            <TextField
              select
              label={t('ficha.general.tipoVia')}
              value={actual.tipo_via ?? ''}
              onChange={(e) => cambiar({ tipo_via: e.target.value || null })}
              error={Boolean(error('tipo_via'))}
              helperText={error('tipo_via')}
              disabled={deshabilitado}
              fullWidth
            >
              <MenuItem value="">{t('ficha.general.ninguno')}</MenuItem>
              {TIPOS_VIA.map((tipo) => (
                <MenuItem key={tipo} value={tipo}>
                  {t(`ficha.general.tiposVia.${tipo}`)}
                </MenuItem>
              ))}
            </TextField>
            <CampoTexto etiqueta={t('ficha.general.numeroVia')} valor={actual.numero_via} onCambiar={(v) => cambiar({ numero_via: v })} mensaje={error('numero_via')} inputMode="numeric" maxLength={4} deshabilitado={deshabilitado} />
            <CampoTexto etiqueta={t('ficha.general.letraVia')} valor={actual.letra_via} onCambiar={(v) => cambiar({ letra_via: v })} mensaje={error('letra_via')} maxLength={2} deshabilitado={deshabilitado} />
            <FormControlLabel
              control={<Checkbox checked={actual.bis === true} onChange={(e) => cambiar({ bis: e.target.checked ? true : null })} />}
              label={t('ficha.general.bis')}
              disabled={deshabilitado}
            />
            <TextField
              select
              label={t('ficha.general.cuadrante')}
              value={actual.cuadrante ?? ''}
              onChange={(e) => cambiar({ cuadrante: e.target.value || null })}
              error={Boolean(error('cuadrante'))}
              helperText={error('cuadrante')}
              disabled={deshabilitado}
              fullWidth
            >
              <MenuItem value="">{t('ficha.general.ninguno')}</MenuItem>
              {CUADRANTES.map((c) => (
                <MenuItem key={c} value={c}>
                  {t(`ficha.general.cuadrantes.${c}`)}
                </MenuItem>
              ))}
            </TextField>
            <CampoTexto etiqueta={t('ficha.general.numeroCruce')} valor={actual.numero_cruce} onCambiar={(v) => cambiar({ numero_cruce: v })} mensaje={error('numero_cruce')} inputMode="numeric" maxLength={4} deshabilitado={deshabilitado} />
            <CampoTexto etiqueta={t('ficha.general.letraCruce')} valor={actual.letra_cruce} onCambiar={(v) => cambiar({ letra_cruce: v })} mensaje={error('letra_cruce')} maxLength={2} deshabilitado={deshabilitado} />
            <CampoTexto etiqueta={t('ficha.general.placa')} valor={actual.placa} onCambiar={(v) => cambiar({ placa: v })} mensaje={error('placa')} maxLength={10} deshabilitado={deshabilitado} />
          </Rejilla>
          <CampoTexto etiqueta={t('ficha.general.complemento')} valor={actual.complemento} onCambiar={(v) => cambiar({ complemento: v })} mensaje={error('complemento')} ayuda={t('ficha.general.complementoAyuda')} deshabilitado={deshabilitado} />
        </Stack>
      </AccordionDetails>
    </Accordion>
  );
}

import { Checkbox, FormControl, FormControlLabel, FormGroup, FormHelperText, FormLabel, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { CampoCifra, CampoFechaFicha, CampoSiNo, Rejilla } from '../campos';
import { cuadre, formatearPesos } from '../cifras';
import { PAISES } from '../paises';
import { SelectorMultiple } from '../selectores';
import { TIPOS_OPERACION, type DatosFinancieros, type MonedaExtranjera, type PropsFormulario, type TipoOperacionExtranjera } from '../tipos';

type Cifra = 'activos' | 'pasivos' | 'patrimonio' | 'ingresos_brutos' | 'ingresos_netos' | 'gastos_operacionales' | 'gastos_no_operacionales' | 'otros_ingresos';

type EtiquetaCifra = 'activos' | 'pasivos' | 'patrimonio' | 'ingresosBrutos' | 'ingresosNetos' | 'gastosOperacionales' | 'gastosNoOperacionales' | 'otrosIngresos';

const CIFRAS: readonly { clave: Cifra; etiqueta: EtiquetaCifra }[] = [
  { clave: 'activos', etiqueta: 'activos' },
  { clave: 'pasivos', etiqueta: 'pasivos' },
  { clave: 'patrimonio', etiqueta: 'patrimonio' },
  { clave: 'ingresos_brutos', etiqueta: 'ingresosBrutos' },
  { clave: 'ingresos_netos', etiqueta: 'ingresosNetos' },
  { clave: 'gastos_operacionales', etiqueta: 'gastosOperacionales' },
  { clave: 'gastos_no_operacionales', etiqueta: 'gastosNoOperacionales' },
  { clave: 'otros_ingresos', etiqueta: 'otrosIngresos' },
];

/**
 * Información financiera de un periodo (`FICHA_FINANCIERA`): el año y la fecha de corte en la misma fila (UI-8: son campos
 * cortos), las cifras con el cuadre activos = pasivos + patrimonio en vivo (informativo: la completitud la dice el
 * servidor) y las operaciones en moneda extranjera.
 */
export function FormularioFinanciera({ datos, onCambiar, mensaje, deshabilitado, selectorPeriodo }: PropsFormulario<DatosFinancieros> & { selectorPeriodo?: ReactNode }) {
  const { t } = useTranslation();
  const moneda: MonedaExtranjera = datos.moneda_extranjera ?? {};
  const cambiarMoneda = (cambio: MonedaExtranjera) => onCambiar({ moneda_extranjera: { ...moneda, ...cambio } });
  const tipos = moneda.tipos ?? [];
  const alternarTipo = (tipo: TipoOperacionExtranjera, marcado: boolean) => {
    const lista = marcado ? [...tipos, tipo] : tipos.filter((x) => x !== tipo);
    cambiarMoneda({ tipos: lista.length > 0 ? lista : null });
  };
  const balance = cuadre(datos.activos, datos.pasivos, datos.patrimonio);

  return (
    <Stack spacing={2.5}>
      <Rejilla>
        {selectorPeriodo}
        <CampoFechaFicha etiqueta={t('ficha.financiera.fechaCorte')} valor={datos.fecha_corte} onCambiar={(v) => onCambiar({ fecha_corte: v })} mensaje={mensaje('fecha_corte')} deshabilitado={deshabilitado} />
      </Rejilla>
      <Rejilla>
        {CIFRAS.map(({ clave, etiqueta }) => (
          <CampoCifra
            key={clave}
            etiqueta={t(`ficha.financiera.${etiqueta}`)}
            valor={datos[clave]}
            onCambiar={(v) => onCambiar({ [clave]: v })}
            mensaje={mensaje(clave)}
            ayuda={t('ficha.numeroAyuda')}
            deshabilitado={deshabilitado}
          />
        ))}
      </Rejilla>
      <Paper variant="outlined" role="status" aria-label={t('ficha.financiera.cuadreTitulo')} sx={{ p: 1.5, bgcolor: 'background.default' }}>
        <Typography sx={{ fontWeight: 500, fontSize: 14 }}>{t('ficha.financiera.cuadreTitulo')}</Typography>
        {balance ? (
          <Typography sx={{ fontSize: 14, color: balance.cuadra ? 'success.main' : 'warning.main' }}>
            {t('ficha.financiera.cuadre', { activos: formatearPesos(datos.activos ?? 0), suma: formatearPesos(balance.suma) })}
            {' · '}
            {balance.diferencia === 0 || balance.cuadra ? t('ficha.financiera.cuadra') : t('ficha.financiera.noCuadra', { diferencia: formatearPesos(Math.abs(balance.diferencia)) })}
          </Typography>
        ) : (
          <Typography sx={{ fontSize: 14, color: 'text.secondary' }}>{t('ficha.financiera.cuadreIncompleto')}</Typography>
        )}
      </Paper>
      <CampoSiNo
        etiqueta={t('ficha.financiera.monedaRealiza')}
        valor={moneda.realiza}
        onCambiar={(realiza) => cambiarMoneda(realiza ? { realiza } : { realiza, tipos: null, paises: null })}
        mensaje={mensaje('moneda_extranjera.realiza')}
        deshabilitado={deshabilitado}
      />
      {moneda.realiza === true && (
        <>
          <FormControl component="fieldset" error={Boolean(mensaje('moneda_extranjera.tipos'))} disabled={deshabilitado}>
            <FormLabel component="legend">{t('ficha.financiera.monedaTipos')}</FormLabel>
            <FormGroup>
              {TIPOS_OPERACION.map((tipo) => (
                <FormControlLabel
                  key={tipo}
                  control={<Checkbox checked={tipos.includes(tipo)} onChange={(e) => alternarTipo(tipo, e.target.checked)} />}
                  label={t(`ficha.financiera.tipos.${tipo}`)}
                  sx={{ minHeight: 40 }}
                />
              ))}
            </FormGroup>
            {mensaje('moneda_extranjera.tipos') && <FormHelperText>{mensaje('moneda_extranjera.tipos')}</FormHelperText>}
          </FormControl>
          <SelectorMultiple
            etiqueta={t('ficha.financiera.monedaPaises')}
            opciones={PAISES}
            valor={moneda.paises}
            onCambiar={(paises) => cambiarMoneda({ paises })}
            etiquetaOpcion={(o) => o.nombre}
            mensaje={mensaje('moneda_extranjera.paises')}
            deshabilitado={deshabilitado}
          />
        </>
      )}
    </Stack>
  );
}

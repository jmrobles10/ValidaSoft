import { Alert, Box, Button, FormControlLabel, Stack, Switch, TextField, Typography } from '@mui/material';
import type { UsoDelGrupoDto } from '@validasoft/api-client';
import { CabeceraEsqueleto, CampoNumero, DosColumnas, EsqueletoFormulario, EsqueletoTarjeta, FaltaParaEnviar } from '@validasoft/ui';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';
import { EncabezadoPagina, EnlaceVolver, ErrorRecuperable, IconoCarga, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { etiquetaMes } from '../configuracion/usoIa/modelo';
import { useGuardarPlanDeUnGrupo, usePlanDeUnGrupo } from '../plan/api';
import { MedidoresDeUso } from '../plan/MedidorDeUso';
import { borradorDesdePlan, cuerpoPlan, hayCambiosDePlan, LONGITUD_MAXIMA_CODIGO, validarPlan, type BorradorPlan } from '../plan/modelo';
import { useGrupo } from './api';

function Formulario({ tenantId, uso, grupo }: { tenantId: string; uso: UsoDelGrupoDto; grupo: string }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const guardar = useGuardarPlanDeUnGrupo(tenantId);
  const [b, setB] = useState<BorradorPlan>(() => borradorDesdePlan(uso.plan));
  const [tocado, setTocado] = useState(false);
  const errores = validarPlan(b);
  const cambios = hayCambiosDePlan(b, uso.plan);
  const cambiar = (parcial: Partial<BorradorPlan>) => setB((p) => ({ ...p, ...parcial }));

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    setTocado(true);
    if (errores.length > 0 || !cambios) return;
    guardar.mutate(cuerpoPlan(b), {
      onSuccess: (guardado) => {
        mostrar(t('plataforma.plan.guardado', { grupo }));
        setB(borradorDesdePlan(guardado.plan));
        setTocado(false);
      },
    });
  };

  const limite = (campo: 'usuarios' | 'empresas' | 'vinculos' | 'almacenamientoMb' | 'llamadas', etiqueta: string, ayuda: string, maxDigitos = 9) => (
    <CampoNumero etiqueta={etiqueta} valor={b[campo]} onCambiar={(v) => cambiar({ [campo]: v })} ayuda={ayuda} maxDigitos={maxDigitos} fullWidth placeholder={t('plataforma.plan.sinLimite')} />
  );

  return (
    <Box component="form" onSubmit={enviar} noValidate>
      <EncabezadoPagina
        antes={<EnlaceVolver a="/plataforma/grupos">{t('plataforma.plan.volver')}</EnlaceVolver>}
        titulo={t('plataforma.plan.titulo')}
        subtitulo={t('plataforma.plan.subtitulo', { grupo })}
        acciones={
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
            {tocado && errores[0] && <FaltaParaEnviar>{t('plataforma.plan.errores.codigo-invalido', { maximo: String(LONGITUD_MAXIMA_CODIGO) })}</FaltaParaEnviar>}
            <Button type="submit" variant="contained" disabled={guardar.isPending || !cambios} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
              {t('plataforma.plan.guardar')}
            </Button>
          </Stack>
        }
      />
      {!uso.plan.configurado && (
        <Alert severity="info" sx={{ mb: 2 }}>
          {t('plataforma.plan.sinConfigurar')}
        </Alert>
      )}
      {guardar.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {mensajeDeError(guardar.error, t)}
        </Alert>
      )}
      <DosColumnas
        izquierda={
          <>
            <Tarjeta titulo={t('plataforma.plan.plan')}>
              <Stack spacing={2}>
                <TextField
                  label={t('plataforma.plan.codigo')}
                  value={b.codigo}
                  onChange={(e) => cambiar({ codigo: e.target.value })}
                  error={tocado && errores.length > 0}
                  helperText={tocado && errores[0] ? t('plataforma.plan.errores.codigo-invalido', { maximo: String(LONGITUD_MAXIMA_CODIGO) }) : t('plataforma.plan.codigoAyuda')}
                  slotProps={{ htmlInput: { maxLength: LONGITUD_MAXIMA_CODIGO, autoComplete: 'off' } }}
                  sx={{ maxWidth: { sm: 'calc(50% - 6px)' } }}
                />
                <Box>
                  <FormControlLabel control={<Switch checked={b.bloqueoSuave} onChange={(e) => cambiar({ bloqueoSuave: e.target.checked })} />} label={t('plataforma.plan.bloqueoSuave')} />
                  <Typography variant="caption" component="p" sx={{ color: 'text.secondary' }}>
                    {b.bloqueoSuave ? t('plataforma.plan.bloqueoActivo') : t('plataforma.plan.bloqueoInactivo')}
                  </Typography>
                </Box>
              </Stack>
            </Tarjeta>
            <Tarjeta titulo={t('plataforma.plan.limites')}>
              <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
                {t('plataforma.plan.limitesAyuda')}
              </Typography>
              <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))' }, gap: 2 }}>
                {limite('usuarios', t('plan.metricas.Usuarios.nombre'), t('plataforma.plan.ayudas.usuarios'))}
                {limite('empresas', t('plan.metricas.Empresas.nombre'), t('plataforma.plan.ayudas.empresas'))}
                {limite('vinculos', t('plan.metricas.VinculosActivos.nombre'), t('plataforma.plan.ayudas.vinculos'))}
                {limite('almacenamientoMb', t('plataforma.plan.almacenamientoMb'), t('plataforma.plan.ayudas.almacenamiento'), 12)}
                {limite('llamadas', t('plataforma.plan.llamadas'), t('plataforma.plan.ayudas.llamadas'), 12)}
              </Box>
            </Tarjeta>
          </>
        }
        derecha={
          <Tarjeta titulo={t('plataforma.plan.usoActual', { mes: etiquetaMes(uso.mes).toLowerCase() })}>
            <MedidoresDeUso mediciones={uso.mediciones} />
            <Typography sx={{ mt: 2, fontSize: 12, color: 'text.secondary', lineHeight: '16px' }}>{t('plataforma.plan.usoNota')}</Typography>
          </Tarjeta>
        }
      />
    </Box>
  );
}

/** Plan y límites de un grupo (OD-14, OD-17): código, límites de usuarios, empresas, vínculos, almacenamiento y llamadas a la API, bloqueo suave y el uso actual. */
export function PaginaPlanGrupo() {
  const { t } = useTranslation();
  const { tenantId = '' } = useParams();
  const plan = usePlanDeUnGrupo(tenantId);
  const grupo = useGrupo(tenantId);

  if (plan.isError) return <ErrorRecuperable mensaje={mensajeDeError(plan.error, t)} onReintentar={() => void plan.refetch()} />;
  if (!plan.data || grupo.isPending) {
    return (
      <>
        <CabeceraEsqueleto />
        <DosColumnas
          izquierda={
            <>
              <EsqueletoFormulario campos={2} conTarjeta />
              <EsqueletoFormulario campos={5} columnas={2} conTarjeta />
            </>
          }
          derecha={<EsqueletoTarjeta lineas={6} />}
        />
      </>
    );
  }

  // Al guardar, el formulario se rehace con lo que devolvió el servidor.
  return <Formulario key={tenantId} tenantId={tenantId} uso={plan.data} grupo={grupo.data?.tenant.nombre ?? '—'} />;
}

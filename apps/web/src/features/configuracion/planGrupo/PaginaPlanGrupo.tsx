import { Alert, Box, Stack, Typography } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { EsqueletoTarjeta } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useUsoDelGrupo } from '../../plan/api';
import { etiquetaMes } from '../usoIa/modelo';
import { MedidoresDeUso } from '../../plan/MedidorDeUso';
import { enAlerta } from '../../plan/modelo';

/**
 * «Plan del grupo» (OD-14, OD-17): lo que permite el plan que fijó Plataforma —usuarios, empresas, vínculos activos,
 * almacenamiento y llamadas a la API del mes— y cuánto lleva usado el grupo. Dice qué pasa al llegar a un límite: con el bloqueo suave
 * activo no se pueden dar de alta más de eso (lo que ya existe sigue funcionando); si no, solo se avisa. El aviso de límite del
 * backend enlaza aquí (`/e/{empresa}/plan-grupo`).
 */
export function PaginaPlanGrupo() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const consulta = useUsoDelGrupo(empresa.id, puedeLeer);
  const uso = consulta.data;

  if (!puedeLeer) return <SinPermiso seccion={t('nav.plan-grupo')} />;

  return (
    <>
      <EncabezadoPagina titulo={t('plan.titulo')} subtitulo={t('plan.subtitulo')} />
      {consulta.isError && !uso ? (
        <ErrorRecuperable mensaje={`${t('plan.error')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />
      ) : !uso ? (
        <Stack spacing={1.5} aria-busy>
          <EsqueletoTarjeta alto={96} />
          <EsqueletoTarjeta alto={220} />
        </Stack>
      ) : (
        <Stack spacing={1.5}>
          <Tarjeta titulo={t('plan.tuPlan')}>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={{ xs: 1, sm: 4 }}>
              <Box>
                <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{t('plan.codigo')}</Typography>
                <Typography sx={{ fontWeight: 600, fontSize: 16 }}>{uso.plan.configurado ? uso.plan.codigo : t('plan.sinPlan')}</Typography>
              </Box>
              <Box sx={{ minWidth: 0 }}>
                <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{t('plan.alLlegarAlLimite')}</Typography>
                <Typography sx={{ fontSize: 14, lineHeight: '20px' }}>{uso.plan.bloqueoSuave ? t('plan.bloqueoActivo') : t('plan.bloqueoInactivo')}</Typography>
              </Box>
            </Stack>
            {!uso.plan.configurado && (
              <Alert severity="info" sx={{ mt: 1.5 }}>
                {t('plan.sinPlanAyuda')}
              </Alert>
            )}
          </Tarjeta>

          {uso.bloqueado && <Alert severity="warning">{t('plan.bloqueado')}</Alert>}
          {!uso.bloqueado && enAlerta(uso.mediciones).length > 0 && <Alert severity="info">{t('plan.alertas', { lista: uso.alertas.join('; ') })}</Alert>}

          <Tarjeta titulo={t('plan.usoDelMes', { mes: etiquetaMes(uso.mes).toLowerCase() })}>
            <MedidoresDeUso mediciones={uso.mediciones} />
            <Typography sx={{ mt: 2, fontSize: 12, color: 'text.secondary', lineHeight: '16px' }}>{t('plan.nota')}</Typography>
          </Tarjeta>
        </Stack>
      )}
    </>
  );
}

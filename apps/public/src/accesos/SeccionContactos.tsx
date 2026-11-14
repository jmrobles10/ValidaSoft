import { Alert, Box, Card, Chip, Stack, Typography } from '@mui/material';
import type { ContactoDelTerceroDto, EntradaDeHistorialDeContactoDto } from '@validasoft/api-client';
import { EsqueletoTarjetas, formatearFecha } from '@validasoft/ui';
import { ShieldAlert, ShieldCheck, ShieldQuestion } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable } from '../shared/componentes';
import { useContactosDelTercero } from './api';
import { estadoDelContacto, ordenarContactos } from './modelo';

/** Cuántas líneas del historial de los contactos se muestran (las más recientes). */
const LINEAS_DE_HISTORIAL = 8;

const ICONO = { Verificado: ShieldCheck, SinVerificar: ShieldQuestion, Desplazado: ShieldAlert } as const;

/**
 * Los contactos del tercero y su verificación (decisión AV): quién tiene acceso completo, con qué se verificó cada uno y quién puede aprobar o rechazar los accesos de otros
 * correos (solo los verificados; sin ninguno decide la plataforma), con el historial de cómo llegó cada uno. Dice qué le pasa a la sesión actual si su correo no está verificado.
 */
export function SeccionContactos() {
  const { t } = useTranslation();
  const consulta = useContactosDelTercero();

  return (
    <Box component="section" aria-labelledby="contactos-del-tercero">
      <Typography id="contactos-del-tercero" variant="h3" component="h2" sx={{ mb: 0.5 }}>
        {t('accesoAislado.contactos.titulo')}
      </Typography>
      <Typography sx={{ color: 'text.secondary', fontSize: 14, lineHeight: '20px', mb: 1.5 }}>{t('accesoAislado.contactos.texto')}</Typography>
      {consulta.isPending ? (
        <EsqueletoTarjetas cantidad={1} lineas={2} />
      ) : consulta.isError ? (
        <ErrorRecuperable error={consulta.error} onReintentar={() => void consulta.refetch()} />
      ) : (
        <Stack spacing={1.5}>
          {!consulta.data.puedesDecidir && (
            <Alert severity="info" role="status">
              {t('accesoAislado.contactos.avisoSinVerificar')}
              {consulta.data.decideLaPlataforma ? ` ${t('accesoAislado.contactos.avisoLaPlataforma')}` : ''}
            </Alert>
          )}
          {consulta.data.contactos.some((c) => c.esTuCorreo && c.desplazado) && (
            <Alert severity="warning" role="status">
              {t('accesoAislado.contactos.avisoDesplazado')}
            </Alert>
          )}
          <Stack component="ul" spacing={1} sx={{ p: 0, m: 0, listStyle: 'none' }}>
            {ordenarContactos(consulta.data.contactos).map((c) => (
              <FilaDeContacto key={c.correo} contacto={c} />
            ))}
          </Stack>
          <HistorialDeContactos entradas={consulta.data.historial} />
        </Stack>
      )}
    </Box>
  );
}

function FilaDeContacto({ contacto }: { contacto: ContactoDelTerceroDto }) {
  const { t } = useTranslation();
  const estado = estadoDelContacto(contacto);
  const Icono = ICONO[estado];
  const etiqueta = t(`accesoAislado.contactos.${estado === 'Verificado' ? 'verificado' : estado === 'Desplazado' ? 'desplazado' : 'sinVerificar'}`);
  const medio = contacto.medio ? t(`accesoAislado.contactos.medios.${contacto.medio}`, { defaultValue: contacto.medio }) : null;
  return (
    <Card component="li" sx={{ p: 1.5 }} aria-label={contacto.correo}>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'flex-start', justifyContent: 'space-between' }}>
        <Box sx={{ minWidth: 0 }}>
          <Typography sx={{ fontSize: 15, lineHeight: '22px', overflowWrap: 'anywhere' }}>
            {contacto.correo}
            {contacto.esTuCorreo ? ` · ${t('accesoAislado.contactos.tuCorreo')}` : ''}
          </Typography>
          {medio && contacto.verificadoEn && (
            <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px' }}>
              {medio} · {formatearFecha(contacto.verificadoEn)}
            </Typography>
          )}
        </Box>
        <Chip variant="outlined" size="small" icon={<Icono size={14} strokeWidth={1.75} aria-hidden />} label={etiqueta} />
      </Stack>
    </Card>
  );
}

function HistorialDeContactos({ entradas }: { entradas: readonly EntradaDeHistorialDeContactoDto[] }) {
  const { t } = useTranslation();
  if (entradas.length === 0) return null;
  return (
    <Box component="section" aria-labelledby="historial-de-contactos">
      <Typography id="historial-de-contactos" sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px', mb: 0.5 }}>
        {t('accesoAislado.contactos.historial')}
      </Typography>
      <Stack component="ul" spacing={0.25} sx={{ p: 0, m: 0, listStyle: 'none' }}>
        {entradas.slice(0, LINEAS_DE_HISTORIAL).map((e, i) => (
          <Typography key={`${e.fecha}-${String(i)}`} component="li" sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px' }}>
            {formatearFecha(e.fecha)} · {t(`accesoAislado.contactos.entradas.${e.tipo}`, { correo: e.correo, detalle: e.detalle ?? '', defaultValue: `${e.correo}: ${e.tipo}` })}
          </Typography>
        ))}
      </Stack>
    </Box>
  );
}

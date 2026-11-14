import { Box, Button, Chip, Stack, Typography } from '@mui/material';
import type { ContactoDelTerceroDto, SolicitudDeAccesoDePlataformaDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha } from '@validasoft/ui';
import { ShieldAlert, ShieldCheck, ShieldQuestion } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../shared/componentes';

const ICONO = { Verificado: ShieldCheck, SinVerificar: ShieldQuestion, Desplazado: ShieldAlert } as const;

const estadoDelContacto = (c: ContactoDelTerceroDto) => (c.verificacion === 'Verificado' ? 'Verificado' : c.desplazado ? 'Desplazado' : 'SinVerificar');

/**
 * Una solicitud de acceso a un tercero con su evidencia (decisión AV): las empresas y grupos que invitaron al correo, los contactos del tercero con su verificación, y los
 * correos del RUT y de la Cámara de comercio si el tercero los tiene. Los pendientes que decide un operador (<c>enCola</c>) se aprueban o rechazan; en cualquier solicitud pendiente
 * se puede verificar un contacto sin verificar (lo que devuelve la decisión a los contactos del tercero).
 */
export function TarjetaSolicitudDeAcceso({
  solicitud,
  onDecidir,
  onVerificar,
  verificando,
}: {
  solicitud: SolicitudDeAccesoDePlataformaDto;
  onDecidir: (aprobar: boolean) => void;
  onVerificar: (contacto: ContactoDelTerceroDto) => void;
  verificando: boolean;
}) {
  const { t } = useTranslation();
  const pendiente = solicitud.estado === 'Pendiente';
  const estado = solicitud.estado === 'Aprobada' ? 'Aprobado' : solicitud.estado === 'Rechazada' ? 'Rechazado' : 'Pendiente';
  const via = solicitud.decididoVia === 'Plataforma' ? 'Plataforma' : solicitud.decididoVia === 'Verificacion' ? 'Verificacion' : 'Contacto';
  return (
    <Box component="li" aria-label={solicitud.correo}>
      <Tarjeta
        titulo={solicitud.correo}
        nivel="h3"
        acciones={<ChipEstado estado={estado} etiqueta={t(`plataforma.accesos.estados.${solicitud.estado === 'Aprobada' ? 'Aprobada' : solicitud.estado === 'Rechazada' ? 'Rechazada' : 'Pendiente'}`)} />}
      >
        <Stack spacing={1.25}>
          <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>
            {t('plataforma.accesos.tercero', { tercero: solicitud.tercero, identificacion: solicitud.identificacion, fecha: formatearFecha(solicitud.desde) })}
          </Typography>
          {solicitud.contactoDesplazado && <Typography sx={{ fontSize: 13 }}>{t('plataforma.accesos.desplazado')}</Typography>}

          <Box component="section" aria-label={t('plataforma.accesos.invitaron')}>
            <Typography sx={{ color: 'text.secondary', fontSize: 13, mb: 0.25 }}>{t('plataforma.accesos.invitaron')}</Typography>
            {solicitud.empresas.map((e) => (
              <Typography key={`${e.tenantId}-${e.fecha}-${e.empresa}`} sx={{ fontSize: 14 }}>
                {t('plataforma.accesos.empresa', { empresa: e.empresa, grupo: e.grupo, fecha: formatearFecha(e.fecha) })}
              </Typography>
            ))}
          </Box>

          <Box component="section" aria-label={t('plataforma.accesos.contactos')}>
            <Typography sx={{ color: 'text.secondary', fontSize: 13, mb: 0.25 }}>{t('plataforma.accesos.contactos')}</Typography>
            {solicitud.contactos.length === 0 ? (
              <Typography sx={{ fontSize: 14 }}>{t('plataforma.accesos.sinContactos')}</Typography>
            ) : (
              <Stack component="ul" spacing={0.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
                {solicitud.contactos.map((c) => (
                  <ContactoDeLaSolicitud key={c.correo} contacto={c} puedeVerificar={pendiente} verificando={verificando} onVerificar={() => onVerificar(c)} />
                ))}
              </Stack>
            )}
          </Box>

          <Typography sx={{ fontSize: 14 }}>
            {solicitud.correosOficiales.length === 0
              ? t('plataforma.accesos.sinCorreoOficial')
              : solicitud.correosOficiales.map((o) => t('plataforma.accesos.correoOficial', { correo: o.correo, documento: o.documento, version: String(o.version) })).join(' · ')}
          </Typography>

          {!pendiente && solicitud.decididoPor && solicitud.decididoEn && (
            <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>
              {t('plataforma.accesos.decididoPor', { quien: solicitud.decididoPor, via: t(`plataforma.accesos.vias.${via}`), fecha: formatearFecha(solicitud.decididoEn) })}
              {solicitud.motivo ? ` · ${t('plataforma.accesos.motivo', { motivo: solicitud.motivo })}` : ''}
            </Typography>
          )}
          {pendiente && !solicitud.enCola && <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('plataforma.accesos.decideUnContacto')}</Typography>}
          {pendiente && solicitud.enCola && (
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
              <Button variant="contained" onClick={() => onDecidir(true)} aria-label={t('plataforma.accesos.aprobarEste', { correo: solicitud.correo })}>
                {t('plataforma.accesos.aprobar')}
              </Button>
              <Button variant="outlined" color="error" onClick={() => onDecidir(false)} aria-label={t('plataforma.accesos.rechazarEste', { correo: solicitud.correo })}>
                {t('plataforma.accesos.rechazar')}
              </Button>
            </Stack>
          )}
        </Stack>
      </Tarjeta>
    </Box>
  );
}

function ContactoDeLaSolicitud({
  contacto,
  puedeVerificar,
  verificando,
  onVerificar,
}: {
  contacto: ContactoDelTerceroDto;
  puedeVerificar: boolean;
  verificando: boolean;
  onVerificar: () => void;
}) {
  const { t } = useTranslation();
  const estado = estadoDelContacto(contacto);
  const Icono = ICONO[estado];
  const medio = contacto.medio ? t(`plataforma.accesos.medios.${contacto.medio}`, { defaultValue: contacto.medio }) : null;
  return (
    <Stack component="li" direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }} useFlexGap>
      <Typography sx={{ fontSize: 14, overflowWrap: 'anywhere' }}>{contacto.correo}</Typography>
      <Chip size="small" variant="outlined" icon={<Icono size={14} strokeWidth={1.75} aria-hidden />} label={t(`plataforma.accesos.estadosContacto.${estado}`)} />
      {medio && <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{medio}</Typography>}
      {puedeVerificar && contacto.verificacion !== 'Verificado' && (
        <Button size="small" variant="text" disabled={verificando} onClick={onVerificar} aria-label={t('plataforma.accesos.verificarEste', { correo: contacto.correo })}>
          {t('plataforma.accesos.verificar')}
        </Button>
      )}
    </Stack>
  );
}

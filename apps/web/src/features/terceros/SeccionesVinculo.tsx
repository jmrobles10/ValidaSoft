import { Divider, Stack, Typography } from '@mui/material';
import type { CertificadoDelVinculoDto, CuentaDesignadaDelVinculoDto, DocumentoVistaEmpresaDto } from '@validasoft/api-client';
import { ChipEstado } from '@validasoft/ui';
import { FileText } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { esEstadoCertificado, estadoVisibleDelCertificado } from '../certificados/api';
import { versionVisible } from '../documentos/api';
import { formatearFechaDeclarada } from '../documentos/formatoDatos';
import { ChipVigencia } from '../documentos/VigenciaDocumento';
import { DatosCuenta } from '../vinculos/cuentaDesignada/DatosCuenta';
import { esEstadoCuenta } from '../vinculos/cuentaDesignada/modelo';
import { Mono } from './DatosTercero';

/** Subsección de la tarjeta de un vínculo (sobretítulo + contenido). */
export function SeccionFicha({ titulo, chip, children }: { titulo: string; chip?: ReactNode; children: ReactNode }) {
  return (
    <Stack component="section" aria-label={titulo} spacing={0.75} sx={{ minWidth: 0 }}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
        <Typography variant="overline" component="h4" sx={{ color: 'text.secondary' }}>
          {titulo}
        </Typography>
        {chip}
      </Stack>
      {children}
    </Stack>
  );
}

/** Texto secundario de una subsección (sin permiso, vacía…). */
const Nota = ({ children }: { children: string }) => (
  <Typography variant="body2" sx={{ color: 'text.secondary' }}>
    {children}
  </Typography>
);

/** Certificado del vínculo: estado, folio y vigencia. `null` = quien consulta no tiene `certificados:leer`. */
export function CertificadoEnFicha({ certificado }: { certificado: CertificadoDelVinculoDto | null }) {
  const { t } = useTranslation();
  const actual = certificado?.actual ?? null;
  const visible = actual ? estadoVisibleDelCertificado(actual) : '';
  return (
    <SeccionFicha titulo={t('terceros.ficha.certificado.titulo')}>
      {!certificado ? (
        <Nota>{t('terceros.ficha.certificado.sinPermiso')}</Nota>
      ) : !actual ? (
        <Nota>{t('terceros.ficha.certificado.sinEmitir')}</Nota>
      ) : (
        <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          {esEstadoCertificado(visible) ? <ChipEstado estado={visible} bloqueante={actual.bloqueante} /> : <Typography variant="body2">{actual.estado}</Typography>}
          <Mono>{actual.folio}</Mono>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {actual.vigenteHasta ? t('terceros.ficha.certificado.vigenteHasta', { fecha: formatearFechaDeclarada(actual.vigenteHasta) }) : t('terceros.ficha.certificado.sinVencimiento')}
          </Typography>
        </Stack>
      )}
    </SeccionFicha>
  );
}

/** Cuenta designada en solo lectura; aprobar o rechazar un cambio se hace en el vínculo. */
export function CuentaEnFicha({ cuenta }: { cuenta: CuentaDesignadaDelVinculoDto }) {
  const { t } = useTranslation();
  const { actual, propuesta } = cuenta;
  return (
    <SeccionFicha titulo={t('terceros.ficha.cuenta.titulo')} chip={actual && esEstadoCuenta(actual.estado) ? <ChipEstado estado={actual.estado} fecha={actual.aprobadaEn} /> : undefined}>
      {actual ? <DatosCuenta cuenta={actual} /> : !propuesta && <Nota>{t('terceros.ficha.cuenta.sinCuenta')}</Nota>}
      {propuesta && <Nota>{t('terceros.ficha.cuenta.propuestaPendiente')}</Nota>}
    </SeccionFicha>
  );
}

/** Documentos que el tercero compartió con el vínculo. `null` = quien consulta no tiene `documentos:leer`. */
export function DocumentosEnFicha({ documentos }: { documentos: DocumentoVistaEmpresaDto[] | null }) {
  const { t } = useTranslation();
  return (
    <SeccionFicha titulo={t('terceros.ficha.documentos.titulo')}>
      {!documentos ? (
        <Nota>{t('terceros.ficha.documentos.sinPermiso')}</Nota>
      ) : documentos.length === 0 ? (
        <Nota>{t('terceros.ficha.documentos.vacio')}</Nota>
      ) : (
        <Stack component="ul" divider={<Divider component="li" aria-hidden />} sx={{ p: 0, m: 0, listStyle: 'none' }}>
          {documentos.map((documento) => {
            const version = versionVisible(documento);
            return (
              <Stack component="li" key={documento.documentoId} direction="row" spacing={1.25} sx={{ alignItems: 'center', py: 0.5, minHeight: 40 }}>
                <FileText size={18} strokeWidth={1.75} aria-hidden style={{ flex: 'none', opacity: 0.7 }} />
                <Stack sx={{ minWidth: 0, flex: 1 }}>
                  <Typography noWrap>{documento.tipo.nombre}</Typography>
                  <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
                    {version ? t('terceros.ficha.documentos.version', { n: String(version.n) }) : t('terceros.ficha.documentos.sinVersion')}
                  </Typography>
                </Stack>
                {version?.vigencia && <ChipVigencia vigencia={version.vigencia} />}
              </Stack>
            );
          })}
        </Stack>
      )}
    </SeccionFicha>
  );
}

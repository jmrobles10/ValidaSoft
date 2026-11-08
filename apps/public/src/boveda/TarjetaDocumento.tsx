import { Box, Button, Card, IconButton, ListItemIcon, ListItemText, Menu, MenuItem, Stack, Typography } from '@mui/material';
import type { DocumentoBovedaDto, VersionDocumentoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { Archive, Download, EllipsisVertical, FileText, Upload } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { FilaEmpresa } from '../shared/componentes';
import { requiereAtencion } from '../vigencia/vigencia';
import { AvisoVigencia, ChipVigencia, LineaVigencia } from '../vigencia/ChipVigencia';
import { DialogoRetirar } from './DialogoRetirar';
import { useDescargar } from './useDescargar';
import { detalleComparticion, sePuedeDescargar, sePuedeRetirar } from './versiones';
import { VersionesDocumento } from './VersionesDocumento';

/**
 * Un documento de la bóveda (TarjetaDocumentoBoveda del design system): la versión vigente una sola vez y una fila
 * por empresa con lo que tiene. Acciones: subir nueva versión, descargar, retirar y ver todas las versiones.
 */
export function TarjetaDocumento({ documento }: { documento: DocumentoBovedaDto }) {
  const { t } = useTranslation();
  const [menu, setMenu] = useState<HTMLElement | null>(null);
  const [retirando, setRetirando] = useState<VersionDocumentoDto | null>(null);
  const [verVersiones, setVerVersiones] = useState(false);
  const descargar = useDescargar();
  const actual = documento.versionActual;
  const titulo = documento.tipo.nombre;

  return (
    <Card component="article" aria-labelledby={`doc-${documento.id}`} sx={{ p: 2 }}>
      <Stack direction="row" spacing={1.5} alignItems="flex-start">
        <Box sx={{ color: 'text.secondary', pt: 0.25 }} aria-hidden>
          <FileText size={22} />
        </Box>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography id={`doc-${documento.id}`} component="h2" sx={{ fontWeight: 600, fontSize: 16, lineHeight: '22px' }}>
            {titulo}
          </Typography>
          <Typography color="text.secondary" sx={{ fontSize: 14 }}>
            {actual ? t('boveda.cargadaEl', { n: String(actual.n), fecha: formatearFecha(actual.cargadaEn) }) : t('boveda.sinVigente')}
          </Typography>
          {/* La vigencia más próxima entre las empresas; la de cada una va en su fila. */}
          <LineaVigencia vigencia={actual?.vigencia} />
        </Box>
        {actual && (
          <IconButton aria-label={`${t('comun.version', { n: String(actual.n) })}: ${titulo}`} aria-haspopup="menu" onClick={(e) => setMenu(e.currentTarget)} sx={{ width: 44, height: 44 }}>
            <EllipsisVertical size={20} />
          </IconButton>
        )}
      </Stack>

      {actual && (
        <Menu anchorEl={menu} open={Boolean(menu)} onClose={() => setMenu(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }} transformOrigin={{ vertical: 'top', horizontal: 'right' }}>
          {sePuedeDescargar(actual) && (
            <MenuItem
              onClick={() => {
                setMenu(null);
                descargar(documento.id, actual.n);
              }}
            >
              <ListItemIcon>
                <Download size={18} aria-hidden />
              </ListItemIcon>
              <ListItemText primary={t('boveda.descargar')} />
            </MenuItem>
          )}
          {sePuedeRetirar(actual) && (
            <MenuItem
              onClick={() => {
                setMenu(null);
                setRetirando(actual);
              }}
            >
              <ListItemIcon>
                <Archive size={18} aria-hidden />
              </ListItemIcon>
              <ListItemText primary={t('boveda.retirar')} />
            </MenuItem>
          )}
        </Menu>
      )}

      <Box component="ul" aria-label={t('boveda.empresas')} sx={{ p: 0, m: 0, mt: 1.5 }}>
        {documento.porEmpresa.length === 0 ? (
          <Typography component="li" color="text.secondary" sx={{ fontSize: 14, py: 1.25, borderTop: 1, borderColor: 'divider', listStyle: 'none' }}>
            {t('boveda.sinEmpresas')}
          </Typography>
        ) : (
          documento.porEmpresa.map((c) => (
            <FilaEmpresa
              key={c.vinculoId}
              nombre={c.empresa.nombre}
              estado={
                c.compartido && c.version !== null ? (
                  <>
                    {requiereAtencion(c.vigencia) && c.vigencia && <ChipVigencia vigencia={c.vigencia} />}
                    <Typography component="span" variant="mono">{`v${String(c.version)}`}</Typography>
                  </>
                ) : undefined
              }
              detalle={detalleComparticion(c, t)}
            />
          ))
        )}
      </Box>

      <AvisoVigencia vigencia={actual?.vigencia} />

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 1.5 }}>
        {/* Un formulario se actualiza desde la revisión de la empresa, que sabe qué campos pide. La API marca la clase en
            la versión (el documento llega como `Archivo`). */}
        {documento.clase !== 'Formulario' && actual?.clase !== 'Formulario' && (
          <Button component={RouterLink} to={`/boveda/subir?documento=${documento.id}&tipo=${encodeURIComponent(documento.tipo.codigo)}`} variant="outlined" startIcon={<Upload size={18} aria-hidden />}>
            {t('boveda.nuevaVersion')}
          </Button>
        )}
        {documento.versiones > 1 && (
          <Button variant="text" onClick={() => setVerVersiones((v) => !v)} aria-expanded={verVersiones}>
            {verVersiones ? t('boveda.ocultarVersiones') : t('boveda.versiones', { n: String(documento.versiones) })}
          </Button>
        )}
      </Stack>

      {verVersiones && <VersionesDocumento documentoId={documento.id} onRetirar={setRetirando} />}
      {retirando && <DialogoRetirar documentoId={documento.id} tipo={titulo} version={retirando} onCerrar={() => setRetirando(null)} />}
    </Card>
  );
}

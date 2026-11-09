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
import { detalleComparticion, enlaceSubirVersion, sePuedeDescargar, sePuedeRetirar, sePuedeSubirVersion } from './versiones';
import { VersionesDocumento } from './VersionesDocumento';

/**
 * Un documento de la bóveda (TarjetaDocumentoBoveda del design system): cabecera compacta con la versión vigente y una
 * sola acción «Subir versión» (la versión es una para todas las empresas), y debajo una fila por empresa con lo que
 * tiene. Descargar y retirar van en el menú «…»; ver todas las versiones, al pie.
 */
export function TarjetaDocumento({ documento }: { documento: DocumentoBovedaDto }) {
  const { t } = useTranslation();
  const [menu, setMenu] = useState<HTMLElement | null>(null);
  const [retirando, setRetirando] = useState<VersionDocumentoDto | null>(null);
  const [verVersiones, setVerVersiones] = useState(false);
  const descargar = useDescargar();
  const actual = documento.versionActual;
  const titulo = documento.tipo.nombre;
  const subible = sePuedeSubirVersion(documento);
  const pie = documento.versiones > 1 || requiereAtencion(actual?.vigencia);

  return (
    <Card component="article" aria-labelledby={`doc-${documento.id}`} sx={{ p: 0, overflow: 'hidden' }}>
      {/* Si el título no cabe junto a las acciones (móvil, nombres largos), las acciones bajan a su propia línea. */}
      <Stack direction="row" alignItems="center" useFlexGap flexWrap="wrap" sx={{ columnGap: 1.25, rowGap: 0.25, py: 1, pl: { xs: 1.5, sm: 2 }, pr: 1 }}>
        <Box aria-hidden sx={(th) => ({ display: 'grid', placeItems: 'center', flex: 'none', width: 28, height: 28, borderRadius: `${String(th.radio.sm)}px`, bgcolor: 'superficie.nivel2', color: 'text.secondary' })}>
          <FileText size={18} />
        </Box>
        <Box sx={{ minWidth: 0, flex: '1 1 180px' }}>
          <Typography id={`doc-${documento.id}`} component="h2" sx={{ fontWeight: 600, fontSize: 15, lineHeight: '20px' }}>
            {titulo}
          </Typography>
          <Typography color="text.secondary" sx={{ fontSize: 12, lineHeight: '16px' }}>
            {actual ? t('boveda.cargadaEl', { n: String(actual.n), fecha: formatearFecha(actual.cargadaEn) }) : t('boveda.sinVigente')}
          </Typography>
          {/* La vigencia más próxima entre las empresas; la de cada una va en su fila. */}
          <LineaVigencia vigencia={actual?.vigencia} />
        </Box>
        <Stack direction="row" alignItems="center" sx={{ ml: 'auto', flex: 'none' }}>
          {subible && (
            <Button
              component={RouterLink}
              to={enlaceSubirVersion(documento)}
              variant="text"
              startIcon={<Upload size={16} aria-hidden />}
              sx={{ minHeight: 44, '@media (min-width:600px)': { minHeight: 36 } }}
            >
              {t('boveda.nuevaVersion')}
            </Button>
          )}
          {actual && (
            <IconButton aria-label={`${t('comun.version', { n: String(actual.n) })}: ${titulo}`} aria-haspopup="menu" onClick={(e) => setMenu(e.currentTarget)} sx={{ width: { xs: 44, sm: 36 }, height: { xs: 44, sm: 36 } }}>
              <EllipsisVertical size={18} />
            </IconButton>
          )}
        </Stack>
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

      <Box component="ul" aria-label={t('boveda.empresas')} sx={{ p: 0, m: 0 }}>
        {documento.porEmpresa.length === 0 ? (
          <Typography component="li" color="text.secondary" sx={{ fontSize: 13, py: 1, px: { xs: 1.5, sm: 2 }, borderTop: 1, borderColor: 'divider', listStyle: 'none' }}>
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

      {pie && (
        <Box sx={{ px: { xs: 1.5, sm: 2 }, py: 0.75, borderTop: 1, borderColor: 'divider' }}>
          <AvisoVigencia vigencia={actual?.vigencia} />
          {documento.versiones > 1 && (
            <Button variant="text" size="small" onClick={() => setVerVersiones((v) => !v)} aria-expanded={verVersiones} sx={{ ml: -1 }}>
              {verVersiones ? t('boveda.ocultarVersiones') : t('boveda.versiones', { n: String(documento.versiones) })}
            </Button>
          )}
          {verVersiones && <VersionesDocumento documentoId={documento.id} onRetirar={setRetirando} />}
        </Box>
      )}
      {retirando && <DialogoRetirar documentoId={documento.id} tipo={titulo} version={retirando} onCerrar={() => setRetirando(null)} />}
    </Card>
  );
}

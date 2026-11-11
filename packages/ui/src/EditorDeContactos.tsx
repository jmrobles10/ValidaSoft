import { Box, Button, Card, IconButton, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { Plus, Trash2 } from 'lucide-react';

/** Un contacto como se edita: todo texto (lo vacío es «sin dato»). */
export interface ContactoEditable {
  nombre: string;
  cargo: string;
  correo: string;
  telefono: string;
  rol: string;
}

export type ErroresDeContacto = Partial<Record<keyof ContactoEditable, string>>;

/** Los cuatro roles del diseño (IO-3): para qué se le escribe a esa persona. */
export const ROLES_DE_CONTACTO = ['Comercial', 'Pagos', 'Cumplimiento', 'Notificaciones'] as const;

export const MAXIMO_DE_CONTACTOS = 20;

export const contactoVacio = (rol: string = ROLES_DE_CONTACTO[0]): ContactoEditable => ({ nombre: '', cargo: '', correo: '', telefono: '', rol });

/** De lo que devuelve la API (campos opcionales nulos) a lo que se edita. */
export function aEditables(contactos: readonly { nombre: string; cargo?: string | null; correo: string; telefono?: string | null; rol: string }[] | null | undefined): ContactoEditable[] {
  return (contactos ?? []).map((c) => ({ nombre: c.nombre, cargo: c.cargo ?? '', correo: c.correo, telefono: c.telefono ?? '', rol: c.rol }));
}

/** Lo que se envía: sin espacios sobrantes, lo vacío como `null` y sin las filas que quedaron totalmente vacías. */
export function desdeEditables(contactos: readonly ContactoEditable[]): { nombre: string; cargo: string | null; correo: string; telefono: string | null; rol: string }[] {
  return contactos
    .filter((c) => [c.nombre, c.cargo, c.correo, c.telefono].some((x) => x.trim() !== ''))
    .map((c) => ({ nombre: c.nombre.trim(), cargo: c.cargo.trim() || null, correo: c.correo.trim(), telefono: c.telefono.trim() || null, rol: c.rol }));
}

const CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Las mismas reglas del dominio (`Vinculo.Normalizar`): nombre y correo válidos y el mismo correo no se repite con el mismo rol. */
export function validarContactos(contactos: readonly ContactoEditable[], mensajes: { nombre: string; correo: string; repetido: string }): Record<number, ErroresDeContacto> {
  const errores: Record<number, ErroresDeContacto> = {};
  const vistos = new Set<string>();
  contactos.forEach((c, i) => {
    if ([c.nombre, c.cargo, c.correo, c.telefono].every((x) => x.trim() === '')) return;
    const e: ErroresDeContacto = {};
    if (!c.nombre.trim()) e.nombre = mensajes.nombre;
    const correo = c.correo.trim().toLowerCase();
    if (!CORREO.test(correo)) e.correo = mensajes.correo;
    else if (vistos.has(`${correo}|${c.rol}`)) e.correo = mensajes.repetido;
    else vistos.add(`${correo}|${c.rol}`);
    if (Object.keys(e).length > 0) errores[i] = e;
  });
  return errores;
}

export interface EtiquetasEditorDeContactos {
  nombre: string;
  cargo: string;
  correo: string;
  telefono: string;
  rol: string;
  agregar: string;
  /** «Quitar contacto 2» (para el lector de pantalla). */
  quitar: (numero: number) => string;
  /** Texto de la fila vacía cuando aún no hay contactos. */
  vacio: string;
  /** Nombre de cada contacto en la lista (para el lector de pantalla). */
  contacto: (numero: number) => string;
}

export interface EditorDeContactosProps {
  contactos: readonly ContactoEditable[];
  onCambiar: (siguiente: ContactoEditable[]) => void;
  roles: readonly { valor: string; etiqueta: string }[];
  etiquetas: EtiquetasEditorDeContactos;
  errores?: Record<number, ErroresDeContacto>;
  deshabilitado?: boolean;
  maximo?: number;
}

/**
 * Lista editable de contactos del tercero con una empresa (IO-3): nombre, cargo, correo, teléfono y rol. Campos cortos en columnas,
 * una tarjeta por contacto y «Agregar contacto» al final. Compartido por la web de la empresa y la app del tercero; no sabe de la API.
 */
export function EditorDeContactos({ contactos, onCambiar, roles, etiquetas, errores = {}, deshabilitado = false, maximo = MAXIMO_DE_CONTACTOS }: EditorDeContactosProps) {
  const cambiar = (indice: number, campo: keyof ContactoEditable, valor: string) =>
    onCambiar(contactos.map((c, i) => (i === indice ? { ...c, [campo]: valor } : c)));
  const quitar = (indice: number) => onCambiar(contactos.filter((_, i) => i !== indice));

  return (
    <Stack spacing={1.5}>
      {contactos.length === 0 && <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>{etiquetas.vacio}</Typography>}
      {contactos.map((c, i) => {
        const e = errores[i] ?? {};
        return (
          <Card key={i} component="fieldset" sx={{ p: 1.5, m: 0, minWidth: 0 }} aria-label={etiquetas.contacto(i + 1)}>
            <Stack spacing={1.5}>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start' }}>
                <TextField
                  label={etiquetas.nombre}
                  value={c.nombre}
                  onChange={(ev) => cambiar(i, 'nombre', ev.target.value)}
                  error={Boolean(e.nombre)}
                  helperText={e.nombre}
                  disabled={deshabilitado}
                  required
                  fullWidth
                  slotProps={{ htmlInput: { maxLength: 200, autoComplete: 'off' } }}
                />
                <IconButton aria-label={etiquetas.quitar(i + 1)} onClick={() => quitar(i)} disabled={deshabilitado} sx={{ mt: 0.5 }}>
                  <Trash2 size={18} aria-hidden />
                </IconButton>
              </Stack>
              <Box sx={{ display: 'grid', gap: 1.5, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' } }}>
                <TextField label={etiquetas.cargo} value={c.cargo} onChange={(ev) => cambiar(i, 'cargo', ev.target.value)} disabled={deshabilitado} slotProps={{ htmlInput: { maxLength: 120, autoComplete: 'off' } }} />
                <TextField select label={etiquetas.rol} value={c.rol} onChange={(ev) => cambiar(i, 'rol', ev.target.value)} disabled={deshabilitado} required>
                  {roles.map((r) => (
                    <MenuItem key={r.valor} value={r.valor}>
                      {r.etiqueta}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  label={etiquetas.correo}
                  type="email"
                  value={c.correo}
                  onChange={(ev) => cambiar(i, 'correo', ev.target.value)}
                  error={Boolean(e.correo)}
                  helperText={e.correo}
                  disabled={deshabilitado}
                  required
                  slotProps={{ htmlInput: { maxLength: 200, autoComplete: 'off' } }}
                />
                <TextField label={etiquetas.telefono} type="tel" value={c.telefono} onChange={(ev) => cambiar(i, 'telefono', ev.target.value)} disabled={deshabilitado} slotProps={{ htmlInput: { maxLength: 40, autoComplete: 'off' } }} />
              </Box>
            </Stack>
          </Card>
        );
      })}
      <Box>
        <Button variant="outlined" size="small" startIcon={<Plus size={16} aria-hidden />} onClick={() => onCambiar([...contactos, contactoVacio(roles[0]?.valor)])} disabled={deshabilitado || contactos.length >= maximo}>
          {etiquetas.agregar}
        </Button>
      </Box>
    </Stack>
  );
}

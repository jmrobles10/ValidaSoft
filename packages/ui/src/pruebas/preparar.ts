import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { afterEach } from 'vitest';

// `findBy*`/`waitFor` esperan 5 s (1 s por omisión): con los tres proyectos en paralelo el primer render de MUI tarda más.
configure({ asyncUtilTimeout: 5_000 });

afterEach(() => {
  cleanup();
});

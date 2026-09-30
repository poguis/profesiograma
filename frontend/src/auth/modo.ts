/**
 * true solo con `npm run dev` y VITE_AUTH_MODE=dev. En `vite build` import.meta.env.DEV es false:
 * las ramas que dependen de esta constante (y los módulos que cargan con import dinámico) no llegan al bundle.
 * Este archivo no debe importar devAuth.ts (así el código de DevAuth queda fuera de producción).
 * En los import() dinámicos de DevAuth (auth/index.ts, DisenoPrincipal.tsx) se repite la expresión literal:
 * con la constante importada el empaquetador genera igualmente los chunks, aunque nunca se carguen.
 */
export const devAuthActivo: boolean = import.meta.env.DEV && import.meta.env.VITE_AUTH_MODE === 'dev'

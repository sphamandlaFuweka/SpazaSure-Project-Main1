/**
 * The supplier portal and the admin console are built from this codebase as two separate apps.
 * Each build contains only its own pages, so a supplier never receives admin code or screens.
 * `__APP_TARGET__` is replaced at build time (see vite.config.ts).
 */
export type AppTarget = 'supplier' | 'admin';

export const APP_TARGET: AppTarget = __APP_TARGET__;
export const IS_ADMIN_APP = __APP_TARGET__ === 'admin';

/** Separate storage keys keep the two sessions apart even if both are served from one origin. */
export const AUTH_STORAGE_KEY = IS_ADMIN_APP ? 'spazasure-admin-auth-v1' : 'spazasure-auth-v2';

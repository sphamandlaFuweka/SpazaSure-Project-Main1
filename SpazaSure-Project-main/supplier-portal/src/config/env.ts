/**
 * Environment Configuration
 * Centralized access to environment variables
 */

export const env = {
  // API Configuration
  apiUrl: import.meta.env.VITE_API_URL || 'http://localhost:5181/api',
  apiTimeout: Number(import.meta.env.VITE_API_TIMEOUT) || 10000,

  // App Configuration
  appName: import.meta.env.VITE_APP_NAME || 'SpazaSure Supplier Portal',
  appEnv: import.meta.env.VITE_APP_ENV || 'development',

  // Feature Flags
  enableDebug: import.meta.env.VITE_ENABLE_DEBUG === 'true',

  // Environment checks
  isDevelopment: import.meta.env.MODE === 'development',
  isQA: import.meta.env.MODE === 'qa',
  isProduction: import.meta.env.MODE === 'production',
} as const;

// Log configuration in non-production environments
if (env.enableDebug) {
  console.log('🔧 Environment Configuration:', {
    mode: import.meta.env.MODE,
    apiUrl: env.apiUrl,
    appEnv: env.appEnv,
  });
}

export default env;

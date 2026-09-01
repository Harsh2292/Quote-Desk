/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_GOOGLE_CLIENT_ID: string
  /** Optional — unset (and therefore same-origin) in dev, where the Vite proxy makes the Api
   * same-origin already. Set in production (task 09b) to the Container App's own origin, since
   * Static Web Apps and the Api are two separate hosts there. */
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

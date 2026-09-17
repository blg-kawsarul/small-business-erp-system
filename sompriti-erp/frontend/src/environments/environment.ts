/**
 * Browser build: the API is served from the same origin, so requests use relative URLs.
 */
export const environment = {
  production: true,
  /** Empty means "same server as the web app". */
  apiBaseUrl: '',
};

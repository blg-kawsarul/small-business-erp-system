/**
 * Application identity and developer credit.
 *
 * Every place that shows the product name, the version or the author reads from here,
 * so the credit stays identical on desktop, on phones and inside the Android app.
 */
export const APP_INFO = {
  name: 'Enterprise Resource Planning',
  version: '1.0.0',
  /** Year the product was first released; the copyright line grows from it. */
  since: 2026,
  developer: {
    name: 'Md Kawsarul Islam',
    title: 'Software Engineer',
    company: 'BMW Group',
    location: 'Malaysia',
  },
} as const;

/** "Software Engineer, BMW Group, Malaysia" */
export const DEVELOPER_ROLE =
  `${APP_INFO.developer.title}, ${APP_INFO.developer.company}, ${APP_INFO.developer.location}`;

/** "Developed by Md Kawsarul Islam" - the short form used in footers. */
export const DEVELOPED_BY = `Developed by ${APP_INFO.developer.name}`;

/** "© 2026 Md Kawsarul Islam. All rights reserved." (a range once the year moves on). */
export function copyrightLine(now: Date = new Date()): string {
  const year = now.getFullYear();
  const span = year > APP_INFO.since ? `${APP_INFO.since}-${year}` : `${APP_INFO.since}`;
  return `© ${span} ${APP_INFO.developer.name}. All rights reserved.`;
}

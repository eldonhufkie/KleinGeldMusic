import 'dotenv/config';

/** Settings shared by the config, the auth provider and the helpers. Defaults match playwright.config.ts. */
export const environment = {
  baseUrl: process.env.BASE_URL ?? 'https://localhost:7201',
  mailPitUrl: `http://localhost:${process.env.MAILPIT_PORT ?? '8025'}`,
  admin: {
    email: process.env.E2E_ADMIN_EMAIL ?? 'admin@kleingeld.test',
    password: process.env.E2E_ADMIN_PASSWORD ?? 'Admin-Pa55word!',
  },
  /** Every Member the tests create gets this password; they only ever exist in a throwaway database. */
  memberPassword: 'New-Member-Pa55word!',
};

/** ASP.NET Core Identity's application cookie: the only credential the app uses (no bearer tokens). */
export const identityCookieName = '.AspNetCore.Identity.Application';

/** The Cookie header that makes an apiRequest act as the Member whose session cookie this is. */
export const asMember = (authToken: string): Record<string, string> => ({
  Cookie: `${identityCookieName}=${authToken}`,
});

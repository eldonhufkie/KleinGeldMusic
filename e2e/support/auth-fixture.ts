import { test as base, type Fixtures, type PlaywrightTestArgs, type PlaywrightTestOptions } from '@playwright/test';
import {
  type AuthFixtures,
  createAuthFixtures,
  getAuthProvider,
  getAuthToken,
  setAuthProvider,
} from '@seontechnologies/playwright-utils/auth-session';
import kleingeldAuthProvider, { ADMIN, MEMBER } from './auth/kleingeld-auth-provider';

// Registered before createAuthFixtures reads it.
setAuthProvider(kleingeldAuthProvider);

const defaults = createAuthFixtures();
// playwright-utils defaults to a user called 'default'; global-setup creates 'member', so start from that one.
defaults.authOptions = [{ environment: process.env.TEST_ENV ?? 'local', userIdentifier: MEMBER }, { option: true }];

// createAuthFixtures() is typed as a plain object; tell extend() which fixtures it adds.
const authFixtures = defaults as unknown as Fixtures<AuthFixtures, object, PlaywrightTestArgs & PlaywrightTestOptions>;

/**
 * `page`, `context` and `authToken` act as the Member named by `authOptions.userIdentifier`
 * (default `member`). Use `test.use({ authOptions: { userIdentifier: ADMIN } })` for the Admin and
 * `test.use({ authSessionEnabled: false })` for an outsider.
 *
 * `adminToken` is the Admin's session cookie alongside whoever the test is, for setup the Admin must do
 * (approving a Release) while the test itself acts as a Member.
 */
export const test = base.extend<AuthFixtures>(authFixtures).extend<{ adminToken: string }>({
  adminToken: async ({ request }, use) => {
    const state = await getAuthToken(request, { userIdentifier: ADMIN });
    await use(getAuthProvider().extractToken(state) ?? '');
  },
});

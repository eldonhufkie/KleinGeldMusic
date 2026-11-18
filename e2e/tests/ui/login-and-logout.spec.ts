import { environment } from '../../support/environment';
import { expect, log, test } from '../../support/merged-fixtures';

/** Story 13 (log in) and story 98 (Queue cleared on log out). Test design R-10. */
test.describe('Logging in', () => {
  test.use({ authSessionEnabled: false });

  test('[P0] an outsider opening a Member page is sent to Log in', async ({ page }) => {
    // Given nobody is logged in
    // When they open the Playlists page
    await page.goto('/playlists');

    // Then they land on the Log in page
    await expect(page).toHaveURL(/\/Account\/Login/);
    await expect(page.getByRole('button', { name: 'Log in', exact: true })).toBeVisible();
  });

  test('[P1] a Member logs in with email and password and reaches the app', async ({ page }) => {
    // Given the Log in page
    await page.goto('/Account/Login');

    // When the Admin (a Member too) logs in
    await log.step('Fill in the Log in form');
    await page.getByLabel('Email').fill(environment.admin.email);
    await page.getByLabel('Password').fill(environment.admin.password);
    await page.getByRole('button', { name: 'Log in', exact: true }).click();

    // Then the app shows its navigation and who is logged in
    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
    await expect(page.getByRole('group').getByText(environment.admin.email)).toBeVisible();
  });
});

test.describe('Logging out', () => {
  test('[P1] log out ends the session and clears the saved Queue', async ({ page }) => {
    // Given a logged-in Member whose browser holds a saved Queue
    await page.goto('/');
    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
    await page.evaluate(() => localStorage.setItem('kleingeld.player', '{"queue":[]}'));

    // When they log out from the profile menu
    await log.step('Log out');
    // The profile menu is a <details> (role group); a hidden copy lives in the phone tab bar.
    await page.getByRole('group').locator('summary').click();
    await page.getByRole('button', { name: 'Log out' }).click();

    // Then the saved Queue is gone and Member pages ask for a login again
    await expect(page.getByRole('navigation', { name: 'Main' })).toBeHidden();
    expect(await page.evaluate(() => localStorage.getItem('kleingeld.player'))).toBeNull();
    await page.goto('/playlists');
    await expect(page).toHaveURL(/\/Account\/Login/);
  });
});

import { createPublishedRelease } from '../../support/factories/release-factory';
import { expect, test } from '../../support/merged-fixtures';

/**
 * Story 71: on a phone, a bottom tab bar replaces the sidebar and the mini player sits above it.
 * Runs in the `phone` project only (Pixel 7, below the 700 px breakpoint).
 */
test.describe('Phone layout @phone', () => {
  test('[P2] shows the tab bar instead of the sidebar', async ({ page }) => {
    await page.goto('/');

    const tabs = page.getByRole('navigation', { name: 'Tabs' });
    await expect(tabs).toBeVisible();
    for (const tab of ['Home', 'Search', 'Releases', 'Playlists']) {
      await expect(tabs.getByRole('link', { name: tab, exact: true })).toBeVisible();
    }
    await expect(tabs.getByText('More')).toBeVisible();
    await expect(page.getByRole('navigation', { name: 'Main' })).toBeHidden();
  });

  test('[P2] the mini player sits above the tab bar while a Track plays', async ({
    page,
    apiRequest,
    request,
    authToken,
    adminToken,
  }) => {
    // Given a Track playing on a phone
    const { release, tracks } = await createPublishedRelease(apiRequest, request, {
      manager: authToken,
      admin: adminToken,
    });
    await page.goto(`/releases/${release.id}/${release.slug}`);
    await page.getByRole('button', { name: `Play ${tracks[0].title}` }).click();

    // Then the mini player is shown and ends where the tab bar begins
    const player = page.getByRole('region', { name: 'Player' });
    await expect(player.getByRole('button', { name: 'Pause' })).toBeVisible();
    const playerBox = (await player.boundingBox())!;
    const tabsBox = (await page.getByRole('navigation', { name: 'Tabs' }).boundingBox())!;
    expect(playerBox.y + playerBox.height).toBeLessThanOrEqual(tabsBox.y + 1);
  });
});

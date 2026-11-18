import { createPublishedRelease } from '../../support/factories/release-factory';
import { expect, log, test } from '../../support/merged-fixtures';

/**
 * Story 86: music keeps playing while the Member moves between pages. Test design R-10 (UI regressions).
 * Only a real browser shows this: the one <audio> element must survive Blazor navigation.
 */
test.describe('Player', () => {
  test('[P1] keeps playing a Track while the Member moves to other pages', async ({
    page,
    apiRequest,
    request,
    authToken,
    adminToken,
    interceptNetworkCall,
    recurse,
  }) => {
    // Given a Published Release with one Track
    await log.step('Publish a Release with a 60-second Track');
    const { release, tracks } = await createPublishedRelease(apiRequest, request, {
      manager: authToken,
      admin: adminToken,
    });

    // When the Member opens it and plays the Track
    const releaseCall = interceptNetworkCall({ url: `**/api/releases/${release.id}/**` });
    await page.goto(`/releases/${release.id}/${release.slug}`);
    await releaseCall;

    const playLink = interceptNetworkCall({ url: `**/api/tracks/${tracks[0].id}/play-link` });
    await page.getByRole('button', { name: `Play ${tracks[0].title}` }).click();
    expect((await playLink).status).toBe(200);

    const player = page.getByRole('region', { name: 'Player' });
    await expect(player.getByRole('button', { name: 'Pause' })).toBeVisible();

    // And moves to Artists, then Releases
    await log.step('Navigate away from the Release page');
    await page.getByRole('link', { name: 'Artists' }).first().click();
    await expect(page).toHaveURL(/\/artists$/);
    await page.getByRole('link', { name: 'Releases' }).first().click();
    await expect(page).toHaveURL(/\/releases$/);

    // Then the same Track is still playing and its position has moved on
    await expect(player.getByRole('button', { name: 'Pause' })).toBeVisible();
    await expect(player.getByText(tracks[0].title).first()).toBeVisible();
    await recurse(
      () => page.locator('audio').evaluate((audio: HTMLAudioElement) => audio.currentTime),
      (seconds) => seconds > 1,
      { timeout: 10_000, interval: 250, error: 'The audio position did not advance after navigation' },
    );
  });

  test('[P2] gives the lock screen the playing Track and its Artist', async ({
    page,
    apiRequest,
    request,
    authToken,
    adminToken,
  }) => {
    // Given a Published Release, played from its page
    const { artist, release, tracks } = await createPublishedRelease(apiRequest, request, {
      manager: authToken,
      admin: adminToken,
    });
    await page.goto(`/releases/${release.id}/${release.slug}`);
    await page.getByRole('button', { name: `Play ${tracks[0].title}` }).click();
    await expect(page.getByRole('region', { name: 'Player' }).getByRole('button', { name: 'Pause' })).toBeVisible();

    // Then Media Session carries its title, Artist and cover (story 96)
    const metadata = await page.evaluate(() => {
      const m = navigator.mediaSession.metadata;
      return m && { title: m.title, artist: m.artist, artwork: m.artwork.length };
    });
    expect(metadata).toEqual({ title: tracks[0].title, artist: artist.name, artwork: 2 });
    expect(await page.evaluate(() => navigator.mediaSession.playbackState)).toBe('playing');
  });
});

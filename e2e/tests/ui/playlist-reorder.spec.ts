import { createPlaylist, readPlaylist } from '../../support/factories/playlist-factory';
import { createPublishedRelease } from '../../support/factories/release-factory';
import { expect, log, test } from '../../support/merged-fixtures';
import type { Page } from '@playwright/test';

/**
 * Stories 105 (drag handle, mouse and touch, saved at once) and 106 (remove with Undo). Test design R-10:
 * the reorder lives in track-reorder.js, so only a browser can show it works.
 *
 * All three Tracks share one audio fixture and so one title; order is checked by entry id, never by title.
 */
const rowsOn = (page: Page) => page.locator('ol.release-tracks li[data-track-id]');

/** Retries until the rows show exactly these entry ids, in this order. */
async function expectOrder(page: Page, entryIds: string[]) {
  await expect(rowsOn(page)).toHaveCount(entryIds.length);
  for (const [i, id] of entryIds.entries()) {
    await expect(rowsOn(page).nth(i)).toHaveAttribute('data-track-id', id);
  }
}

test.describe('Playlist editing', () => {
  let playlistId: string;
  let entryIds: string[];
  let trackIds: (string | null)[];

  test.beforeEach(async ({ page, apiRequest, request, authToken, adminToken, interceptNetworkCall }) => {
    await log.step('Publish a Release with three Tracks and put them on a Playlist');
    const { tracks } = await createPublishedRelease(
      apiRequest,
      request,
      { manager: authToken, admin: adminToken },
      { tracks: ['tone-60s.mp3', 'tone-60s.mp3', 'tone-60s.mp3'] },
    );
    const playlist = await createPlaylist(
      apiRequest,
      authToken,
      tracks.map((t) => t.id),
    );
    playlistId = playlist.id;
    entryIds = playlist.tracks.map((t) => t.entryId);
    trackIds = playlist.tracks.map((t) => t.trackId);

    // The rows render before Blazor imports track-reorder.js (fingerprinted) and attaches its listeners, so wait for it.
    const reorderScript = interceptNetworkCall({ url: '**/track-reorder*.js' });
    await page.goto(`/playlists/${playlist.id}/${playlist.slug}`);
    await expectOrder(page, entryIds);
    await reorderScript;
  });

  test('[P1] dragging a Track by its handle moves it and the new order is saved', async ({
    page,
    apiRequest,
    authToken,
    interceptNetworkCall,
  }) => {
    // When the Member drags the third Track's handle onto the first row
    await log.step('Drag the third Track to the top');
    const saved = interceptNetworkCall({ method: 'PUT', url: `**/api/playlists/${playlistId}/entries/*/position` });
    const handle = page.getByRole('button', { name: /^Move .*, or use the arrow keys$/ }).nth(2);
    const from = (await handle.boundingBox())!;
    const to = (await rowsOn(page).first().boundingBox())!;
    await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2);
    await page.mouse.down();
    await page.mouse.move(to.x + to.width / 2, to.y + 2, { steps: 10 });
    await page.mouse.up();
    expect((await saved).status).toBeLessThan(300);

    // Then the list shows it first, and so does the stored Playlist
    const expected = [entryIds[2], entryIds[0], entryIds[1]];
    await expectOrder(page, expected);
    const stored = await readPlaylist(apiRequest, authToken, playlistId);
    expect(stored.tracks.map((t) => t.entryId)).toEqual(expected);

    // And a reload keeps it
    await page.reload();
    await expectOrder(page, expected);
  });

  test('[P2] the arrow keys on a handle move a Track too', async ({ page, interceptNetworkCall }) => {
    // When the Member focuses the second handle and presses Arrow Up
    const saved = interceptNetworkCall({ method: 'PUT', url: `**/api/playlists/${playlistId}/entries/*/position` });
    await page.getByRole('button', { name: /^Move .*, or use the arrow keys$/ }).nth(1).press('ArrowUp');
    await saved;

    // Then the first two Tracks have swapped
    await expectOrder(page, [entryIds[1], entryIds[0], entryIds[2]]);
  });

  test('[P1] taking a Track off shows Undo, and Undo puts it back where it was', async ({
    page,
    apiRequest,
    authToken,
    interceptNetworkCall,
  }) => {
    // When the Member takes the middle Track off
    await rowsOn(page).nth(1).getByRole('button', { name: 'Take off Playlist' }).click();

    // Then it is gone and an Undo offer appears
    await expectOrder(page, [entryIds[0], entryIds[2]]);
    const notice = page.getByRole('status').filter({ hasText: 'off the Playlist' });
    await expect(notice).toBeVisible();

    // When they press Undo
    const restored = interceptNetworkCall({ method: 'POST', url: `**/api/playlists/${playlistId}/entries/restore` });
    await notice.getByRole('button', { name: 'Undo' }).click();
    expect((await restored).status).toBeLessThan(300);

    // Then the same Track is back in the middle (Undo may give it a new entry id, so compare Tracks)
    await expect(rowsOn(page)).toHaveCount(3);
    const stored = await readPlaylist(apiRequest, authToken, playlistId);
    expect(stored.tracks.map((t) => t.trackId)).toEqual(trackIds);
  });
});

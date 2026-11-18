import { releaseFormFactory } from '../../support/factories/release-factory';
import { asMember, expect, log, MEMBER, test } from '../../support/merged-fixtures';
import { getAuthProvider, getAuthToken } from '@seontechnologies/playwright-utils/auth-session';

type ArtistSummary = { id: string };
type ReleaseDetails = { id: string; slug: string };

/**
 * Story 85 / test design R-02: an unpublished Release's link answers "not found" to other Members.
 * A sample of the API style; the full visibility matrix belongs in the xUnit API suite (C-04).
 */
test.describe('Release visibility', () => {
  test('[P0] a Draft is visible to its manager and not found for another Member', async ({
    apiRequest,
    request,
    authToken,
  }) => {
    // Given a Draft Release managed by the default Member
    await log.step('Create a Draft Release');
    const manager = asMember(authToken);
    const { body: artist } = await apiRequest<ArtistSummary>({
      method: 'POST',
      path: '/api/artists',
      body: { name: `Draft Band ${Date.now()}` },
      headers: manager,
    });
    const { status: created, body: draft } = await apiRequest<ReleaseDetails>({
      method: 'POST',
      path: `/api/artists/${artist.id}/releases`,
      body: releaseFormFactory(),
      headers: manager,
    });
    expect(created).toBe(201);

    // When its manager and another Member ask for it
    const otherState = await getAuthToken(request, { userIdentifier: `${MEMBER}-other` });
    const other = asMember(getAuthProvider().extractToken(otherState) ?? '');

    const asManager = await apiRequest({ method: 'GET', path: `/api/releases/${draft.id}/${draft.slug}`, headers: manager });
    const asOther = await apiRequest({ method: 'GET', path: `/api/releases/${draft.id}/${draft.slug}`, headers: other });

    // Then only the manager sees it
    expect(asManager.status).toBe(200);
    expect(asOther.status).toBe(404);
  });
});

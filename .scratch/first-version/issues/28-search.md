# 28: Search

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A Member types in one search box and sees Artists, Releases and Tracks, each with its top 5 and Show all.

**Blocked by:** 19 (Approve: Published)

**Status:** ready-for-agent

- [ ] Searches Artist names, Release titles, and Track titles plus featured Artists; Published content only
- [ ] `pg_trgm` + `unaccent` with GIN trigram indexes; case- and accent-insensitive; prefix match at 2 characters, trigram similarity ranked by score from 3
- [ ] Results update as you type (~300 ms debounce); the query is in the URL so Back works
- [ ] Show all opens one group with Load more
- [ ] API tests (Aspire testing seam): accents, case, a small typo, 2-character prefix, and unpublished content never found

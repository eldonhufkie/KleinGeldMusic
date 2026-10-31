# 43: Human-only: GitHub and Tailscale

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** The owner sets up the repository and the private path CI uses to reach TrueNAS. Use `/wizard` to walk through it.

**Blocked by:** None (can start immediately)

**Status:** needs-human

- [ ] Repository fully on GitHub (`git push --mirror` from Azure DevOps if anything is missing); push protection on; `main` branch created
- [ ] `production` Environment with Eldon as required reviewer
- [ ] Tailscale Funnel for Production and `tailscale serve` for Test
- [ ] Tailscale OAuth client stored as a GitHub secret; ACL lets `tag:ci` reach only TrueNAS port 22
- [ ] `deploy` user whose SSH key is locked to the forced command `deploy.sh`

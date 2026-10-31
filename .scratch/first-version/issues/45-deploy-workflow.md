# 45: Deploy workflow

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A merge to `main` deploys to Test automatically, and a "Live" approval deploys the same tag to Production.

**Blocked by:** 41 (Human-only: prepare TrueNAS), 42 (Human-only: Azure, Gmail and secrets), 43 (Human-only: GitHub and Tailscale), 44 (Compose output and images)

**Status:** ready-for-agent

- [ ] An ephemeral `tag:ci` runner joins the tailnet via `tailscale/github-action` and SSHes as `deploy`
- [ ] `deploy.sh` accepts only `deploy test <tag>` or `deploy prod <tag>` plus the compose file, writes it and `release.env`, and runs `midclt call app.redeploy <app>`
- [ ] Production waits on the `production` Environment and deploys the same tag; nothing is rebuilt
- [ ] Rollback by changing the tag in `release.env` and redeploying is documented
- [ ] Verified by a real deploy to Test and Production

# Production deployment

The standalone repository builds with `docker build -t moorkyz-tools .` from its root. GitHub Actions validates a locked install, security checks, source gates and a container HTTP smoke test before a production deployment.

## Coolify on Hetzner

| Setting | Value |
| --- | --- |
| Git repository | `Moorky/moorkyz-tools` |
| Branch | `master` |
| Build pack | Dockerfile |
| Build context | Repository root, `/` |
| Dockerfile | `/Dockerfile` |
| Container port | `3000` |
| Public domain | `https://tools.moorkyz.com` |
| Health path | `/` |
| Built-in Git auto-deploy | Disabled; CI is the trigger |

Point the Cloudflare hostname at the approved Hetzner ingress and enable TLS at the proxy. Do not copy server IPs or credentials from examples. Restrict runtime/network access to the reverse proxy and necessary management paths.

The Next.js standalone portal needs no runtime secret or persistent volume. Keep `TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED` and `TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_ENABLED` unset on public hosting. Native media/filesystem services are explicitly opt-in and loopback-only. The portal ships the builder, metadata and allowlisted source bundles. Preserve third-party licenses and resolve the imported VideoCrunch distribution-license decision before public redistribution.

## GitHub configuration

Use a `production` environment, with a deploy-scoped Coolify API token in the `COOLIFY_TOKEN` environment secret. Set repository/environment variables `COOLIFY_BASE_URL` (HTTPS origin), `COOLIFY_RESOURCE_UUID` and optionally `APP_HEALTH_URL` (default `https://tools.moorkyz.com/`). Enable Coolify API access. Keep secrets out of files and Docker build arguments.

Pull requests and manual CI runs validate without production access. Only a passing push to `master` reaches the serialized deployment job. It confirms the current branch head, calls `scripts/deployment/deploy-coolify.cjs`, requires the returned deployment commit to match the tested SHA, polls for a successful terminal status, then checks public HTTP health. A queued API response alone is not deployment success. Protect `master` with the `verify` and `container` checks. Configure environment reviewers only if your release process requires them.

## Verification and recovery

Run the repository checks (including `node --test scripts/deployment/deploy-coolify.test.cjs` through `pnpm test:unit`), then the CI container gate. After deployment, check the health endpoint and the product flow in a browser. Use Coolify's last healthy deployment to roll back, or revert the source commit and let CI redeploy. Preserve runtime credentials across deployments.

## Primary references

Reviewed on 2026-10-23:

- [GitHub Actions secure use](https://docs.github.com/en/actions/reference/security/secure-use): least privilege, secrets and immutable action references.
- [Docker build practices](https://docs.docker.com/build/building/best-practices/): small multi-stage images, non-root runtime and context exclusions.
- [Coolify deploy webhooks](https://coolify.io/docs/core/automation/deploy-webhooks): authenticated triggers and deployment verification.

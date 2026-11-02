# Deployment automation

[deploy-coolify.cjs](deploy-coolify.cjs) is the product-specific authenticated
Coolify client used by the existing production workflow. Moving it here does
not authorize deployment or change its network/credential behavior.
See the [deployment contract](../../docs/deployment/coolify.md).

Its tests use injected responses, not live credentials or production services:

```powershell
node --test src/scripts/deployment/deploy-coolify.test.cjs
```

They also run in `pnpm --dir src test:unit` and therefore in `pnpm --dir src test` / `pnpm --dir src check`.

For a local Windows launcher, use
[build-moorkyz-exe.ps1](build-moorkyz-exe.ps1); see the
[EXE guide](../../.moorkyz/app/README.md). It embeds the project icon and builds the
shared startup launcher locally, without installing prerequisites, signing or
performing any external deployment.

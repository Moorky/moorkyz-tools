# Deployment automation

[deploy-coolify.cjs](deploy-coolify.cjs) is the product-specific authenticated
Coolify client used by the existing production workflow. Moving it here does
not authorize deployment or change its network/credential behavior.
See the [deployment contract](../../../docs/deployment/coolify.md).

Its tests use injected responses, not live credentials or production services:

```powershell
node --test src/scripts/deployment/deploy-coolify.test.cjs
```

They also run in `pnpm --dir src test:unit` and therefore in `pnpm --dir src test` / `pnpm --dir src check`.

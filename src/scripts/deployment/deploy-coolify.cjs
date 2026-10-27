"use strict";

// Portable deployment client. Secrets are read from the environment only.
const { setTimeout: delay } = require("node:timers/promises");

function configuration(env) {
  for (const key of [
    "COOLIFY_BASE_URL",
    "COOLIFY_RESOURCE_UUID",
    "COOLIFY_TOKEN",
    "APP_HEALTH_URL",
  ]) {
    if (!env[key]?.trim())
      throw new Error(
        `Missing ${key}. Configure the repository's production environment before deploying.`,
      );
  }
  const base = new URL(env.COOLIFY_BASE_URL);
  if (
    base.protocol !== "https:" ||
    base.username ||
    base.password ||
    base.search ||
    base.hash ||
    base.pathname !== "/"
  ) {
    throw new Error(
      "COOLIFY_BASE_URL must be an HTTPS origin without credentials, path, query, or fragment.",
    );
  }
  const health = new URL(env.APP_HEALTH_URL);
  if (
    health.protocol !== "https:" ||
    health.username ||
    health.password ||
    health.hash
  )
    throw new Error(
      "APP_HEALTH_URL must be an HTTPS URL without credentials or a fragment.",
    );
  if (!/^[A-Za-z0-9-]{8,128}$/.test(env.COOLIFY_RESOURCE_UUID))
    throw new Error("COOLIFY_RESOURCE_UUID is invalid.");
  const commit = env.DEPLOY_COMMIT_SHA || "";
  if (commit && !/^[a-f0-9]{40}$/i.test(commit))
    throw new Error("DEPLOY_COMMIT_SHA must be a full Git commit SHA.");
  const imageCommit = env.DEPLOY_IMAGE_SHA || "";
  if (imageCommit && !/^[a-f0-9]{40}$/i.test(imageCommit))
    throw new Error("DEPLOY_IMAGE_SHA must be a full Git commit SHA.");
  return {
    base: base.origin,
    uuid: env.COOLIFY_RESOURCE_UUID,
    token: env.COOLIFY_TOKEN,
    health: health.href,
    commit,
    imageCommit,
  };
}

async function deploy(env = process.env, dependencies = {}) {
  const config = configuration(env);
  const request = dependencies.fetch || fetch;
  const pause = dependencies.delay || delay;
  const report = dependencies.report || console.log;
  async function api(path, method = "GET", body) {
    let response;
    try {
      response = await request(`${config.base}/api/v1${path}`, {
        method,
        redirect: "error",
        signal: AbortSignal.timeout(30000),
        headers: {
          Authorization: `Bearer ${config.token}`,
          Accept: "application/json",
          ...(body ? { "Content-Type": "application/json" } : {}),
        },
        ...(body ? { body: JSON.stringify(body) } : {}),
      });
    } catch {
      throw new Error(
        `Coolify ${method} request failed or timed out. Inspect the dashboard before retrying a deployment request.`,
      );
    }
    if (!response.ok)
      throw new Error(
        `Coolify returned HTTP ${response.status}. Check the endpoint, token scope, and resource access.`,
      );
    try {
      return await response.json();
    } catch {
      throw new Error("Coolify returned invalid JSON.");
    }
  }
  // POST is deliberately not retried: a lost response may already have queued a release.
  const queued = await api("/deploy", "POST", {
    uuid: config.uuid,
    force: false,
  });
  const entry = queued.deployments?.find(
    (item) => item.resource_uuid === config.uuid,
  );
  const id = entry?.deployment_uuid;
  if (!id || !/^[A-Za-z0-9-]{8,128}$/.test(id))
    throw new Error(
      "Coolify did not return a deployment ID for the requested resource. Inspect its dashboard before retrying.",
    );
  report(`Queued deployment ${id}. Waiting for completion.`);
  let failures = 0;
  for (let attempt = 0; attempt < 80; attempt += 1) {
    let result;
    try {
      result = await api(`/deployments/${id}`);
      failures = 0;
    } catch (error) {
      failures += 1;
      if (failures >= 3) throw error;
      await pause(15000);
      continue;
    }
    const status = String(result.status || "").toLowerCase();
    if (["failed", "cancelled", "canceled", "error"].includes(status))
      throw new Error(
        `Deployment ${id} ended with status ${status}. Inspect deployment logs and keep the previous release available.`,
      );
    if (status === "finished") {
      const deployedCommit =
        result.commit || result.commit_sha || result.git_commit_sha;
      if (config.commit && deployedCommit !== config.commit)
        throw new Error(
          "Coolify did not confirm the expected source commit. Refusing to report this release as verified.",
        );
      for (let healthAttempt = 0; healthAttempt < 12; healthAttempt += 1) {
        try {
          const response = await request(config.health, {
            redirect: "error",
            signal: AbortSignal.timeout(15000),
            headers: { "Cache-Control": "no-cache" },
          });
          if (response.ok) {
            if (config.imageCommit) {
              const version = await request(
                new URL("/version.txt", config.health),
                {
                  redirect: "error",
                  signal: AbortSignal.timeout(15000),
                  headers: { "Cache-Control": "no-cache" },
                },
              );
              if (
                !version.ok ||
                (await version.text()).trim() !== config.imageCommit
              ) {
                await pause(5000);
                continue;
              }
            }
            report(`Deployment ${id} finished and HTTPS health check passed.`);
            return { deployment: id, status };
          }
        } catch {
          /* bounded readiness retry, without credentials */
        }
        await pause(5000);
      }
      throw new Error(
        "Deployment finished, but the public HTTPS health check did not pass within one minute.",
      );
    }
    if (
      ![
        "queued",
        "in_progress",
        "pending",
        "starting",
        "building",
        "deploying",
      ].includes(status)
    )
      throw new Error(
        "Coolify returned an unknown deployment status. Inspect the dashboard; this release has not been verified.",
      );
    await pause(15000);
  }
  throw new Error(
    "Deployment did not finish within 20 minutes. Inspect the existing deployment before starting another.",
  );
}

module.exports = { configuration, deploy };
if (require.main === module)
  deploy().catch((error) => {
    console.error(`Deployment failed: ${error.message}`);
    process.exitCode = 1;
  });

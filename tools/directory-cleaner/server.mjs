import http from "node:http";
import { randomBytes } from "node:crypto";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { CleanerError, createPlan, applyPlan } from "./core.mjs";

async function readBody(request) {
  if (!/^application\/json(?:;|$)/i.test(request.headers["content-type"] ?? ""))
    throw new CleanerError("Send JSON settings.", 415);
  const chunks = [];
  let size = 0;
  for await (const chunk of request.iterator({ destroyOnReturn: false })) {
    size += chunk.length;
    if (size > 65536) {
      request.resume();
      throw new CleanerError("Request exceeds 64 KiB.", 413);
    }
    chunks.push(chunk);
  }
  try {
    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  } catch {
    throw new CleanerError("Request is not valid JSON.");
  }
}

export function createApplication() {
  const token = randomBytes(32).toString("hex");
  const plans = new Map();
  const runs = new Map();
  let busy = false;
  let active = null;
  const server = http.createServer(async (request, response) => {
    response.setHeader("Cache-Control", "no-store");
    response.setHeader("Content-Type", "application/json; charset=utf-8");
    response.setHeader("X-Content-Type-Options", "nosniff");
    response.setHeader("Referrer-Policy", "no-referrer");
    response.setHeader("Cross-Origin-Resource-Policy", "same-origin");
    response.setHeader(
      "Content-Security-Policy",
      "default-src 'none'; frame-ancestors 'none'",
    );
    try {
      const host = `127.0.0.1:${server.address().port}`;
      if (
        request.headers.host !== host ||
        (request.headers.origin &&
          request.headers.origin !== `http://${host}`) ||
        request.headers["sec-fetch-site"] === "cross-site"
      )
        throw new CleanerError(
          "Only the printed loopback address may use this service.",
          403,
        );
      const url = new URL(request.url, `http://${host}`);
      if (request.method === "GET" && url.pathname === "/api/config") {
        response.end(
          JSON.stringify({
            name: "Moorky Directory Cleaner",
            version: 1,
            token,
            deleteBehavior: "archive",
            activeRunId:
              [...runs.values()].find((run) => run.status === "running")?.id ??
              null,
          }),
        );
        return;
      }
      const runRoute = /^\/api\/runs\/([0-9a-f-]{36})$/.exec(url.pathname);
      if (request.method === "GET" && runRoute) {
        if (request.headers["x-directory-cleaner-token"] !== token)
          throw new CleanerError(
            "Reconnect before reading cleanup results.",
            403,
          );
        const run = runs.get(runRoute[1]);
        if (!run)
          throw new CleanerError(
            "Cleanup run not found. Check the archive receipt.",
            404,
          );
        response.end(JSON.stringify(run));
        return;
      }
      if (
        request.method !== "POST" ||
        !["/api/preview", "/api/apply"].includes(url.pathname)
      )
        throw new CleanerError("Unknown cleaner operation.", 404);
      if (request.headers["x-directory-cleaner-token"] !== token)
        throw new CleanerError(
          "Reconnect before changing or previewing local files.",
          403,
        );
      if (busy)
        throw new CleanerError(
          "Another preview or cleanup is in progress.",
          409,
        );
      busy = true;
      try {
        const body = await readBody(request);
        for (const [id, saved] of plans)
          if (Date.now() - saved.at > 600000) plans.delete(id);
        if (url.pathname === "/api/preview") {
          const plan = await createPlan(body);
          while (plans.size >= 10) plans.delete(plans.keys().next().value);
          plans.set(plan.id, { at: Date.now(), plan });
          response.end(JSON.stringify(plan));
        } else {
          if (
            !body ||
            typeof body.planId !== "string" ||
            typeof body.confirmation !== "string" ||
            Object.keys(body).some(
              (key) => !["planId", "confirmation"].includes(key),
            )
          )
            throw new CleanerError(
              "Apply requires a preview ID and confirmation.",
            );
          const saved = plans.get(body.planId);
          if (!saved)
            throw new CleanerError(
              "Preview expired, was already used or does not exist. Preview again.",
              409,
            );
          if (body.confirmation !== `APPLY ${body.planId}`)
            throw new CleanerError("Enter the exact APPLY confirmation.");
          plans.delete(body.planId);
          const run = {
            id: body.planId,
            status: "running",
            result: null,
            error: null,
          };
          while (runs.size >= 10) runs.delete(runs.keys().next().value);
          runs.set(run.id, run);
          active = (async () => {
            try {
              run.result = await applyPlan(saved.plan, body.confirmation);
              run.status = run.result.status;
            } catch (error) {
              console.error("Moorky Directory Cleaner apply failed:", error);
              run.status = "failed";
              run.error = error.message;
            } finally {
              busy = false;
              active = null;
            }
          })();
          response.writeHead(202);
          response.end(JSON.stringify(run));
        }
      } finally {
        if (!active) busy = false;
      }
    } catch (error) {
      const status = error instanceof CleanerError ? error.status : 500;
      if (status === 500) console.error("Moorky Directory Cleaner:", error);
      response.writeHead(status);
      response.end(
        JSON.stringify({
          error:
            status === 500
              ? `Local cleanup failed: ${error.message}. No automatic retry was attempted; inspect the receipt if files had already moved.`
              : error.message,
        }),
      );
    }
  });
  server.requestTimeout = 30000;
  return {
    server,
    close: async () => {
      const stopped = new Promise((resolve, reject) =>
        server.close((error) => (error ? reject(error) : resolve())),
      );
      await active;
      await stopped;
    },
  };
}

if (
  process.argv[1] &&
  path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  if (Number(process.versions.node.split(".")[0]) < 22)
    throw new Error("Node.js 22+ is required.");
  const portIndex = process.argv.indexOf("--port");
  const port = portIndex === -1 ? 8766 : Number(process.argv[portIndex + 1]);
  if (!Number.isInteger(port) || port < 1 || port > 65535)
    throw new Error("Choose a TCP port between 1 and 65535.");
  const application = createApplication();
  application.server.once("error", (error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
  application.server.listen(port, "127.0.0.1", () =>
    console.log(
      `Moorky Directory Cleaner: http://127.0.0.1:${port}. Keep this terminal open. Delete always archives.`,
    ),
  );
  for (const signal of ["SIGINT", "SIGTERM"])
    process.once(signal, () =>
      application.close().catch((error) => {
        console.error(error);
        process.exitCode = 1;
      }),
    );
}

import assert from "node:assert/strict";
import { test } from "node:test";
import { proxyLocalTool } from "../../packages/portal/src/server/local-tool-proxy.ts";

test("Moorky Directory Cleaner gateway is independently off, fixed-loopback, bounded and token-specific", async (t) => {
  const keys = [
    "TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_ENABLED",
    "TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_PORT",
    "TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED",
  ];
  const saved = keys.map((key) => process.env[key]);
  t.after(() =>
    keys.forEach((key, index) =>
      saved[index] === undefined
        ? delete process.env[key]
        : (process.env[key] = saved[index]),
    ),
  );
  delete process.env.TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_ENABLED;
  delete process.env.TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_PORT;
  process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED = "1";
  const calls = [];
  t.mock.method(globalThis, "fetch", async (url, options) => {
    calls.push({ url: url.href, options });
    return Response.json(
      {
        id: "00000000-0000-0000-0000-000000000001",
        status: "running",
        result: null,
        error: null,
      },
      { status: 202 },
    );
  });
  function request(resource, options = {}) {
    return new Request(
      `http://127.0.0.1:4174/api/local-tools/directory-cleaner/${resource}`,
      { ...options, headers: { host: "127.0.0.1:4174", ...options.headers } },
    );
  }
  assert.equal(
    (
      await proxyLocalTool(
        request("api/config"),
        ["api", "config"],
        "directory-cleaner",
      )
    ).status,
    503,
  );
  assert.equal(calls.length, 0);
  process.env.TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_ENABLED = "1";
  assert.equal(
    (
      await proxyLocalTool(
        request("server.mjs"),
        ["server.mjs"],
        "directory-cleaner",
      )
    ).status,
    404,
  );
  assert.equal(
    (
      await proxyLocalTool(
        request("api/config", { headers: { origin: "https://evil.example" } }),
        ["api", "config"],
        "directory-cleaner",
      )
    ).status,
    403,
  );
  const response = await proxyLocalTool(
    request("api/apply", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-directory-cleaner-token": "cleaner-token",
        "x-webvideocrunch-token": "video-token",
        cookie: "private=1",
      },
      body: JSON.stringify({ planId: "id", confirmation: "APPLY id" }),
    }),
    ["api", "apply"],
    "directory-cleaner",
  );
  assert.equal(response.status, 202);
  assert.equal(calls[0].url, "http://127.0.0.1:8766/api/apply");
  assert.equal(
    calls[0].options.headers.get("x-directory-cleaner-token"),
    "cleaner-token",
  );
  assert.equal(calls[0].options.headers.get("x-webvideocrunch-token"), null);
  assert.equal(calls[0].options.headers.get("cookie"), null);
  assert.equal(
    (
      await proxyLocalTool(
        request("api/preview", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: "x".repeat(65537),
        }),
        ["api", "preview"],
        "directory-cleaner",
      )
    ).status,
    413,
  );
  assert.equal(calls.length, 1);
});

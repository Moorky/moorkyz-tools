import assert from "node:assert/strict";
import { test } from "node:test";
import { proxyVideoCrunch } from "../../packages/portal/src/server/videocrunch-proxy.ts";

function request(path = "api/config", options = {}) {
  return new Request(
    `http://127.0.0.1:4174/api/local-tools/videocrunch/${path}`,
    { ...options, headers: { host: "127.0.0.1:4174", ...options.headers } },
  );
}

test("local service is zero-network while off and denies public/cross-site access", async (t) => {
  const originalEnabled = process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED;
  const originalPort = process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_PORT;
  t.after(() => {
    if (originalEnabled === undefined)
      delete process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED;
    else process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED = originalEnabled;
    if (originalPort === undefined)
      delete process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_PORT;
    else process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_PORT = originalPort;
  });
  const fetch = t.mock.method(globalThis, "fetch", async () => {
    throw new Error("must not fetch");
  });
  delete process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED;
  assert.equal(
    (await proxyVideoCrunch(request(), ["api", "config"])).status,
    503,
  );
  process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED = "1";
  for (const headers of [
    { host: "tools.moorkyz.com" },
    { origin: "https://evil.example" },
    { "sec-fetch-site": "cross-site" },
    { host: "127.0.0.1.evil.example" },
  ]) {
    assert.equal(
      (
        await proxyVideoCrunch(request("api/config", { headers }), [
          "api",
          "config",
        ])
      ).status,
      403,
    );
  }
  for (const segments of [
    ["..", "server.js"],
    ["api/config"],
    ["server.js"],
    ["api", "jobs", "abc", "files", "..", "history.json"],
  ]) {
    assert.equal((await proxyVideoCrunch(request(), segments)).status, 404);
  }
  assert.equal(
    (
      await proxyVideoCrunch(
        request("api/config", { method: "POST", body: "{}" }),
        ["api", "config"],
      )
    ).status,
    405,
  );
  process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_PORT = "https://evil.example";
  assert.equal(
    (await proxyVideoCrunch(request(), ["api", "config"])).status,
    503,
  );
  assert.equal(fetch.mock.callCount(), 0);
});

test("local proxy forwards only bounded intended data and preserves streams/ranges/errors", async (t) => {
  const original = process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED;
  const originalPort = process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_PORT;
  process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED = "1";
  delete process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_PORT;
  t.after(() => {
    if (original === undefined)
      delete process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED;
    else process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED = original;
    if (originalPort === undefined)
      delete process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_PORT;
    else process.env.TOOLS_MOORKYZ_COM_VIDEOCRUNCH_PORT = originalPort;
  });
  const calls = [];
  const fetch = t.mock.method(globalThis, "fetch", async (url, options) => {
    calls.push({ url: url.href, options });
    return new Response("part", {
      status: 206,
      headers: {
        "Content-Type": "video/mp4",
        "Content-Range": "bytes 0-3/12",
        "Content-Length": "4",
        "Accept-Ranges": "bytes",
      },
    });
  });
  const response = await proxyVideoCrunch(
    request("api/jobs/abc/files/original/source.mp4?download=1", {
      headers: {
        range: "bytes=0-3",
        cookie: "private=1",
        authorization: "secret",
      },
    }),
    ["api", "jobs", "abc", "files", "original", "source.mp4"],
  );
  assert.equal(response.status, 206);
  assert.equal(response.headers.get("content-range"), "bytes 0-3/12");
  assert.equal(await response.text(), "part");
  assert.equal(
    calls[0].url,
    "http://127.0.0.1:8765/api/jobs/abc/files/original/source.mp4?download=1",
  );
  assert.equal(calls[0].options.headers.get("range"), "bytes=0-3");
  assert.equal(calls[0].options.headers.get("cookie"), null);
  assert.equal(calls[0].options.headers.get("authorization"), null);
  assert.equal(calls[0].options.redirect, "error");
  const config = {
    url: "https://example.com/video",
    outputDir: "C:\\Exports",
    compression: "Crunch",
    skipCompression: false,
  };
  await proxyVideoCrunch(
    request("api/jobs", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-webvideocrunch-token": "process-token",
      },
      body: JSON.stringify(config),
    }),
    ["api", "jobs"],
  );
  assert.deepEqual(
    JSON.parse(new TextDecoder().decode(calls[1].options.body)),
    config,
  );
  assert.equal(
    calls[1].options.headers.get("x-webvideocrunch-token"),
    "process-token",
  );
  const before = fetch.mock.callCount();
  assert.equal(
    (
      await proxyVideoCrunch(
        request("api/jobs", {
          method: "POST",
          headers: { "content-type": "application/json" },
          body: "x".repeat(16385),
        }),
        ["api", "jobs"],
      )
    ).status,
    413,
  );
  assert.equal(
    (
      await proxyVideoCrunch(
        request("api/jobs", { method: "POST", body: "{}" }),
        ["api", "jobs"],
      )
    ).status,
    415,
  );
  assert.equal(fetch.mock.callCount(), before);
  await proxyVideoCrunch(request("index.html"), ["index.html"]);
  assert.equal(calls.at(-1).url, "http://127.0.0.1:8765/");
  fetch.mock.mockImplementation(async () => {
    throw new Error("connection refused");
  });
  const logging = t.mock.method(console, "error", () => {});
  const unavailable = await proxyVideoCrunch(request(), ["api", "config"]);
  assert.equal(unavailable.status, 502);
  assert.match(
    (await unavailable.json()).error,
    /Start pnpm --dir src tool:videocrunch/,
  );
  assert.equal(logging.mock.callCount(), 1);
});

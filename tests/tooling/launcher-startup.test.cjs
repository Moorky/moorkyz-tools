"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { test } = require("node:test");

const root = path.resolve(__dirname, "../..");
const server = `
const http = require("node:http");
const mode = process.argv[2];
const began = Date.now();
const server = http.createServer((request, response) => {
  const ready = mode !== "unhealthy" && Date.now() - began >= 700;
  response.writeHead(ready ? 200 : 503);
  response.end(JSON.stringify({ ready, mode, environment: process.env.NODE_ENV }));
});
server.listen(Number(process.env.PORT), "127.0.0.1", () => {
  if (mode === "transient") setTimeout(() => {
    console.error("Synthetic startup failure after binding.");
    server.close(() => process.exit(1));
  }, 200);
});
`;

for (const launcher of ["moorkyz", "tools.moorkyz.com"]) {
  function fixture(t, mode, timeout = 8000) {
    const folder = fs.mkdtempSync(path.join(os.tmpdir(), "moorkyz-startup-"));
    const cli = path.join(root, `${launcher}.cjs`);
    function run(...args) {
      return spawnSync(process.execPath, [cli, ...args], {
        cwd: folder,
        encoding: "utf8",
        timeout: 20000,
        env: { ...process.env, CI: "1" },
      });
    }
    t.after(() => {
      const stopped = run("stop");
      assert.equal(stopped.status, 0, stopped.stdout + stopped.stderr);
      fs.rmSync(folder, { recursive: true, force: true });
    });
    fs.writeFileSync(
      path.join(folder, "package.json"),
      JSON.stringify({
        private: true,
        packageManager: "pnpm@11.8.0",
        scripts: {
          dev: `node server.cjs ${mode}`,
          start: "node server.cjs production",
        },
      }),
    );
    fs.writeFileSync(
      path.join(folder, ".npmrc"),
      "verify-deps-before-run=error\n",
    );
    fs.writeFileSync(path.join(folder, "server.cjs"), server);
    fs.writeFileSync(
      path.join(folder, `${launcher}.config.json`),
      JSON.stringify({
        projects: {
          portal: {
            cwd: ".",
            url: "http://127.0.0.1:4320",
            startupTimeoutMs: timeout,
          },
        },
      }),
    );
    return run;
  }

  test(`${launcher} start waits for HTTP readiness and supports restart, production, status, logs and stop`, async (t) => {
    const run = fixture(t, "delayed");
    for (const args of [["start"], ["restart", "--prod"]]) {
      const started = run(...args);
      assert.equal(started.status, 0, started.stdout + started.stderr);
      const url = /URL: (http:\/\/127\.0\.0\.1:\d+)/.exec(started.stdout)?.[1];
      assert.ok(url, started.stdout);
      const response = await fetch(url);
      assert.equal(response.status, 200);
      const data = await response.json();
      assert.equal(data.ready, true);
      assert.equal(
        data.environment,
        args[0] === "start" ? "development" : "production",
      );
      assert.match(run("status").stdout, /RUNNING/);
      assert.equal(run("logs").status, 0);
    }
    assert.equal(run("stop").status, 0);
    assert.match(run("status").stdout, /STOPPED/);
  });

  test(`${launcher} rejects a transient listener instead of reporting false startup success`, (t) => {
    const run = fixture(t, "transient");
    const result = run("start");
    assert.equal(result.status, 1, result.stdout + result.stderr);
    assert.doesNotMatch(result.stdout, /Started 'portal'/);
    assert.match(result.stderr, /Synthetic startup failure after binding/);
    assert.match(run("status").stdout, /STOPPED/);
  });

  test(`${launcher} stops an unhealthy server on readiness timeout`, (t) => {
    const run = fixture(t, "unhealthy", 1500);
    const result = run("start");
    assert.equal(result.status, 1, result.stdout + result.stderr);
    assert.match(result.stderr, /responding successfully over HTTP/);
    assert.match(run("status").stdout, /STOPPED/);
  });
}

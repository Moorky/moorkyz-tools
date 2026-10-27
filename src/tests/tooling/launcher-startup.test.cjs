"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { test } = require("node:test");

const root = path.resolve(__dirname, "../../..");
function fixtureEnvironment() {
  const env = { ...process.env, CI: "1" };
  // pnpm disables recursive verification inside its lifecycle environment.
  // These fixtures model a standalone launcher, using their own workspace policy.
  for (const key of Object.keys(env)) {
    if (key.toLowerCase() === "pnpm_config_verify_deps_before_run")
      delete env[key];
  }
  return env;
}

function pnpm(folder, ...args) {
  const command =
    process.platform === "win32" ? process.env.ComSpec || "cmd.exe" : "pnpm";
  return spawnSync(
    command,
    process.platform === "win32" ? ["/d", "/s", "/c", "pnpm", ...args] : args,
    {
      cwd: folder,
      encoding: "utf8",
      timeout: 20000,
      env: fixtureEnvironment(),
    },
  );
}

test("pnpm dependency verification rejects automatic installs at the root and portal workspace", () => {
  for (const folder of [
    path.join(root, "src"),
    path.join(root, "src", "packages", "portal"),
  ]) {
    const result = pnpm(folder, "config", "get", "verifyDepsBeforeRun");
    assert.equal(result.status, 0, result.stdout + result.stderr);
    assert.equal(result.stdout.trim(), "error");
  }
});

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
    const cli = path.join(
      root,
      launcher === "moorkyz"
        ? "moorkyz.cjs"
        : "src/.moorkyz/launchers/tools.moorkyz.com.cjs",
    );
    function run(...args) {
      return spawnSync(process.execPath, [cli, ...args], {
        cwd: folder,
        encoding: "utf8",
        timeout: 20000,
        env: fixtureEnvironment(),
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
          probe: "node --version",
        },
      }),
    );
    fs.copyFileSync(
      path.join(root, "src", "pnpm-workspace.yaml"),
      path.join(folder, "pnpm-workspace.yaml"),
    );
    fs.writeFileSync(
      path.join(folder, "pnpm-lock.yaml"),
      "lockfileVersion: '9.0'\nsettings:\n  autoInstallPeers: true\n  excludeLinksFromLockfile: false\nimporters:\n  .: {}\n",
    );
    const installed = pnpm(
      folder,
      "install",
      "--offline",
      "--frozen-lockfile",
      "--ignore-scripts",
    );
    assert.equal(installed.status, 0, installed.stdout + installed.stderr);
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
    return { run, folder };
  }

  test(`${launcher} start waits for HTTP readiness and supports restart, production, status, logs and stop`, async (t) => {
    const { run, folder } = fixture(t, "delayed");
    const legacy = launcher === "tools.moorkyz.com";
    if (legacy) {
      fs.mkdirSync(path.join(folder, ".tools.moorkyz.com"));
      fs.writeFileSync(
        path.join(folder, ".tools.moorkyz.com", "portal.log"),
        "Preserved legacy log.\n",
      );
      fs.mkdirSync(path.join(folder, "logs"));
      fs.writeFileSync(
        path.join(folder, "logs", "unmanaged.log"),
        "Do not rotate.",
      );
    }
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
      if (legacy) {
        assert.match(
          started.stdout,
          /Logs: \.tools\.moorkyz\.com[\\/]portal\.log/,
        );
        assert.ok(
          fs
            .readFileSync(
              path.join(folder, ".tools.moorkyz.com", "portal.log"),
              "utf8",
            )
            .startsWith("Preserved legacy log.\n"),
        );
        assert.equal(
          fs.readFileSync(path.join(folder, "logs", "unmanaged.log"), "utf8"),
          "Do not rotate.",
        );
        assert.equal(fs.existsSync(path.join(folder, ".moorkyz")), false);
        assert.equal(fs.existsSync(path.join(folder, "archive")), false);
        assert.equal(fs.existsSync(path.join(folder, "trash")), false);
      }
    }
    assert.equal(run("stop").status, 0);
    assert.match(run("status").stdout, /STOPPED/);
  });

  test(`${launcher} rejects a transient listener instead of reporting false startup success`, (t) => {
    const { run } = fixture(t, "transient");
    const result = run("start");
    assert.equal(result.status, 1, result.stdout + result.stderr);
    assert.doesNotMatch(result.stdout, /Started 'portal'/);
    assert.match(result.stderr, /Synthetic startup failure after binding/);
    assert.match(run("status").stdout, /STOPPED/);
  });

  test(`${launcher} stops an unhealthy server on readiness timeout`, (t) => {
    const { run } = fixture(t, "unhealthy", 1500);
    const result = run("start");
    assert.equal(result.status, 1, result.stdout + result.stderr);
    assert.match(result.stderr, /responding successfully over HTTP/);
    assert.match(run("status").stdout, /STOPPED/);
  });

  test(`${launcher} rejects stale dependencies without installing during startup or script execution`, (t) => {
    const { run, folder } = fixture(t, "delayed");
    const manifestPath = path.join(folder, "package.json");
    const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
    manifest.dependencies = {
      "missing-fixture-dependency": "file:./missing-fixture-dependency",
    };
    fs.writeFileSync(manifestPath, JSON.stringify(manifest));
    fs.writeFileSync(
      path.join(folder, "node_modules", "installation-marker.txt"),
      "Preserve the existing installation.",
    );
    const filenames = [
      "package.json",
      "pnpm-lock.yaml",
      path.join("node_modules", "installation-marker.txt"),
      path.join("node_modules", ".pnpm-workspace-state-v1.json"),
    ];
    const before = filenames.map((filename) =>
      fs.readFileSync(path.join(folder, filename)),
    );

    for (const args of [["start"], ["run", "probe"]]) {
      const result = run(...args);
      const output = result.stdout + result.stderr;
      assert.equal(result.status, 1, output);
      assert.match(output, /ERR_PNPM_VERIFY_DEPS_BEFORE_RUN/);
      assert.doesNotMatch(
        output,
        /ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY|Scope: all/,
      );
      assert.doesNotMatch(result.stdout, /Started 'portal'/);
      assert.match(run("status").stdout, /STOPPED/);
      assert.deepEqual(
        filenames.map((filename) =>
          fs.readFileSync(path.join(folder, filename)),
        ),
        before,
      );
      assert.equal(
        fs.existsSync(
          path.join(folder, "node_modules", "missing-fixture-dependency"),
        ),
        false,
      );
    }
  });
}

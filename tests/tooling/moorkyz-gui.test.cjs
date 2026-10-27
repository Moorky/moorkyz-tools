"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { test } = require("node:test");
const { guiInvocation } = require("../../.template/cli/helpers/gui.cjs");

const root = path.resolve(__dirname, "..", "..");
const options = {
  platform: "win32",
  root: "C:\\fixture with spaces\\project",
  entrypoint: "C:\\fixture with spaces\\moorkyz.cjs",
  node: process.execPath,
  config: { projects: { portal: {}, secondary: {} }, defaultProject: "portal" },
};

test("GUI invocation is explicit, STA, profile-free and preserves the CLI namespace", () => {
  const { command, args } = guiInvocation(options);
  assert.equal(path.basename(command).toLowerCase(), "powershell.exe");
  assert.ok(args.includes("-STA"));
  assert.ok(args.includes("-NoProfile"));
  assert.ok(!args.includes("-ExecutionPolicy"));
  assert.equal(args[args.indexOf("-Root") + 1], options.root);
  assert.equal(args[args.indexOf("-EntryPoint") + 1], options.entrypoint);
  assert.equal(args[args.indexOf("-NodePath") + 1], options.node);
  assert.ok(fs.existsSync(args[args.indexOf("-File") + 1]));
  assert.deepEqual(
    JSON.parse(Buffer.from(args.at(-1), "base64").toString("utf8")),
    { names: ["portal", "secondary"], defaultProject: "portal" },
  );
  const alternate = guiInvocation({
    ...options,
    entrypoint: "C:\\fixture\\tools.moorkyz.com.cjs",
  });
  assert.equal(
    alternate.args[alternate.args.indexOf("-EntryPoint") + 1],
    "C:\\fixture\\tools.moorkyz.com.cjs",
  );
});

test("unsupported GUI platforms fail explicitly rather than changing CLI behavior", () => {
  for (const platform of ["linux", "darwin"])
    assert.throws(
      () => guiInvocation({ ...options, platform }),
      /currently requires Windows/,
    );
});

test("both launchers expose GUI help and reject GUI arguments without opening it", (t) => {
  const fixture = fs.mkdtempSync(path.join(os.tmpdir(), "moorkyz-gui-cli-"));
  t.after(() => fs.rmSync(fixture, { recursive: true, force: true }));
  for (const entrypoint of ["moorkyz", "tools.moorkyz.com"]) {
    const run = (...args) =>
      spawnSync(
        process.execPath,
        [path.join(root, `${entrypoint}.cjs`), ...args],
        {
          cwd: fixture,
          encoding: "utf8",
          timeout: 10000,
        },
      );
    const help = run("help", "gui");
    assert.equal(help.status, 0, help.stderr);
    assert.match(help.stdout, new RegExp(`${entrypoint} gui`));
    const invalid = run("gui", "unexpected");
    assert.equal(invalid.status, 1, invalid.stdout + invalid.stderr);
    assert.match(invalid.stderr, /Usage: .* gui/);
    assert.deepEqual(fs.readdirSync(fixture), []);
  }
});

test(
  "native WPF GUI, shared effects and CLI actions work against isolated fixtures",
  { skip: process.platform !== "win32", timeout: 60000 },
  () => {
    const result = spawnSync(
      "powershell.exe",
      [
        "-NoProfile",
        "-STA",
        "-File",
        path.join(__dirname, "moorkyz-gui.test.ps1"),
      ],
      { cwd: root, encoding: "utf8", timeout: 55000 },
    );
    assert.equal(result.status, 0, result.stdout + result.stderr);
    assert.match(result.stdout, /PASS: native Moorkyz GUI/);
  },
);

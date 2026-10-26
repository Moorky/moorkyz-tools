"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const {
  mkdtempSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { after, test } = require("node:test");

const repoRoot = path.resolve(__dirname, "../..");
const cli = path.join(repoRoot, "tools.moorkyz.com.cjs");
const fixtures = [];

function fixture() {
  const folder = mkdtempSync(path.join(os.tmpdir(), "tools.moorkyz.com-cli-"));
  fixtures.push(folder);
  return folder;
}

function run(folder, args) {
  return spawnSync(process.execPath, [cli, ...args], {
    cwd: folder,
    encoding: "utf8",
    timeout: 10000,
  });
}

after(() => {
  for (const folder of fixtures.splice(0)) {
    const relative = path.relative(os.tmpdir(), folder);
    if (!relative.startsWith("tools.moorkyz.com-cli-")) {
      throw new Error(`Refusing to remove unexpected fixture path: ${folder}`);
    }
    rmSync(folder, { recursive: true, force: true });
  }
});

test("help works without package scaffolding", () => {
  const result = run(fixture(), ["help"]);
  assert.equal(result.status, 0, result.stderr);
  assert.match(
    result.stdout,
    /tools\.moorkyz\.com — reusable project development CLI/,
  );
  assert.match(result.stdout, /tools\.moorkyz\.com audit/);
});

test("default project is the portal workspace", () => {
  const result = run(repoRoot, ["list"]);
  assert.equal(result.status, 0, result.stderr);
  assert.match(
    result.stdout,
    /portal \(default\): packages[\\/]portal — dev:dev prod:start/,
  );
});

test("audit writes a task report and preserves replacement-like title text", () => {
  const folder = fixture();
  const audits = path.join(folder, "docs", "audits");
  mkdirSync(audits, { recursive: true });
  writeFileSync(
    path.join(audits, "TEMPLATE.md"),
    "# {{TASK_NAME}}\n\nCreated: {{DATE_UTC}}\nID: {{AUDIT_ID}}\n",
  );
  const title = "$& and {{DATE_UTC}}";
  const result = run(folder, ["audit", title]);
  assert.equal(result.status, 0, result.stderr);

  const report = readdirSync(audits).find((name) => name !== "TEMPLATE.md");
  assert.ok(report, "expected a generated audit report");
  const contents = readFileSync(path.join(audits, report), "utf8");
  assert.ok(contents.startsWith(`# ${title}\n`));
  assert.match(contents, /Created: \d{4}-\d\d-\d\dT/);
  assert.match(contents, /ID: \d{4}-\d\d-\d\dT/);
});

test("rejects a project path that escapes the repository", () => {
  const folder = fixture();
  const outside = fixture();
  writeFileSync(
    path.join(folder, "tools.moorkyz.com.config.json"),
    JSON.stringify({ projects: { escape: { cwd: outside } } }),
  );
  const result = run(folder, ["list"]);
  assert.equal(result.status, 1);
  assert.match(result.stderr, /Configured project path escapes repository/);
});

test("doctor reports the renamed configuration and ignored runtime directory", () => {
  const result = run(repoRoot, ["doctor"]);
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /tools\.moorkyz\.com config: present/);
  assert.match(
    result.stdout,
    /Runtime directory: \.tools\.moorkyz\.com \(ignored by Git\)/,
  );
});

test("invalid renamed configuration reports its exact filename", () => {
  const folder = fixture();
  writeFileSync(path.join(folder, "tools.moorkyz.com.config.json"), "[]");
  const result = run(folder, ["list"]);
  assert.equal(result.status, 1);
  assert.match(
    result.stderr,
    /tools\.moorkyz\.com\.config\.json must contain a JSON object/,
  );
});

test("rejects invalid startup timeouts before launching a project", () => {
  const folder = fixture();
  writeFileSync(
    path.join(folder, "tools.moorkyz.com.config.json"),
    JSON.stringify({ projects: { portal: { startupTimeoutMs: 0 } } }),
  );
  const result = run(folder, ["start"]);
  assert.equal(result.status, 1);
  assert.match(result.stderr, /startupTimeoutMs.*1000 to 300000/);
});

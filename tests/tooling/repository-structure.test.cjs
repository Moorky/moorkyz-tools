"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { test } = require("node:test");
const {
  checkSyntax,
} = require("../../.template/maintenance/helpers/check-syntax.cjs");

const root = path.resolve(__dirname, "..", "..");
const read = (filename) => fs.readFileSync(path.join(root, filename), "utf8");
const json = (filename) => JSON.parse(read(filename));

function fixture(t) {
  const folder = fs.mkdtempSync(path.join(os.tmpdir(), "moorkyz-structure-"));
  t.after(() => fs.rmSync(folder, { recursive: true, force: true }));
  return folder;
}

function run(entrypoint, folder, ...args) {
  return spawnSync(process.execPath, [path.join(root, entrypoint), ...args], {
    cwd: folder,
    encoding: "utf8",
    timeout: 10_000,
    env: { ...process.env, CI: "1" },
  });
}

test("tools are local directories tracked by the parent repository, not nested repositories", () => {
  const tools = fs
    .readdirSync(path.join(root, "tools"), { withFileTypes: true })
    .filter((entry) => entry.isDirectory() || entry.isSymbolicLink());
  assert.ok(tools.length > 0);
  for (const entry of tools) {
    assert.equal(entry.isSymbolicLink(), false, entry.name);
    assert.equal(
      fs.existsSync(path.join(root, "tools", entry.name, ".git")),
      false,
      entry.name,
    );
  }
  const tracked = spawnSync("git", ["ls-files", "--stage", "--", "tools"], {
    cwd: root,
    encoding: "utf8",
  });
  assert.equal(tracked.status, 0, tracked.stderr);
  assert.doesNotMatch(
    tracked.stdout,
    /^160000 /m,
    "No tool may be a Git submodule.",
  );
  for (const entry of tools)
    assert.ok(tracked.stdout.includes(`\ttools/${entry.name}/`), entry.name);
});

test("root launchers delegate to one core and the storage API is a compatibility re-export", () => {
  for (const entrypoint of ["moorkyz.cjs", "tools.moorkyz.com.cjs"]) {
    assert.match(
      read(entrypoint),
      /require\("\.\/\.template\/cli\/moorkyz\.cjs"\)/,
    );
    assert.doesNotMatch(read(entrypoint), /function |spawn\(|createServer\(/);
  }
  assert.equal(
    require("../../moorkyz-storage.cjs"),
    require("../../.template/cli/helpers/storage.cjs"),
  );
});

test("both launcher configs resolve existing workspace scripts and preserve their preferred ports", () => {
  for (const [launcher, port] of [
    ["moorkyz", 4320],
    ["tools.moorkyz.com", 3001],
  ]) {
    const config = json(`${launcher}.config.json`);
    assert.equal(config.defaultProject, "portal");
    const project = config.projects[config.defaultProject];
    assert.equal(new URL(project.url).hostname, "127.0.0.1");
    assert.equal(new URL(project.url).port, String(port));
    const workspace = json(path.join(project.cwd, "package.json"));
    assert.ok(workspace.scripts[project.dev]);
    assert.ok(workspace.scripts[project.prod]);
    const result = run(`${launcher}.cjs`, root, "list");
    assert.equal(result.status, 0, result.stdout + result.stderr);
    assert.match(result.stdout, /portal \(default\)/);
  }
});

test("local and CI gates include repository, source, storage and deployment checks", () => {
  const { scripts } = json("package.json");
  for (const gate of [
    "check:repository",
    "lint",
    "format:check",
    "test",
    "test:browser",
    "typecheck",
    "check:source",
  ]) {
    assert.ok(scripts.check.split(" && ").includes(`pnpm ${gate}`), gate);
  }
  for (const selector of [
    "tests/tooling/*.node.cjs",
    "scripts/deployment/*.test.cjs",
  ]) {
    assert.ok(scripts["test:unit"].includes(selector), selector);
  }
  assert.equal(scripts.test, "pnpm build && pnpm test:unit");
  const workflow = read(".github/workflows/ci.yml");
  assert.match(workflow, /run: pnpm check:repository/);
  assert.match(workflow, /run: pnpm check\n/);
  assert.match(workflow, /run: node scripts\/deployment\/deploy-coolify\.cjs/);
  assert.doesNotMatch(workflow, /node scripts\/deploy-coolify/);
  assert.equal(
    fs.existsSync(path.join(root, "scripts", "deploy-coolify.cjs")),
    false,
  );
});

test("runtime data stays ignored by Git and excluded from container contexts", () => {
  for (const folder of [
    ".moorkyz",
    ".tools.moorkyz.com",
    "logs",
    "archive",
    "trash",
  ]) {
    const result = spawnSync(
      "git",
      ["check-ignore", "--no-index", "--quiet", `${folder}/fixture.json`],
      {
        cwd: root,
        encoding: "utf8",
        timeout: 10_000,
      },
    );
    assert.equal(result.status, 0, `${folder}: ${result.stderr}`);
    assert.ok(read(".dockerignore").split(/\r?\n/).includes(folder), folder);
  }
  const result = spawnSync(
    "git",
    ["check-ignore", "--no-index", "--quiet", ".audits/fixture.md"],
    {
      cwd: root,
      encoding: "utf8",
      timeout: 10_000,
    },
  );
  assert.equal(result.status, 1, "task audits must remain trackable");
});

test("maintained hierarchy documentation has no broken local Markdown links", () => {
  for (const filename of [
    ".template/README.md",
    ".template/cli/README.md",
    ".template/maintenance/README.md",
    ".audits/README.md",
    "scripts/README.md",
    "scripts/deployment/README.md",
    "docs/README.md",
    "docs/engineering/repository-structure.md",
  ]) {
    for (const [, target] of read(filename).matchAll(
      /\[[^\]]+\]\(([^)]+)\)/g,
    )) {
      if (/^[a-z]+:|^#/i.test(target)) continue;
      const destination = path.resolve(
        root,
        path.dirname(filename),
        target.split("#")[0],
      );
      assert.ok(fs.existsSync(destination), `${filename}: ${target}`);
    }
  }
});

test("checkups parse sources without executing them and report invalid syntax", (t) => {
  const folder = fixture(t);
  const sentinel = path.join(folder, "must-not-exist.txt");
  fs.writeFileSync(
    path.join(folder, "safe.cjs"),
    `require("node:fs").writeFileSync(${JSON.stringify(sentinel)}, "unexpected");`,
  );
  fs.writeFileSync(path.join(folder, "broken.mjs"), "export const = ;");
  const result = checkSyntax(folder, [
    "safe.cjs",
    "broken.mjs",
    "deleted.js",
    "README.md",
    "safe.cjs",
  ]);
  assert.equal(result.checked, 2);
  assert.equal(result.failures.length, 1);
  assert.match(result.failures[0], /broken\.mjs:.*SyntaxError/s);
  assert.equal(fs.existsSync(sentinel), false);
});

test("checkups reject escaped and linked sources without executing them", (t) => {
  const folder = fixture(t);
  const outside = fixture(t);
  fs.writeFileSync(
    path.join(outside, "outside.cjs"),
    "throw new Error('not executed');",
  );
  fs.symlinkSync(
    outside,
    path.join(folder, "linked"),
    process.platform === "win32" ? "junction" : "dir",
  );
  const result = checkSyntax(folder, [
    path.join(outside, "outside.cjs"),
    "linked/outside.cjs",
  ]);
  assert.equal(result.checked, 0);
  assert.equal(result.failures.length, 2);
  assert.match(result.failures[0], /escapes project scope/);
  assert.match(result.failures[1], /symlink or junction/);
});

test("checkup previews and invalid arguments never touch the working directory", (t) => {
  const folder = fixture(t);
  for (const [args, status, message] of [
    [["--dry-run"], 0, /PREVIEW ONLY/],
    [["--help"], 0, /Usage:/],
    [["--unexpected"], 1, /FAIL: Unexpected arguments/],
  ]) {
    const result = run(
      ".template/maintenance/run-checkups.cjs",
      folder,
      ...args,
    );
    assert.equal(result.status, status, result.stdout + result.stderr);
    assert.match(result.stdout + result.stderr, message);
    assert.deepEqual(fs.readdirSync(folder), []);
  }
});

for (const launcher of ["moorkyz", "tools.moorkyz.com"]) {
  test(`${launcher} selects the canonical audit template without rewriting historical evidence`, (t) => {
    const folder = fixture(t);
    const docs = path.join(folder, "docs");
    const legacy = path.join(docs, "audits");
    fs.mkdirSync(legacy, { recursive: true });
    const template = "# {{TASK_NAME}}\n{{DATE_UTC}}\n{{AUDIT_ID}}\n";
    fs.writeFileSync(path.join(docs, "AUDIT_TEMPLATE.md"), template);
    fs.writeFileSync(path.join(legacy, "TEMPLATE.md"), "legacy template");
    fs.writeFileSync(path.join(legacy, "historical.md"), "preserved bytes");
    const title = "$& and {{DATE_UTC}}";
    const result = run(`${launcher}.cjs`, folder, "audit", title);
    assert.equal(result.status, 0, result.stdout + result.stderr);
    const reports = fs.readdirSync(path.join(folder, ".audits"));
    assert.equal(reports.length, 1);
    const report = fs.readFileSync(
      path.join(folder, ".audits", reports[0]),
      "utf8",
    );
    assert.ok(report.startsWith(`# ${title}\n`));
    assert.match(report, /\d{4}-\d\d-\d\dT/);
    assert.equal(
      fs.readFileSync(path.join(docs, "AUDIT_TEMPLATE.md"), "utf8"),
      template,
    );
    assert.deepEqual(fs.readdirSync(legacy).sort(), [
      "TEMPLATE.md",
      "historical.md",
    ]);
    assert.equal(
      fs.readFileSync(path.join(legacy, "historical.md"), "utf8"),
      "preserved bytes",
    );
  });

  test(`${launcher} rejects an audit-directory junction without writing outside the fixture`, (t) => {
    const folder = fixture(t);
    const outside = fixture(t);
    fs.mkdirSync(path.join(folder, "docs"));
    fs.writeFileSync(
      path.join(folder, "docs", "AUDIT_TEMPLATE.md"),
      "# {{TASK_NAME}}",
    );
    fs.symlinkSync(
      outside,
      path.join(folder, ".audits"),
      process.platform === "win32" ? "junction" : "dir",
    );
    const result = run(`${launcher}.cjs`, folder, "audit", "test");
    assert.equal(result.status, 1, result.stdout + result.stderr);
    assert.match(result.stderr, /symlink or junction/);
    assert.deepEqual(fs.readdirSync(outside), []);
  });
}

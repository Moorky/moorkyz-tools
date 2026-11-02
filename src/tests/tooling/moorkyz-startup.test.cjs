"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { spawnSync } = require("node:child_process");
const { test } = require("node:test");
const {
  runStartup,
  listTasks,
  inspectDependencies,
} = require("../../.moorkyz/scripts/helpers/startup.cjs");

const root = path.resolve(__dirname, "..", "..", "..");
const managerInvocation = (_manager, args) => ({
  command: "pnpm-fixture",
  argv: args,
});
function fixture(t) {
  const folder = fs.mkdtempSync(path.join(os.tmpdir(), "moorkyz-checklist-"));
  t.after(() => fs.rmSync(folder, { recursive: true, force: true }));
  for (const relative of [
    "moorkyz",
    "RUN.cmd",
    "AGENTS.md",
    "src/package.json",
    "src/pnpm-lock.yaml",
    "src/pnpm-workspace.yaml",
    "src/packages/portal/package.json",
    "src/.moorkyz/config/moorkyz.config.json",
    "src/.moorkyz/scripts/moorkyz.cjs",
    "src/.moorkyz/scripts/helpers/startup.cjs",
    "src/.moorkyz/gui/start.ps1",
    "src/.moorkyz/gui/MoorkyzGui.cs",
    "src/.moorkyz/gui/CommandJournal.cs",
    "src/.moorkyz/gui/TerminalSearch.cs",
    "src/.moorkyz/gui/ReportView.cs",
    "src/.moorkyz/app/launch.ps1",
    "src/.moorkyz/gui/Motion.cs",
    "src/.moorkyz/gui/MainWindow.xaml",
    "src/.moorkyz/gui/Theme.xaml",
    "src/.moorkyz/public/favicon.ico",
    "src/.moorkyz/public/android-chrome-192x192.png",
  ]) {
    const file = path.join(folder, relative);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, "{}");
  }
  for (const relative of [
    "src/package.json",
    "src/packages/portal/package.json",
  ])
    fs.copyFileSync(path.join(root, relative), path.join(folder, relative));
  const messages = [];
  const calls = [];
  const options = {
    root: folder,
    managerInvocation,
    output: (message) => messages.push(message),
    execute: (command, args) => {
      calls.push([command, args]);
      return {
        status: 0,
        stdout: args[0] === "--version" ? "11.8.0" : "",
        stderr: "",
      };
    },
  };
  const entries = (id) =>
    fs
      .readFileSync(
        path.join(folder, ".moorkyz", "tasks", `startup-${id}.jsonl`),
        "utf8",
      )
      .trim()
      .split("\n")
      .map(JSON.parse);
  return { folder, options, messages, calls, entries };
}

test("startup shows one ordered checklist and the declared stack without installing healthy dependencies", (t) => {
  const { folder, options, messages, calls } = fixture(t);
  assert.equal(runStartup(options), 0);
  assert.deepEqual(
    messages.filter((line) => /^\d\./.test(line)).map((line) => line[0]),
    ["1", "2", "3"],
  );
  assert.ok(
    messages.some((line) =>
      /Next.js 16.4.1.*React \^19.3.1.*TypeScript \^6.0.3/.test(line),
    ),
  );
  assert.equal(calls.filter(([, args]) => args[0] === "install").length, 0);
  assert.equal(fs.existsSync(path.join(folder, ".moorkyz")), false);
});

test("failed repairs stop after two attempts and each later failed pair appends another linked task entry", (t) => {
  const { options, calls, entries, folder } = fixture(t);
  const execute = options.execute;
  options.execute = (command, args, settings) => {
    if (args[0] === "install" || args[0] === "run") {
      calls.push([command, args]);
      return {
        status: args[0] === "run" ? 42 : 1,
        stderr:
          "https://user:pass@example.invalid/?token=secret token=private-value",
      };
    }
    return execute(command, args, settings);
  };
  assert.equal(runStartup(options), 1);
  assert.equal(calls.filter(([, args]) => args[0] === "install").length, 2);
  let reports = entries("dependencies");
  assert.equal(reports.length, 1);
  assert.equal(reports[0].attempts.length, 2);
  assert.ok(reports[0].fixSuggestion.includes("--frozen-lockfile"));
  assert.equal(reports[0].status, "open");
  assert.doesNotMatch(
    JSON.stringify(reports),
    /private-value|user:pass|token=secret/,
  );
  assert.equal(runStartup(options), 1);
  reports = entries("dependencies");
  assert.equal(reports.length, 2);
  assert.equal(reports[1].previousEntry, reports[0].id);
  assert.equal(calls.filter(([, args]) => args[0] === "install").length, 4);
  options.execute = execute;
  assert.equal(runStartup({ ...options, only: "dependencies" }), 0);
  reports = entries("dependencies");
  assert.equal(reports.length, 3);
  assert.equal(reports[2].status, "resolved");
  const listed = [];
  listTasks(folder, (line) => listed.push(line));
  assert.match(
    listed.join("\n"),
    /dependencies: resolved \(3 history entries\)/,
  );
});

test("a failed first install can recover on the second attempt, then packages are rechecked", (t) => {
  const { options } = fixture(t);
  const execute = options.execute;
  let installs = 0;
  let probes = 0;
  options.execute = (command, args, settings) => {
    if (args[0] === "run") {
      probes++;
      return { status: installs >= 2 ? 0 : 42 };
    }
    if (args[0] === "install") {
      assert.deepEqual(args, ["install", "--frozen-lockfile"]);
      installs++;
      return { status: installs === 1 ? 1 : 0 };
    }
    return execute(command, args, settings);
  };
  assert.equal(runStartup(options), 0);
  assert.equal(installs, 2);
  assert.equal(probes, 3);
});

test("check-only mode reports twice-failed missing dependencies without any install", (t) => {
  const { options, calls, entries } = fixture(t);
  const execute = options.execute;
  options.execute = (command, args, settings) => {
    if (args[0] === "run") {
      calls.push([command, args]);
      return { status: 1, stderr: "ERR_PNPM_VERIFY_DEPS_BEFORE_RUN" };
    }
    return execute(command, args, settings);
  };
  assert.equal(runStartup({ ...options, checkOnly: true }), 1);
  assert.equal(calls.filter(([, args]) => args[0] === "install").length, 0);
  assert.equal(calls.filter(([, args]) => args[0] === "run").length, 2);
  assert.equal(entries("dependencies")[0].attempts.length, 2);
});

test("non-dependency errors and mismatched toolchain pins never trigger installation", (t) => {
  const { options, calls, entries } = fixture(t);
  const execute = options.execute;
  options.execute = (command, args, settings) => {
    if (args[0] === "--version") {
      calls.push([command, args]);
      return { status: 0, stdout: "99.0.0" };
    }
    return execute(command, args, settings);
  };
  assert.equal(runStartup(options), 1);
  assert.match(entries("stack")[0].attempts[0].error, /Expected pnpm@11.8.0/);
  assert.equal(
    calls.some(([, args]) => args[0] === "install"),
    false,
  );
  options.execute = (command, args, settings) =>
    args[0] === "run"
      ? { status: 1, stderr: "Unexpected script error" }
      : execute(command, args, settings);
  assert.equal(runStartup(options), 1);
  assert.equal(
    calls.some(([, args]) => args[0] === "install"),
    false,
  );
});

test("missing source produces an actionable task but never fabricates files", (t) => {
  const { folder, options, entries, calls } = fixture(t);
  fs.unlinkSync(path.join(folder, "RUN.cmd"));
  assert.equal(runStartup(options), 1);
  assert.equal(calls.length, 0);
  assert.equal(entries("files")[0].attempts.length, 2);
  assert.equal(fs.existsSync(path.join(folder, "RUN.cmd")), false);
  assert.throws(
    () => runStartup({ ...options, only: "../escape" }),
    /Unknown startup task/,
  );
});

test("task storage refuses a junction and never writes an outside report", (t) => {
  const { folder, options } = fixture(t);
  const outside = fs.mkdtempSync(
    path.join(os.tmpdir(), "moorkyz-task-outside-"),
  );
  t.after(() => fs.rmSync(outside, { recursive: true, force: true }));
  fs.symlinkSync(
    outside,
    path.join(folder, ".moorkyz"),
    process.platform === "win32" ? "junction" : "dir",
  );
  assert.throws(() => runStartup(options), /symlink or junction/);
  assert.deepEqual(fs.readdirSync(outside), []);
});

test("dependency probe checks all declared packages including scoped packages and exact pins", (t) => {
  const { folder } = fixture(t);
  for (const relative of [
    "src/package.json",
    "src/packages/portal/package.json",
  ])
    fs.writeFileSync(
      path.join(folder, relative),
      JSON.stringify({ dependencies: { "@fixture/package": "1.2.3" } }),
    );
  assert.throws(() => inspectDependencies(folder), /Missing or mismatched/);
  for (const workspace of ["src", "src/packages/portal"]) {
    const destination = path.join(
      folder,
      workspace,
      "node_modules",
      "@fixture",
      "package",
    );
    fs.mkdirSync(destination, { recursive: true });
    fs.writeFileSync(
      path.join(destination, "package.json"),
      JSON.stringify({ name: "@fixture/package", version: "1.2.3" }),
    );
  }
  assert.doesNotThrow(() => inspectDependencies(folder));
  const packageFile = path.join(
    folder,
    "src",
    "node_modules",
    "@fixture",
    "package",
    "package.json",
  );
  fs.writeFileSync(
    packageFile,
    JSON.stringify({ version: "1.2.3", bin: "missing.cjs" }),
  );
  assert.throws(
    () => inspectDependencies(folder),
    /missing package entrypoint/,
  );
  fs.writeFileSync(
    path.join(
      folder,
      "src",
      "node_modules",
      "@fixture",
      "package",
      "package.json",
    ),
    JSON.stringify({ version: "1.2.2" }),
  );
  assert.throws(
    () => inspectDependencies(folder),
    /@fixture\/package \(1.2.3\)/,
  );
});

test(
  "bare moorkyz and RUN enter the same checklist without starting a service in a noninteractive session",
  { timeout: 120000 },
  () => {
    const run = (command, args) =>
      spawnSync(command, args, {
        cwd: root,
        env: { ...process.env, MOORKYZ_HEADLESS: "1" },
        encoding: "utf8",
        timeout: 45000,
      });
    const direct = spawnSync(process.execPath, [path.join(root, "moorkyz")], {
      cwd: os.tmpdir(),
      env: { ...process.env, MOORKYZ_HEADLESS: "1" },
      encoding: "utf8",
      timeout: 45000,
    });
    assert.equal(direct.status, 0, direct.stdout + direct.stderr);
    assert.match(direct.stdout, /1\. Check required files/);
    assert.match(direct.stdout, /0\. Exit/);
    assert.match(direct.stdout, /nothing was started/);
    if (process.platform === "win32") {
      const batch = run(process.env.ComSpec, ["/d", "/s", "/c", "RUN.cmd"]);
      assert.equal(batch.status, 0, batch.stdout + batch.stderr);
      const steps = (text) =>
        text
          .split(/\r?\n/)
          .filter((line) => /^\d\. Check/.test(line))
          .map((line) => line.replace(/ \(check only; no installation\)/, ""));
      assert.deepEqual(steps(batch.stdout), steps(direct.stdout));
      assert.equal(
        batch.stdout.slice(
          batch.stdout.indexOf("Moorkyz - local project actions"),
        ),
        direct.stdout.slice(
          direct.stdout.indexOf("Moorkyz - local project actions"),
        ),
      );
    }
  },
);

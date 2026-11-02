"use strict";

const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const { spawnSync } = require("node:child_process");
const readline = require("node:readline/promises");
const { scopedPath } = require("./storage.cjs");

const definitions = [
  {
    id: "files",
    title: "Check required files and repository structure",
    fix: "Restore the reported tracked file from a trusted checkout, or correct its documented path. Do not generate replacement source or remove personal files.",
    verify: "node moorkyz launch --check",
  },
  {
    id: "stack",
    title: "Check the declared tech stack and installed toolchain",
    fix: "Install/activate the Node and pnpm versions declared in src/package.json explicitly, then reopen the terminal. Machine-wide prerequisites are never auto-installed.",
    verify: "node moorkyz launch --check",
  },
  {
    id: "dependencies",
    title: "Check workspace packages; repair missing dependencies",
    fix: "Run pnpm --dir src install --frozen-lockfile. Check the registry/network error or reviewed manifest/lockfile mismatch; never delete the lockfile or bypass its checks.",
    verify: "node moorkyz launch --check",
  },
];

function clean(value) {
  return String(value)
    .replace(/https?:\/\/[^\s"'<>]+/gi, "[URL redacted]")
    .replace(/\b(Bearer\s+)\S+/gi, "$1[redacted]")
    .replace(
      /\b(token|password|secret|authorization|cookie|api[_-]?key|_authToken|_auth)["']?\s*[:=]\s*(?:"[^"]*"|'[^']*'|\S+)/gi,
      "$1=[redacted]",
    )
    .slice(-12000);
}

function taskFile(root, id) {
  if (!definitions.some((definition) => definition.id === id))
    throw new Error(`Unknown startup task '${id}'.`);
  return scopedPath(
    root,
    path.join(".moorkyz", "tasks", `startup-${id}.jsonl`),
    true,
  );
}

function history(root, id) {
  const file = taskFile(root, id);
  if (!fs.existsSync(file)) return [];
  return fs
    .readFileSync(file, "utf8")
    .trim()
    .split(/\r?\n/)
    .filter(Boolean)
    .map((line) => JSON.parse(line));
}

function record(root, definition, status, attempts, stack) {
  const file = taskFile(root, definition.id);
  if (status === "resolved" && !fs.existsSync(file)) return null;
  const entries = history(root, definition.id);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const entry = {
    id: crypto.randomUUID(),
    task: `startup-${definition.id}`,
    title: definition.title,
    status,
    createdAt: new Date().toISOString(),
    previousEntry: entries.at(-1)?.id ?? null,
    attempts,
    fixSuggestion: definition.fix,
    verify: definition.verify,
    stack,
  };
  fs.appendFileSync(file, `${JSON.stringify(entry)}\n`, { mode: 0o600 });
  return file;
}

function listTasks(root, output = console.log) {
  let count = 0;
  for (const definition of definitions) {
    const entries = history(root, definition.id);
    if (!entries.length) continue;
    count++;
    const latest = entries.at(-1);
    output(
      `${definition.id}: ${latest.status} (${entries.length} history entries)`,
    );
    output(`  Suggested fix: ${latest.fixSuggestion}`);
    output(`  Retry: node moorkyz tasks retry ${definition.id}`);
  }
  if (!count) output("No startup error tasks.");
}

function inspectDependencies(root) {
  const workspace = path.join(root, "src");
  const folders = [
    workspace,
    ...fs
      .readdirSync(path.join(workspace, "packages"), { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .map((entry) => path.join(workspace, "packages", entry.name))
      .filter((folder) => fs.existsSync(path.join(folder, "package.json"))),
  ];
  const missing = [];
  for (const folder of folders) {
    const manifest = JSON.parse(
      fs.readFileSync(path.join(folder, "package.json"), "utf8"),
    );
    for (const [name, wanted] of Object.entries({
      ...manifest.dependencies,
      ...manifest.devDependencies,
    })) {
      if (!/^(?:@[a-z0-9._-]+\/)?[a-z0-9._-]+$/i.test(name))
        throw new Error(`Invalid package name in ${folder}: ${name}`);
      const file = path.join(
        folder,
        "node_modules",
        ...name.split("/"),
        "package.json",
      );
      try {
        const installed = JSON.parse(fs.readFileSync(file, "utf8"));
        if (
          typeof installed.version !== "string" ||
          (/^\d+\.\d+\.\d+$/.test(wanted) && installed.version !== wanted)
        )
          missing.push(`${name} (${wanted})`);
        const binaries =
          typeof installed.bin === "string"
            ? [installed.bin]
            : Object.values(installed.bin ?? {});
        const entries = [
          ...binaries,
          installed.main,
          installed.types ?? installed.typings,
        ].filter((entry) => typeof entry === "string");
        if (
          entries.some(
            (entry) => !fs.existsSync(path.resolve(path.dirname(file), entry)),
          )
        )
          missing.push(`${name} (missing package entrypoint)`);
      } catch (error) {
        if (error.code !== "ENOENT") throw error;
        missing.push(`${name} (${wanted})`);
      }
    }
  }
  if (missing.length) {
    const error = new Error(
      `Missing or mismatched workspace packages: ${missing.join(", ")}`,
    );
    error.code = "MISSING_DEPENDENCIES";
    throw error;
  }
}

function stackFor(root) {
  const workspace = JSON.parse(
    fs.readFileSync(path.join(root, "src", "package.json"), "utf8"),
  );
  const portal = JSON.parse(
    fs.readFileSync(
      path.join(root, "src", "packages", "portal", "package.json"),
      "utf8",
    ),
  );
  return {
    node: workspace.engines.node,
    packageManager: workspace.packageManager,
    next: portal.dependencies.next,
    react: portal.dependencies.react,
    reactDom: portal.dependencies["react-dom"],
    typescript: workspace.devDependencies.typescript,
  };
}

function runStartup({
  root,
  managerInvocation,
  checkOnly = false,
  only,
  execute = spawnSync,
  output = console.log,
}) {
  if (only && !definitions.some((definition) => definition.id === only))
    throw new Error(`Unknown startup task '${only}'.`);
  let stack = null;
  function run(command, args, cwd = root, timeout = 120000) {
    const result = execute(command, args, {
      cwd,
      encoding: "utf8",
      timeout,
      maxBuffer: 1024 * 1024,
      windowsHide: true,
      env: { ...process.env, CI: "1", NEXT_TELEMETRY_DISABLED: "1" },
    });
    const text = clean(`${result.stdout ?? ""}\n${result.stderr ?? ""}`);
    if (result.error || result.status !== 0) {
      const error = new Error(
        `${path.basename(command)} ${args.join(" ")}: ${result.error?.message ?? `exit ${result.status}`}\n${text}`,
      );
      error.repairable =
        result.status === 42 ||
        text.includes("ERR_PNPM_VERIFY_DEPS_BEFORE_RUN");
      throw error;
    }
    return text.trim();
  }
  function pnpm(args, timeout) {
    const invocation = managerInvocation("pnpm", args);
    return run(
      invocation.command,
      invocation.argv,
      path.join(root, "src"),
      timeout,
    );
  }
  for (const [index, definition] of definitions.entries()) {
    if (only && index > definitions.findIndex((entry) => entry.id === only))
      continue;
    output(
      `${index + 1}. ${definition.title}${checkOnly && definition.id === "dependencies" ? " (check only; no installation)" : ""}`,
    );
    const attempts = [];
    for (let attempt = 1; attempt <= 2; attempt++) {
      try {
        if (definition.id === "files") {
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
            const file = scopedPath(root, relative);
            if (!fs.statSync(file).isFile())
              throw new Error(`Required regular file: ${relative}`);
          }
          JSON.parse(
            fs.readFileSync(
              path.join(
                root,
                "src",
                ".moorkyz",
                "config",
                "moorkyz.config.json",
              ),
              "utf8",
            ),
          );
          run(process.execPath, ["src/.template/maintenance/run-checkups.cjs"]);
          run(process.execPath, [
            "--test",
            "src/tests/tooling/repository-structure.test.cjs",
          ]);
        } else if (definition.id === "stack") {
          stack = stackFor(root);
          if (!/^>=\d+\.\d+\.\d+$/.test(stack.node))
            throw new Error(
              `Unsupported Node engine expression: ${stack.node}`,
            );
          const minimum = stack.node.slice(2).split(".").map(Number);
          const actual = process.versions.node.split(".").map(Number);
          const difference =
            actual
              .map((part, i) => part - minimum[i])
              .find((part) => part !== 0) ?? 0;
          if (difference < 0)
            throw new Error(
              `Node ${stack.node} required; installed ${process.version}`,
            );
          if (!/^pnpm@\d+\.\d+\.\d+$/.test(stack.packageManager))
            throw new Error(
              `Unsupported package manager pin: ${stack.packageManager}`,
            );
          const installed = pnpm(["--version"]);
          if (installed !== stack.packageManager.slice(5))
            throw new Error(
              `Expected ${stack.packageManager}; installed pnpm ${installed}`,
            );
          output(
            `   Stack: Node ${stack.node}, ${stack.packageManager}, Next.js ${stack.next}, React ${stack.react}, React DOM ${stack.reactDom}, TypeScript ${stack.typescript}`,
          );
        } else {
          if (!stack) stack = stackFor(root);
          try {
            pnpm(["run", "startup:dependencies"]);
          } catch (error) {
            if (checkOnly || !error.repairable) throw error;
            output(
              `   Repair ${attempt}/2: pnpm install --frozen-lockfile (workspace only).`,
            );
            pnpm(["install", "--frozen-lockfile"], 0);
            pnpm(["run", "startup:dependencies"]);
          }
        }
        attempts.push({ attempt, result: "PASS" });
        record(root, definition, "resolved", attempts, stack);
        output("   PASS");
        break;
      } catch (error) {
        attempts.push({ attempt, result: "FAIL", error: clean(error.message) });
        output(`   FAIL ${attempt}/2: ${clean(error.message)}`);
        if (attempt === 1) {
          output("   Retrying once.");
          continue;
        }
        const report = record(root, definition, "open", attempts, stack);
        output(`   Error task: ${report}`);
        output(`   Suggested fix: ${definition.fix}`);
        return 1;
      }
    }
  }
  return 0;
}

async function launch({ root, entrypoint, managerInvocation, args }) {
  if (args.some((arg) => arg !== "--check") || args.length > 1)
    throw new Error("Usage: moorkyz launch [--check]");
  const status = runStartup({
    root,
    managerInvocation,
    checkOnly: args.includes("--check"),
  });
  if (status !== 0 || args.includes("--check")) return status;
  console.log("\nMoorkyz - local project actions");
  const actions = [
    ["Open control GUI", "gui"],
    ["Start portal", "start"],
    ["Status", "status"],
    ["Stop portal", "stop"],
    ["Error tasks", "tasks"],
    ["Help", "help"],
  ];
  actions.forEach(([label], index) => console.log(`${index + 1}. ${label}`));
  console.log("0. Exit");
  if (!process.stdin.isTTY || !process.stdout.isTTY) {
    console.log(
      "Non-interactive session: no action selected; nothing was started.",
    );
    return 0;
  }
  const terminal = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
  });
  try {
    const choice = (await terminal.question("Select an action: ")).trim();
    if (choice === "0") return 0;
    if (!/^[1-6]$/.test(choice))
      throw new Error("Choose a numbered action from 0 to 6.");
    const result = spawnSync(
      process.execPath,
      [entrypoint, actions[Number(choice) - 1][1]],
      {
        cwd: root,
        stdio: "inherit",
      },
    );
    if (result.error) throw result.error;
    return result.status ?? 1;
  } finally {
    terminal.close();
  }
}

module.exports = { runStartup, launch, listTasks, inspectDependencies };

if (require.main === module) {
  if (process.argv.length !== 3 || process.argv[2] !== "--probe") {
    console.error("Usage: startup.cjs --probe (from the src workspace)");
    process.exitCode = 1;
  } else {
    try {
      inspectDependencies(path.resolve(process.cwd(), ".."));
      console.log("PASS: declared workspace packages are installed.");
    } catch (error) {
      console.error(error.message);
      process.exitCode = error.code === "MISSING_DEPENDENCIES" ? 42 : 1;
    }
  }
}

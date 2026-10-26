#!/usr/bin/env node
"use strict";

// Moorkyz project development CLI. Built from Node.js core modules only.
// Run from the intended project root; never manipulates other projects' processes.
const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const net = require("node:net");
const { spawn, spawnSync } = require("node:child_process");
const storage = require("./helpers/storage.cjs");

const ROOT = process.cwd();
const ENTRYPOINT = path.resolve(process.argv[1]);
const COMMAND =
  path.basename(ENTRYPOINT).toLowerCase() === "tools.moorkyz.com.cjs"
    ? "tools.moorkyz.com"
    : "moorkyz";
const STATE_DIRECTORY = `.${COMMAND}`;
const CONFIG_FILE = `${COMMAND}.config.json`;
const STATE = path.join(ROOT, STATE_DIRECTORY);
const MANAGED_LOGS = COMMAND === "moorkyz";
const DEFAULT = "default";
const DEFAULT_URL = "http://localhost:3000";
const NAME_RE = /^[a-zA-Z][a-zA-Z0-9_-]{0,50}$/;
const SCRIPT_RE = /^[a-zA-Z0-9][a-zA-Z0-9:_./-]*$/;
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const scriptCache = new Map();
const managerVersions = new Map();
const managerCache = new Map();

function die(message, code = 1) {
  console.error(`Error: ${message}`);
  process.exit(code);
}
function info(message) {
  console.log(message);
}
function openBrowser(url) {
  if (process.env.CI || !process.stdout.isTTY) return;
  const command =
    process.platform === "win32"
      ? "explorer.exe"
      : process.platform === "darwin"
        ? "open"
        : "xdg-open";
  const browser = spawn(command, [url], {
    detached: true,
    stdio: "ignore",
    windowsHide: true,
  });
  browser.on("error", (error) => {
    info(`Could not open ${url} in the default browser: ${error.message}`);
  });
  browser.unref();
  info(`Opening ${url} in the default browser.`);
}
// Windows resolves package managers to .cmd shims, which require a shell.
// Spawn cmd.exe explicitly instead of using shell: true, which concatenates arguments (DEP0190).
function managerInvocation(manager, argv) {
  return process.platform === "win32"
    ? {
        command: process.env.ComSpec || "cmd.exe",
        argv: ["/d", "/s", "/c", manager, ...argv],
      }
    : { command: manager, argv };
}

function managerFor(project) {
  if (managerCache.has(project.cwd)) return managerCache.get(project.cwd);
  const folders = [];
  let current = project.cwd;
  while (true) {
    folders.push(current);
    if (current === ROOT || current === path.dirname(current)) break;
    current = path.dirname(current);
  }
  for (const folder of folders) {
    const manifest = path.join(folder, "package.json");
    if (!exists(manifest)) continue;
    const declared = readJson(manifest).packageManager;
    if (declared != null) {
      if (typeof declared !== "string" || !/^(pnpm|npm|yarn)@\d/.test(declared))
        die(
          `Unsupported packageManager in ${path.relative(ROOT, manifest)}. Supported package managers: pnpm, npm and yarn.`,
        );
      const manager = { name: declared.split("@")[0], declared, folder };
      managerCache.set(project.cwd, manager);
      return manager;
    }
  }
  for (const folder of folders) {
    const matches = [
      ["pnpm", "pnpm-lock.yaml"],
      ["npm", "package-lock.json"],
      ["yarn", "yarn.lock"],
    ].filter(([, filename]) => exists(path.join(folder, filename)));
    if (matches.length > 1)
      die(
        `Multiple package-manager lockfiles in ${path.relative(ROOT, folder) || "."}. Set packageManager in package.json to choose the authoritative tool.`,
      );
    if (matches.length === 1) {
      const manager = { name: matches[0][0], folder };
      managerCache.set(project.cwd, manager);
      return manager;
    }
  }
  const manager = { name: "pnpm", folder: project.cwd };
  managerCache.set(project.cwd, manager);
  return manager;
}

function scriptInvocation(project, script, extra = []) {
  const manager = managerFor(project).name;
  return managerInvocation(manager, [
    "run",
    script,
    ...(manager === "npm" && extra.length ? ["--"] : []),
    ...extra,
  ]);
}
function exists(file) {
  return fs.existsSync(file);
}
function readJson(file) {
  try {
    return JSON.parse(fs.readFileSync(file, "utf8"));
  } catch (e) {
    die(
      `Cannot read valid JSON from ${path.relative(ROOT, file) || file}: ${e.message}`,
    );
  }
}
function safeProjectName(name) {
  if (!NAME_RE.test(name))
    die(`Invalid project name '${name}'. Use letters, digits, _ and - only.`);
  return name;
}
function projectUrl(value, name) {
  if (value == null) return DEFAULT_URL;
  if (typeof value !== "string" || !value.trim())
    die(`Invalid URL for '${name}'.`);
  try {
    const url = new URL(value);
    if (!["http:", "https:"].includes(url.protocol))
      throw new Error("unsupported protocol");
    if (url.username || url.password)
      throw new Error("credentials are not allowed in project URLs");
    return url.toString().replace(/\/$/, "");
  } catch {
    die(`Invalid URL for '${name}'. Use an http or https URL.`);
  }
}
function portHosts(url) {
  if (url.hostname === "localhost") return ["127.0.0.1", "::1"];
  const hostname = url.hostname.replace(/^\[|\]$/g, "");
  return net.isIP(hostname) ? [hostname] : ["127.0.0.1", "::1"];
}
function canListen(port, host) {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once("error", (error) => {
      if (["EADDRINUSE", "EACCES"].includes(error.code)) resolve(false);
      else if (["EAFNOSUPPORT", "EADDRNOTAVAIL"].includes(error.code))
        resolve(true);
      else reject(error);
    });
    server.listen({ port, host, exclusive: true }, () =>
      server.close((error) => (error ? reject(error) : resolve(true))),
    );
  });
}
async function findAvailablePort(
  project,
  excluded = [],
  names = [project.name],
) {
  const url = new URL(project.url);
  const basePort = Number(url.port || (url.protocol === "https:" ? 443 : 80));
  if (!Number.isInteger(basePort) || basePort < 1 || basePort > 65535)
    die(`Invalid starting port for '${project.name}'.`);
  const hosts = portHosts(url);
  const preference = readPreferences(project.name);
  const seed = crypto
    .createHash("sha256")
    .update(`${fs.realpathSync(ROOT)}:${project.name}`)
    .digest()
    .readUInt32BE();
  const candidates = [
    preference.configuredUrl === project.url ? preference.port : null,
    basePort,
    10000 + (seed % 39152),
    ...Array.from({ length: 64 }, () => crypto.randomInt(10000, 49152)),
  ];
  const reserved = new Set(excluded);
  for (const name of names) {
    if (name === project.name) continue;
    const state = readState(name);
    if (state && alive(state.pid)) reserved.add(state.port);
  }
  for (const port of new Set(candidates)) {
    if (
      !Number.isInteger(port) ||
      port < 1 ||
      port > 65535 ||
      reserved.has(port)
    )
      continue;
    const available = await Promise.all(
      hosts.map((host) => canListen(port, host)),
    );
    if (available.every(Boolean)) return port;
  }
  die(
    `No available local port found for '${project.name}' after up to 67 probes. Inspect firewall/reserved-port settings and retry.`,
  );
}
function urlForPort(value, port) {
  const url = new URL(value);
  url.port = String(port);
  return url.toString().replace(/\/$/, "");
}
function runtimeUrl(project, entry) {
  if (entry?.url) return entry.url;
  if (Number.isInteger(entry?.port)) return urlForPort(project.url, entry.port);
  const preference = readPreferences(project.name);
  if (
    preference.configuredUrl === project.url &&
    Number.isInteger(preference.port)
  )
    return urlForPort(project.url, preference.port);
  return project.url;
}
function folderFor(input) {
  if (typeof input !== "string") die("Configured cwd must be a string.");
  const target = path.resolve(ROOT, input);
  const rel = path.relative(ROOT, target);
  if (rel === ".." || rel.startsWith(".." + path.sep) || path.isAbsolute(rel))
    die(`Configured project path escapes repository: ${input}`);
  if (!exists(target) || !fs.statSync(target).isDirectory())
    die(`Project folder does not exist: ${input}`);
  // Resolve symlinks to avoid allowing a config path to escape through a symlink.
  const actual = fs.realpathSync(target);
  const rootActual = fs.realpathSync(ROOT);
  const actualRel = path.relative(rootActual, actual);
  if (
    actualRel === ".." ||
    actualRel.startsWith(".." + path.sep) ||
    path.isAbsolute(actualRel)
  )
    die(`Project symlink escapes repository: ${input}`);
  return target;
}
function cfg() {
  if (exists(STATE)) folderFor(STATE_DIRECTORY);
  if (exists(STATE)) storage.scopedPath(ROOT, STATE_DIRECTORY);
  const filename = path.join(ROOT, CONFIG_FILE);
  const data = exists(filename) ? readJson(filename) : {};
  if (!data || typeof data !== "object" || Array.isArray(data))
    die(`${CONFIG_FILE} must contain a JSON object.`);
  if (
    data.projects != null &&
    (typeof data.projects !== "object" || Array.isArray(data.projects))
  )
    die("projects must be an object.");
  const raw = data.projects ?? {
    [DEFAULT]: { cwd: ".", dev: "dev", prod: "start" },
  };
  const projects = Object.create(null);
  for (const [name, settings] of Object.entries(raw)) {
    safeProjectName(name);
    if (!settings || typeof settings !== "object" || Array.isArray(settings))
      die(`Configuration for '${name}' must be an object.`);
    const cwd = folderFor(settings.cwd ?? ".");
    const dev = settings.dev ?? "dev";
    const prod = settings.prod ?? "start";
    if (![dev, prod].every((s) => typeof s === "string" && SCRIPT_RE.test(s)))
      die(`Invalid script name for '${name}'.`);
    const portArgs = settings.portArgs;
    if (
      portArgs != null &&
      (!Array.isArray(portArgs) ||
        !portArgs.every(
          (argument) =>
            typeof argument === "string" &&
            /^[a-zA-Z0-9_{}=:/.-]+$/.test(argument),
        ))
    )
      die(
        `portArgs for '${name}' must contain safe CLI arguments; use {port} as the port placeholder.`,
      );
    const startupTimeoutMs = settings.startupTimeoutMs ?? 20000;
    if (
      !Number.isInteger(startupTimeoutMs) ||
      startupTimeoutMs < 1000 ||
      startupTimeoutMs > 300000
    )
      die(
        `startupTimeoutMs for '${name}' must be an integer from 1000 to 300000.`,
      );
    projects[name] = {
      name,
      cwd,
      dev,
      prod,
      url: projectUrl(settings.url, name),
      portArgs,
      startupTimeoutMs,
    };
  }
  const defaultProject =
    data.defaultProject ??
    (Object.keys(projects).length === 1 ? Object.keys(projects)[0] : null);
  if (
    defaultProject !== null &&
    (typeof defaultProject !== "string" ||
      !Object.hasOwn(projects, defaultProject))
  )
    die("Configured defaultProject must name an existing project.");
  return {
    projects,
    defaultProject,
    storage: storage.storagePolicy(data.storage),
  };
}
function targetNames(config, raw, allowAll) {
  const name = raw || config.defaultProject;
  if (!name)
    die(
      `Choose a project name or "all" (or set defaultProject in ${CONFIG_FILE}).`,
    );
  if (name === "all") {
    if (!allowAll) die("This command requires a single project name.");
    return Object.keys(config.projects);
  }
  if (!Object.hasOwn(config.projects, name))
    die(`Unknown project '${name}'. Run '${COMMAND} list'.`);
  return [name];
}
function statePath(name) {
  return path.join(STATE, `${name}.json`);
}
function heartbeatPath(name) {
  return path.join(STATE, `${name}.heartbeat.json`);
}
function logPath(name) {
  if (!MANAGED_LOGS)
    return storage.scopedPath(
      ROOT,
      path.join(STATE_DIRECTORY, `${name}.log`),
      true,
    );
  const preference = readPreferences(name);
  if (
    preference.log &&
    typeof preference.log === "string" &&
    path.dirname(preference.log) === "logs" &&
    storage.validName(path.basename(preference.log), ".log")
  )
    return storage.scopedPath(ROOT, preference.log, true);
  // Read-only compatibility for logs from the previous CLI version.
  return storage.scopedPath(
    ROOT,
    path.join(STATE_DIRECTORY, `${name}.log`),
    true,
  );
}
function readPreferences(name) {
  const file = storage.scopedPath(
    ROOT,
    path.join(STATE_DIRECTORY, `${name}.preferences.json`),
    true,
  );
  if (!exists(file)) return {};
  const preference = readJson(file);
  if (
    !preference ||
    typeof preference !== "object" ||
    Array.isArray(preference) ||
    (preference.port != null &&
      (!Number.isInteger(preference.port) ||
        preference.port < 1 ||
        preference.port > 65535))
  )
    die(
      `Invalid saved preferences in ${path.relative(ROOT, file)}. Inspect this file before retrying.`,
    );
  return preference;
}
function writeJsonAtomic(file, value) {
  storage.scopedPath(ROOT, file, true);
  const temporary = storage.scopedPath(
    ROOT,
    `${file}.${process.pid}.${crypto.randomBytes(4).toString("hex")}.tmp`,
    true,
  );
  try {
    fs.writeFileSync(temporary, JSON.stringify(value, null, 2), {
      flag: "wx",
      mode: 0o600,
    });
    fs.renameSync(temporary, file);
  } finally {
    try {
      fs.unlinkSync(temporary);
    } catch {
      /* renamed or never created */
    }
  }
}
async function withProjectLock(name, operation) {
  fs.mkdirSync(STATE, { recursive: true });
  folderFor(STATE_DIRECTORY);
  storage.scopedPath(ROOT, STATE_DIRECTORY);
  const file = storage.scopedPath(
    ROOT,
    path.join(STATE, `${name}.lock.json`),
    true,
  );
  const token = crypto.randomBytes(16).toString("hex");
  const deadline = Date.now() + 5000;
  while (true) {
    try {
      fs.writeFileSync(file, JSON.stringify({ pid: process.pid, token }), {
        flag: "wx",
      });
      break;
    } catch (error) {
      if (error.code !== "EEXIST") throw error;
      // Do not automatically unlink stale locks: two reclaimers could remove
      // a new owner's lock between reading the old PID and deleting the file.
      if (Date.now() >= deadline)
        die(
          `Another Moorkyz operation holds the lock for '${name}'. Retry when it completes or review ${path.relative(ROOT, file)}.`,
        );
      await sleep(50);
    }
  }
  const release = () => {
    try {
      const owner = JSON.parse(fs.readFileSync(file, "utf8"));
      if (owner.token === token) fs.unlinkSync(file);
    } catch {
      /* already released */
    }
  };
  const interrupt = () => {
    release();
    process.exit(130);
  };
  const terminate = () => {
    release();
    process.exit(143);
  };
  process.once("exit", release);
  process.once("SIGINT", interrupt);
  process.once("SIGTERM", terminate);
  try {
    return await operation();
  } finally {
    release();
    process.removeListener("exit", release);
    process.removeListener("SIGINT", interrupt);
    process.removeListener("SIGTERM", terminate);
  }
}
function readState(name) {
  const file = storage.scopedPath(ROOT, statePath(name), true);
  if (!exists(file)) return null;
  try {
    const state = JSON.parse(fs.readFileSync(file, "utf8"));
    if (
      !state ||
      typeof state !== "object" ||
      Array.isArray(state) ||
      !/^[a-f0-9]{32}$/.test(state.token || "") ||
      (state.pid != null && (!Number.isSafeInteger(state.pid) || state.pid < 1))
    )
      throw new Error(
        "Expected an object containing a supervisor token and valid PID",
      );
    return state;
  } catch (error) {
    die(
      `Invalid tracked state ${path.relative(ROOT, file)}: ${error.message}. Inspect it before retrying; no process was stopped.`,
    );
  }
}
function clearState(name, token) {
  const entry = readState(name);
  if (entry && entry.token === token) {
    for (const f of [statePath(name), heartbeatPath(name)]) {
      try {
        fs.unlinkSync(f);
      } catch {
        /* already removed */
      }
    }
  }
}
function alive(pid) {
  if (!Number.isSafeInteger(pid) || pid < 1) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error.code === "EPERM";
  }
}
function freshHeartbeat(name, entry) {
  try {
    const beat = JSON.parse(
      fs.readFileSync(storage.scopedPath(ROOT, heartbeatPath(name)), "utf8"),
    );
    return (
      beat.token === entry.token &&
      beat.pid === entry.pid &&
      Date.now() - beat.at >= 0 &&
      Date.now() - beat.at < 12000
    );
  } catch {
    return false;
  }
}
function running(name) {
  const entry = readState(name);
  return entry && alive(entry.pid) && freshHeartbeat(name, entry)
    ? entry
    : null;
}
function packageScripts(project) {
  if (scriptCache.has(project.cwd)) return scriptCache.get(project.cwd);
  const file = path.join(project.cwd, "package.json");
  if (!exists(file))
    die(`No package.json in ${path.relative(ROOT, project.cwd) || "."}.`);
  const scripts = readJson(file).scripts ?? {};
  if (
    !scripts ||
    typeof scripts !== "object" ||
    Array.isArray(scripts) ||
    !Object.values(scripts).every((script) => typeof script === "string")
  )
    die("package.json scripts must be an object containing string commands.");
  scriptCache.set(project.cwd, scripts);
  return scripts;
}
function hasScript(scripts, name) {
  return (
    Object.hasOwn(scripts, name) &&
    typeof scripts[name] === "string" &&
    scripts[name].trim().length > 0
  );
}
function requireManager(project) {
  const manager = managerFor(project).name;
  if (managerVersions.has(manager)) return managerVersions.get(manager);
  const { command, argv } = managerInvocation(manager, ["--version"]);
  const check = spawnSync(command, argv, {
    cwd: project.cwd,
    encoding: "utf8",
    timeout: 5000,
  });
  if (check.error || check.status !== 0)
    die(
      `Cannot run ${manager}${check.error ? ` (${check.error.code || check.error.message})` : ` (exit ${check.status})`}. Install/activate the packageManager version in package.json, then run '${COMMAND} doctor'.`,
    );
  const version = (check.stdout || "").trim();
  managerVersions.set(manager, version);
  return version;
}
function runScript(script, project) {
  if (!SCRIPT_RE.test(script)) die(`Invalid script name: ${script}`);
  if (!hasScript(packageScripts(project), script))
    die(
      `Script '${script}' is not defined in ${path.relative(ROOT, project.cwd) || "."}/package.json.`,
    );
  requireManager(project);
  const { command, argv } = scriptInvocation(project, script);
  const result = spawnSync(command, argv, {
    cwd: project.cwd,
    stdio: "inherit",
  });
  if (result.error) die(result.error.message);
  return result.status == null ? 1 : result.status;
}
function worker(argv) {
  const [name, token, mode] = argv;
  if (
    !NAME_RE.test(name || "") ||
    !/^[a-f0-9]{32}$/.test(token || "") ||
    !["dev", "prod"].includes(mode)
  )
    process.exit(2);
  const config = cfg();
  const project = config.projects[name];
  if (!project) process.exit(2);
  const state = readState(name);
  if (!state || state.token !== token) process.exit(2);
  let child;
  let stopping = false;
  const beat = () => {
    try {
      writeJsonAtomic(heartbeatPath(name), {
        token,
        pid: process.pid,
        at: Date.now(),
      });
    } catch (err) {
      console.error(`Heartbeat failed: ${err.message}`);
    }
  };
  let heartbeatTimer;
  let maintenanceTimer;
  let releaseLog;
  try {
    // Project/script names are constrained and taken from local config; no raw shell arguments.
    const scriptText = packageScripts(project)[project[mode]];
    const portArgs =
      project.portArgs ??
      (/\b(?:vite|astro)\b/.test(scriptText)
        ? ["--port", "{port}", "--host", "127.0.0.1"]
        : []);
    const { command, argv } = scriptInvocation(
      project,
      project[mode],
      portArgs.map((value) => value.replaceAll("{port}", String(state.port))),
    );
    child = spawn(command, argv, {
      cwd: project.cwd,
      stdio: "inherit",
      env: {
        ...process.env,
        PORT: String(state.port),
        NODE_ENV:
          mode === "prod"
            ? "production"
            : process.env.NODE_ENV || "development",
      },
      windowsHide: true,
    });
  } catch (e) {
    console.error(e.message);
    cleanup(1);
    return;
  }
  function cleanup(code) {
    if (stopping) return;
    stopping = true;
    clearInterval(heartbeatTimer);
    clearInterval(maintenanceTimer);
    if (releaseLog) {
      try {
        releaseLog();
      } catch (error) {
        console.error(`Could not release the log lease: ${error.message}`);
      }
    }
    clearState(name, token);
    process.exit(code);
  }
  const shutdown = (signal) => {
    if (stopping) return;
    // When group termination is used, children receive the same signal. If
    // started directly, forwarding the signal is still appropriate.
    try {
      if (child && child.pid) child.kill(signal);
    } catch (error) {
      console.error(`Could not forward ${signal}: ${error.message}`);
    }
    setTimeout(() => cleanup(0), 1500).unref();
  };
  process.on("SIGTERM", () => shutdown("SIGTERM"));
  process.on("SIGINT", () => shutdown("SIGINT"));
  child.on("spawn", () => {
    if (MANAGED_LOGS) {
      try {
        releaseLog = storage.registerLog(ROOT, logPath(name));
      } catch (error) {
        console.error(`Could not register the active log: ${error.message}`);
      }
      maintenanceTimer = setInterval(() => {
        maintain(config, false).catch((error) =>
          console.error(`Storage maintenance failed: ${error.message}`),
        );
      }, 30000);
    }
    beat();
    heartbeatTimer = setInterval(beat, 1500);
  });
  child.on("error", (error) => {
    console.error(
      `Could not start ${managerFor(project).name}: ${error.message}`,
    );
    cleanup(1);
  });
  child.on("exit", (code, signal) => {
    if (!stopping)
      console.log(`Project '${name}' exited (${signal || code || 0}).`);
    cleanup(code ?? (signal ? 1 : 0));
  });
}
function activeLogPaths(config) {
  return [
    ...storage.registeredLogPaths(ROOT),
    ...Object.keys(config.projects)
      .filter((name) => {
        const state = readState(name);
        return state && alive(state.pid);
      })
      .map(logPath),
  ];
}
async function maintain(config, includeTrash = false) {
  return withProjectLock("__storage", async () =>
    storage.maintainStorage(ROOT, {
      policy: config.storage,
      protectedPaths: activeLogPaths(config),
      includeTrash,
    }),
  );
}
async function ready(url, timeout) {
  try {
    const response = await fetch(url, {
      redirect: "manual",
      signal: AbortSignal.timeout(timeout),
    });
    await response.body?.cancel();
    return response.status >= 200 && response.status < 400;
  } catch (error) {
    if (
      error instanceof TypeError ||
      error.name === "TimeoutError" ||
      error.name === "AbortError"
    )
      return false;
    throw error;
  }
}
async function startOne(project, mode, config, attempts = 0, excluded = []) {
  const name = project.name;
  const active = running(name);
  if (active) {
    info(
      `Stopping the existing managed instance of '${name}' before starting it again.`,
    );
    await stopOne(project);
  }
  const previous = readState(name);
  if (previous && alive(previous.pid))
    die(
      `Supervisor for '${name}' is alive but its heartbeat is unavailable. Inspect the service before starting another instance.`,
    );
  if (previous) info(`Removing stale state for ${name}.`);
  const script = project[mode];
  if (!hasScript(packageScripts(project), script))
    die(`${name}: package.json does not declare '${script}'.`);
  requireManager(project);
  if (MANAGED_LOGS) storage.ensureDirectories(ROOT);
  else fs.mkdirSync(STATE, { recursive: true });
  let port;
  let url;
  let log;
  const token = crypto.randomBytes(16).toString("hex");
  const state = {
    name,
    mode,
    token,
    // The starting CLI reserves this port until the supervisor PID is published.
    pid: process.pid,
    script,
    startedAt: new Date().toISOString(),
  };
  // Publish the token before the supervisor can read it. The project lock
  // protects publication from simultaneous start/stop/restart commands.
  await withProjectLock("__ports", async () => {
    port = await findAvailablePort(
      project,
      excluded,
      Object.keys(config.projects),
    );
    url = urlForPort(project.url, port);
    if (MANAGED_LOGS) {
      await withProjectLock("__storage", async () => {
        const options = {
          policy: config.storage,
          protectedPaths: activeLogPaths(config),
        };
        // Make room before creating the next log, so the cap is never exceeded.
        await storage.maintainStorage(ROOT, { ...options, reserveLogs: 1 });
        log = storage.createLog(ROOT);
      });
    } else log = logPath(name);
    writeJsonAtomic(path.join(STATE, `${name}.preferences.json`), {
      port,
      configuredUrl: project.url,
      log: path.relative(ROOT, log).split(path.sep).join("/"),
    });
    Object.assign(state, { port, url, log: path.relative(ROOT, log) });
    writeJsonAtomic(statePath(name), state);
  });
  const out = fs.openSync(log, "a");
  let proc;
  try {
    proc = spawn(process.execPath, [ENTRYPOINT, "__run", name, token, mode], {
      cwd: ROOT,
      detached: true,
      stdio: ["ignore", out, out],
      windowsHide: true,
    });
  } finally {
    fs.closeSync(out);
  }
  if (!proc.pid) {
    proc.once("error", (error) =>
      console.error(`Cannot launch supervisor: ${error.message}`),
    );
    clearState(name, token);
    die(`Could not start ${name}.`);
  }
  writeJsonAtomic(statePath(name), { ...state, pid: proc.pid });
  proc.unref();
  const deadline = Date.now() + project.startupTimeoutMs;
  while (Date.now() < deadline) {
    await sleep(100);
    if (
      running(name) &&
      (await ready(url, Math.max(1, Math.min(2000, deadline - Date.now())))) &&
      running(name)
    ) {
      info(
        `Started '${name}' (${mode}; supervisor PID ${proc.pid}). URL: ${url}. Logs: ${path.relative(ROOT, log)}`,
      );
      openBrowser(url);
      return true;
    }
    if (!alive(proc.pid)) break;
  }
  if (running(name)) await stopOne(project);
  const diagnostic = tailLog(log, 12);
  if (attempts < 2 && /EADDRINUSE|address already in use/i.test(diagnostic)) {
    info(
      `Port ${port} was claimed during startup; selecting another port (attempt ${attempts + 2}/3).`,
    );
    return startOne(project, mode, config, attempts + 1, [...excluded, port]);
  }
  console.error(
    `Could not confirm '${name}' responding successfully over HTTP at ${url} within ${project.startupTimeoutMs} ms. ${project.portArgs ? "Check portArgs and the service logs." : "The script must use PORT or accept configured portArgs."} Inspect: ${COMMAND} logs ${name}\n${diagnostic}`,
  );
  return false;
}
async function stopOne(project) {
  const { name } = project;
  const entry = readState(name);
  const url = runtimeUrl(project, entry);
  if (!entry) {
    info(`Not running: ${name} — URL: ${url}`);
    return true;
  }
  if (!running(name)) {
    if (alive(entry.pid))
      die(
        `Supervisor for '${name}' is alive but cannot be verified. Inspect its heartbeat; tracking state has been preserved.`,
      );
    info(
      `No verified live Moorkyz process for '${name}' at ${url}; leaving unrelated processes untouched.`,
    );
    clearState(name, entry.token);
    return true;
  }
  try {
    if (process.platform === "win32") {
      const result = spawnSync(
        "taskkill",
        ["/PID", String(entry.pid), "/T", "/F"],
        { windowsHide: true, encoding: "utf8", timeout: 10000 },
      );
      if (result.status !== 0 && alive(entry.pid))
        die(`Failed to stop ${name}: ${result.stderr || result.stdout}`);
    } else {
      process.kill(-entry.pid, "SIGTERM");
      for (let i = 0; i < 20 && alive(entry.pid); i++) await sleep(100);
      // Never SIGKILL arbitrary/possibly reused PIDs. Report if still running.
      if (alive(entry.pid) && freshHeartbeat(name, entry))
        die(`'${name}' did not stop gracefully. Review its process manually.`);
    }
    clearState(name, entry.token);
    info(`Stopped '${name}'. URL: ${url}`);
    return true;
  } catch (e) {
    die(`Could not stop '${name}': ${e.message}`);
  }
}
function statusOne(project) {
  const { name } = project;
  const entry = running(name);
  const url = runtimeUrl(project, entry);
  if (entry)
    info(
      `${name}: RUNNING (${entry.mode}, PID ${entry.pid}, started ${entry.startedAt}) — URL: ${url}`,
    );
  else info(`${name}: STOPPED — URL: ${url}`);
}
function tailLog(file, lines) {
  // Read only the end of the file; large logs are not fully loaded into memory.
  const st = fs.statSync(file);
  const start = Math.max(0, st.size - 512 * 1024);
  const fd = fs.openSync(file, "r");
  const buf = Buffer.alloc(st.size - start);
  try {
    fs.readSync(fd, buf, 0, buf.length, start);
  } finally {
    fs.closeSync(fd);
  }
  const logLines = buf.toString("utf8").split(/\r?\n/);
  if (logLines.at(-1) === "") logLines.pop();
  return logLines.slice(-lines).join("\n");
}
function showLogs(name, lines, follow) {
  let file = logPath(name);
  if (!exists(file))
    die(`No logs yet for '${name}'. Start it with '${COMMAND} start ${name}'.`);
  info(tailLog(file, lines));
  if (!follow) return;
  let offset = fs.statSync(file).size;
  info("Following logs; press Ctrl+C to stop.");
  const timer = setInterval(() => {
    try {
      const next = logPath(name);
      if (next !== file) {
        file = next;
        offset = 0;
      }
      const current = fs.statSync(file);
      if (current.size < offset) offset = 0;
      if (current.size > offset) {
        const readFd = fs.openSync(file, "r");
        try {
          let remaining = current.size - offset;
          while (remaining > 0) {
            const chunk = Buffer.alloc(Math.min(65536, remaining));
            const bytes = fs.readSync(readFd, chunk, 0, chunk.length, offset);
            if (!bytes) break;
            process.stdout.write(chunk.subarray(0, bytes));
            remaining -= bytes;
            offset += bytes;
          }
        } finally {
          fs.closeSync(readFd);
        }
      }
    } catch {
      /* File may be rotated */
    }
  }, 400);
  process.on("SIGINT", () => {
    clearInterval(timer);
    process.exit(0);
  });
}
function slugify(s) {
  return (
    s
      .toLowerCase()
      .normalize("NFKD")
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "")
      .slice(0, 60) || "task"
  );
}
function createAudit(title) {
  const source = [
    path.join(ROOT, "docs", "AUDIT_TEMPLATE.md"),
    path.join(ROOT, "docs", "audits", "TEMPLATE.md"),
    path.join(ROOT, "templates", "audit", "AUDIT.md"),
  ].find(exists);
  if (!source)
    die(
      "Missing audit template. Add docs/AUDIT_TEMPLATE.md (legacy docs/audits/TEMPLATE.md and templates/audit/AUDIT.md are also supported).",
    );
  storage.scopedPath(ROOT, source);
  const date = new Date();
  const timestamp = date
    .toISOString()
    .replace(/\.\d{3}Z$/, "Z")
    .replace(/:/g, "-");
  const slug = slugify(title);
  const auditDirectory =
    source === path.join(ROOT, "docs", "AUDIT_TEMPLATE.md")
      ? ".audits"
      : path.join("docs", "audits");
  const dest = path.join(ROOT, auditDirectory, `${timestamp}-${slug}.md`);
  const auditFolder = storage.scopedPath(ROOT, path.dirname(dest), true);
  fs.mkdirSync(auditFolder, { recursive: true });
  const template = fs
    .readFileSync(source, "utf8")
    .replaceAll("{{AUDIT_ID}}", `${timestamp}-${slug}`)
    .replaceAll("{{DATE_UTC}}", date.toISOString())
    .replaceAll("{{TASK_NAME}}", () =>
      title.replace(/[\r\n]+/g, " ").slice(0, 120),
    );
  fs.writeFileSync(dest, template, { flag: "wx" });
  info(
    `Created ${path.relative(ROOT, dest)}. Fill in observed results before committing.`,
  );
}
function listProjects(config) {
  for (const project of Object.values(config.projects)) {
    const url = runtimeUrl(project, running(project.name));
    info(
      `${project.name}${config.defaultProject === project.name ? " (default)" : ""}: ${path.relative(ROOT, project.cwd) || "."} — dev:${project.dev} prod:${project.prod} — URL: ${url}`,
    );
  }
}
function doctor(config) {
  info(`Project root: ${ROOT}`);
  info(
    `Node.js: ${process.version}${Number(process.versions.node.split(".")[0]) < 24 ? " (recommended: Node 24 LTS)" : ""}`,
  );
  info(
    `${COMMAND === "moorkyz" ? "Moorkyz" : COMMAND} config: ${exists(path.join(ROOT, CONFIG_FILE)) ? "present" : "defaults (single root project)"}`,
  );
  for (const project of Object.values(config.projects)) {
    const manager = managerFor(project);
    const { command, argv } = managerInvocation(manager.name, ["--version"]);
    const installed = spawnSync(command, argv, {
      cwd: project.cwd,
      encoding: "utf8",
      timeout: 5000,
    });
    info(
      `  ${project.name} package manager: ${manager.name} ${installed.status === 0 ? installed.stdout.trim() : "MISSING"}${manager.declared ? ` (declared ${manager.declared})` : " (detected from lockfile/default)"}`,
    );
    const manifest = path.join(project.cwd, "package.json");
    const scripts = exists(manifest)
      ? Object.keys(readJson(manifest).scripts || {})
      : [];
    info(
      `  ${project.name}: ${exists(manifest) ? `package.json OK; scripts: ${scripts.join(", ") || "none"}` : "package.json MISSING"}`,
    );
  }
  info(
    `Audit template: ${["docs/AUDIT_TEMPLATE.md", "docs/audits/TEMPLATE.md", "templates/audit/AUDIT.md"].some((file) => exists(path.join(ROOT, file))) ? "OK" : "MISSING"}`,
  );
  info(`Runtime directory: ${path.relative(ROOT, STATE)} (ignored by Git)`);
  info(
    MANAGED_LOGS
      ? `Storage: logs/ (maximum ${config.storage.maxLogs}); archive/ (ZIP); trash/ (local staging). Run '${COMMAND} maintain' to enforce retention.`
      : `Logs: ${STATE_DIRECTORY}/<project>.log (append-only; no automatic retention). Storage maintenance requires an explicit command.`,
  );
}
function help(command) {
  const commands = {
    start:
      "moorkyz start [project|all] [--prod]  Launch the service and open its URL in the default browser.",
    stop: "moorkyz stop [project|all]          Stop only services started and verified by Moorkyz.",
    restart:
      "moorkyz restart [project|all] [--prod]  Restart the service and open its URL in the default browser.",
    status:
      "moorkyz status [project|all]        Show the status of managed processes.",
    logs: "moorkyz logs [project] [--lines 80] [--follow]  Show or follow a service log.",
    list: "moorkyz list                         List configured projects.",
    doctor:
      "moorkyz doctor                       Check Node, the package manager, scripts, and repository prerequisites.",
    verify:
      "moorkyz verify [project]             Run package.json verify script, or available quality gates.",
    build:
      "moorkyz build [project]              Run package.json build script.",
    test: "moorkyz test [project]               Run package.json test script.",
    lint: "moorkyz lint [project]               Run package.json lint script.",
    typecheck:
      "moorkyz typecheck [project]          Run package.json typecheck script.",
    security:
      "moorkyz security [project]           Run a read-only package-manager vulnerability audit.",
    install:
      "moorkyz install [project]            Install dependencies reproducibly using the authoritative package manager and lockfile.",
    run: "moorkyz run <script> [project]       Run an existing package.json script.",
    audit:
      'moorkyz audit "task description"       Create a documented audit from docs/AUDIT_TEMPLATE.md.',
    archive:
      "moorkyz archive <path> [path ...]     Compress selected project paths in one verified ZIP, then remove their originals.",
    maintain:
      "moorkyz maintain                     Archive malformed/expired logs and trash; prune oldest archives under disk pressure.",
    help: "moorkyz help [command]               Show general or command-specific help.",
  };
  if (command) {
    if (!Object.hasOwn(commands, command))
      die(`Unknown help topic '${command}'.`);
    info(commands[command].replace(/^moorkyz\b/, COMMAND));
  } else {
    info(
      `${COMMAND === "moorkyz" ? "Moorkyz" : COMMAND} — reusable project development CLI`,
    );
    info(`Usage: ${COMMAND} <command> [project|all] [options]\n`);
    Object.values(commands).forEach((description) =>
      info(description.replace(/^moorkyz\b/, COMMAND)),
    );
    info(
      "\nRun from the target project root. With no config, a single root package.json is assumed.",
    );
    info(
      `For multiple projects, copy moorkyz.config.example.json to ${CONFIG_FILE} and edit.`,
    );
    info(
      `Windows: .\\${COMMAND}.cmd help; Unix: ./${COMMAND} help; see docs/moorkyz-cli.md for command setup.`,
    );
    info(
      `State and saved ports: ${STATE_DIRECTORY}/; logs: ${MANAGED_LOGS ? "logs/" : `${STATE_DIRECTORY}/<project>.log`}; ZIP archives: archive/; staging: trash/ (all gitignored).`,
    );
  }
}
async function main() {
  const [command = "help", ...args] = process.argv.slice(2);
  if (command === "__run") {
    worker(args);
    return;
  }
  if (command === "help" || command === "--help" || command === "-h") {
    help(args[0]);
    return;
  }
  const config = cfg();
  if (command === "maintain") {
    if (args.length) die(`Usage: ${COMMAND} maintain`);
    const result = await maintain(config, true);
    for (const entry of result.archived)
      info(
        `Archived ${entry.entries} entries: ${path.relative(ROOT, entry.filename)}`,
      );
    info(
      `Storage maintained. Pruned ${result.pruned.removed.length} old archives.${result.pruned.lowSpace ? " Disk space is still below the minimum; free space outside archive/ manually." : ""}`,
    );
    return;
  }
  if (command === "archive") {
    if (!args.length || args.some((value) => value.startsWith("-")))
      die(`Usage: ${COMMAND} archive <project-relative-path> [path ...]`);
    const result = await withProjectLock("__storage", async () =>
      storage.archivePaths(ROOT, args, {
        policy: config.storage,
        protectedPaths: activeLogPaths(config),
      }),
    );
    info(
      `Archived ${result.entries} entries: ${path.relative(ROOT, result.filename)}`,
    );
    return;
  }
  if (command === "list") {
    listProjects(config);
    return;
  }
  if (command === "doctor") {
    doctor(config);
    return;
  }
  if (command === "audit") {
    const title = args.join(" ").trim();
    if (!title || title.startsWith("-"))
      die(`Usage: ${COMMAND} audit "description of completed task"`);
    createAudit(title);
    return;
  }
  if (["start", "stop", "restart", "status"].includes(command)) {
    const prod = args.includes("--prod");
    if (
      args.filter((x) => x !== "--prod").length > 1 ||
      args.some((x) => x.startsWith("--") && x !== "--prod")
    )
      die(
        `Invalid arguments for ${command}; run '${COMMAND} help ${command}'.`,
      );
    if (prod && !["start", "restart"].includes(command))
      die("--prod is only valid for start or restart.");
    const name = args.find((x) => x !== "--prod");
    const names = targetNames(config, name, true);
    let failed = false;
    for (const n of names) {
      if (command === "status") statusOne(config.projects[n]);
      else
        await withProjectLock(n, async () => {
          if (command === "start")
            failed =
              !(await startOne(
                config.projects[n],
                prod ? "prod" : "dev",
                config,
              )) || failed;
          else if (command === "stop")
            failed = !(await stopOne(config.projects[n])) || failed;
          else {
            await stopOne(config.projects[n]);
            failed =
              !(await startOne(
                config.projects[n],
                prod ? "prod" : "dev",
                config,
              )) || failed;
          }
        });
    }
    if (failed) process.exitCode = 1;
    return;
  }
  if (command === "logs") {
    const follow = args.includes("--follow");
    const idx = args.indexOf("--lines");
    const lines = idx !== -1 ? Number(args[idx + 1]) : 80;
    if (!Number.isInteger(lines) || lines < 1 || lines > 10000)
      die("--lines must be between 1 and 10000.");
    const filtered = args.filter(
      (arg, i) =>
        arg !== "--follow" &&
        arg !== "--lines" &&
        (idx === -1 || i !== idx + 1),
    );
    if (filtered.length > 1 || filtered.some((x) => x.startsWith("-")))
      die("Invalid logs arguments.");
    showLogs(targetNames(config, filtered[0], false)[0], lines, follow);
    return;
  }
  if (
    [
      "build",
      "test",
      "lint",
      "typecheck",
      "verify",
      "security",
      "install",
      "run",
    ].includes(command)
  ) {
    const script = command === "run" ? args.shift() : command;
    if (!script || !SCRIPT_RE.test(script))
      die(`Usage: ${COMMAND} run <existing-script> [project]`);
    if (args.length > 1 || args.some((x) => x.startsWith("-")))
      die(`Unexpected arguments. Run ${COMMAND} help.`);
    const name = targetNames(config, args[0], false)[0];
    const project = config.projects[name];
    if (command === "security" || command === "install") {
      const version = requireManager(project);
      const manager = managerFor(project);
      const modernYarn =
        manager.name === "yarn" && Number(version.split(".")[0]) >= 2;
      let argumentsForManager;
      if (command === "security")
        argumentsForManager =
          manager.name === "yarn"
            ? modernYarn
              ? ["npm", "audit", "--all", "--recursive", "--severity", "high"]
              : ["audit", "--level", "high"]
            : ["audit", "--audit-level", "high"];
      else {
        const lock =
          manager.name === "npm"
            ? "package-lock.json"
            : manager.name === "pnpm"
              ? "pnpm-lock.yaml"
              : "yarn.lock";
        if (!exists(path.join(manager.folder, lock)))
          die(
            `Missing ${lock} in ${path.relative(ROOT, manager.folder) || "."}. Generate and review the lockfile with ${manager.name} before reproducible installation.`,
          );
        argumentsForManager =
          manager.name === "npm"
            ? ["ci"]
            : [
                "install",
                manager.name === "yarn" && modernYarn
                  ? "--immutable"
                  : "--frozen-lockfile",
              ];
      }
      const { command: executable, argv } = managerInvocation(
        manager.name,
        argumentsForManager,
      );
      const result = spawnSync(executable, argv, {
        cwd: command === "install" ? manager.folder : project.cwd,
        stdio: "inherit",
      });
      if (result.error)
        die(`${manager.name} ${command} failed: ${result.error.message}`);
      process.exitCode = result.status ?? 1;
    } else if (
      command === "verify" &&
      !hasScript(packageScripts(project), "verify")
    ) {
      const scripts = packageScripts(project);
      const available = hasScript(scripts, "validate")
        ? ["validate"]
        : ["format:check", "lint", "typecheck", "test", "build"].filter((s) =>
            hasScript(scripts, s),
          );
      if (!available.length)
        die(
          "No verify/format:check/lint/typecheck/test/build scripts found in package.json.",
        );
      for (const s of available) {
        info(`\n--- ${s} ---`);
        if (runScript(s, project) !== 0) {
          process.exitCode = 1;
          break;
        }
      }
    } else process.exitCode = runScript(script, project);
    return;
  }
  die(`Unknown command '${command}'. Run '${COMMAND} help'.`);
}
main().catch((error) => {
  console.error(`Error: ${error.message}`);
  process.exitCode = 1;
});

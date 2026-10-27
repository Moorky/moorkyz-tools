import assert from "node:assert/strict";
import { test } from "node:test";
import fs from "node:fs/promises";
import path from "node:path";
import os from "node:os";
import { spawnSync } from "node:child_process";
import {
  createPlan,
  applyPlan,
  matchesRule,
  validateConfiguration,
} from "../../../tools/directory-cleaner/core.mjs";
import { createApplication } from "../../../tools/directory-cleaner/server.mjs";
import {
  createCleanerConfiguration,
  EMPTY_RULE,
  isCleanerPlan,
  isCleanerRun,
} from "../../packages/portal/src/components/directory-cleaner-model.ts";

test("canonical source paths cannot alias a protected root", async (t) => {
  const f = await fixture(t);
  const original = fs.realpath.bind(fs);
  t.mock.method(fs, "realpath", async (file) =>
    file === f.sourceDir ? path.parse(f.sourceDir).root : original(file),
  );
  await assert.rejects(createPlan(f.config), /filesystem root/);
  assert.equal(
    await fs.readFile(path.join(f.sourceDir, "a.tmp"), "utf8"),
    "archive",
  );
});

test("an archived file named receipt.jsonl never collides with its recovery receipt", async (t) => {
  const f = await fixture(t, { "receipt.jsonl": "original receipt bytes" });
  const plan = await createPlan({
    ...f.config,
    rules: [{ action: "archive", pattern: "*" }],
  });
  const result = await applyPlan(plan, `APPLY ${plan.id}`);
  assert.equal(result.status, "completed");
  assert.notEqual(result.completed[0].target, result.receiptPath);
  assert.equal(
    await fs.readFile(result.completed[0].target, "utf8"),
    "original receipt bytes",
  );
});

test("file destinations that would become parent folders fail preflight without writes", async (t) => {
  const f = await fixture(t, { a: "first", a0: "neighbor", b: "child" });
  const plan = await createPlan({
    ...f.config,
    rules: [
      { action: "move", pattern: "a", destination: f.destination },
      { action: "move", pattern: "a0", destination: f.destination },
      {
        action: "move",
        pattern: "b",
        destination: path.join(f.destination, "a"),
      },
    ],
  });
  await assert.rejects(
    applyPlan(plan, `APPLY ${plan.id}`),
    /destinations conflict/,
  );
  await assert.rejects(fs.access(f.destination), { code: "ENOENT" });
  assert.deepEqual((await fs.readdir(f.sourceDir)).sort(), ["a", "a0", "b"]);
});

async function fixture(
  t,
  files = { "important.pdf": "keep", "a.tmp": "archive", "photo.jpg": "move" },
) {
  const root = await fs.mkdtemp(
    path.join(os.tmpdir(), "tools.moorkyz.com-cleaner-"),
  );
  t.after(async () => {
    assert.ok(path.basename(root).startsWith("tools.moorkyz.com-cleaner-"));
    await fs.rm(root, { recursive: true, force: true });
  });
  const sourceDir = path.join(root, "source"),
    archiveDir = path.join(root, "archive"),
    destination = path.join(root, "moved");
  await fs.mkdir(sourceDir);
  for (const [name, text] of Object.entries(files)) {
    const file = path.join(sourceDir, name);
    await fs.mkdir(path.dirname(file), { recursive: true });
    await fs.writeFile(file, text);
  }
  return {
    root,
    sourceDir,
    archiveDir,
    destination,
    config: {
      version: 1,
      sourceDir,
      archiveDir,
      recursive: true,
      rules: [
        { action: "archive", pattern: "*.tmp" },
        { action: "move", pattern: "*.jpg", destination },
        { action: "keep", pattern: "*.pdf" },
      ],
    },
  };
}

test("preview is non-mutating, Keep wins, unmatched stays, and outputs are excluded", async (t) => {
  const f = await fixture(t, {
    "a.tmp": "archive",
    "kept.tmp": "keep",
    "unmatched.txt": "untouched",
    "nested/z.tmp": "nested",
  });
  const config = {
    ...f.config,
    rules: [...f.config.rules, { action: "keep", pattern: "kept.*" }],
  };
  const before = await fs.readdir(f.root);
  const plan = await createPlan(config);
  assert.deepEqual(await fs.readdir(f.root), before);
  assert.equal(
    plan.entries.find((entry) => entry.relativePath === "kept.tmp").action,
    "keep",
  );
  assert.equal(
    plan.entries.find((entry) => entry.relativePath === "unmatched.txt").action,
    "keep",
  );
  assert.equal(
    plan.entries.find((entry) => entry.relativePath === "nested/z.tmp").action,
    "archive",
  );
  assert.ok(isCleanerPlan(plan));
  const localArchive = path.join(f.sourceDir, ".directory-cleaner-archive");
  await fs.mkdir(localArchive);
  await fs.writeFile(
    path.join(localArchive, "previous.tmp"),
    "never reprocess",
  );
  const local = await createPlan({ ...config, archiveDir: localArchive });
  assert.ok(
    local.skipped.some(
      (entry) => entry.relativePath === ".directory-cleaner-archive",
    ),
  );
  assert.equal(
    local.entries.some((entry) => entry.relativePath.includes("previous.tmp")),
    false,
  );
  const shallow = await createPlan({ ...config, recursive: false });
  assert.equal(
    shallow.entries.some((entry) => entry.relativePath.includes("/")),
    false,
  );
});

test("glob/date/size conditions are strict AND filters, with deterministic globstar semantics", () => {
  const file = {
    relativePath: "nested/FILE.tmp",
    createdMs: Date.parse("2026-10-23T00:00:00Z"),
    bytes: 100,
  };
  assert.equal(matchesRule({ pattern: "**/*.TMP" }, file), true);
  assert.equal(
    matchesRule({ pattern: "**/*.tmp" }, { ...file, relativePath: "file.tmp" }),
    true,
  );
  assert.equal(matchesRule({ pattern: "nested/????.tmp" }, file), true);
  assert.equal(matchesRule({ pattern: "other/*.tmp" }, file), false);
  assert.equal(
    matchesRule({ pattern: "*.tmp", smallerThanBytes: 100 }, file),
    false,
  );
  assert.equal(
    matchesRule({ pattern: "*.tmp", largerThanBytes: 100 }, file),
    false,
  );
  assert.equal(
    matchesRule(
      {
        pattern: "*.tmp",
        smallerThanBytes: 101,
        largerThanBytes: 99,
        createdAfter: "2026-10-22",
        createdBefore: "2026-10-24",
      },
      file,
    ),
    true,
  );
  assert.equal(
    matchesRule({ pattern: "*.tmp", createdBefore: "2026-10-23" }, file),
    false,
  );
  assert.equal(
    matchesRule({ pattern: "*.tmp", createdAfter: "2026-10-23" }, file),
    false,
  );
  assert.equal(
    matchesRule(
      { pattern: "*.tmp", smallerThanBytes: 101, createdBefore: "2026-10-22" },
      file,
    ),
    false,
  );
  const start = performance.now();
  assert.equal(
    matchesRule(
      { pattern: "*a".repeat(100) + "b" },
      { ...file, relativePath: "a".repeat(240) },
    ),
    false,
  );
  assert.ok(
    performance.now() - start < 1000,
    "Wildcard matching must not use exponential regular-expression backtracking.",
  );
});

test("archives and moves preserve exact bytes and record recovery locations without touching kept files", async (t) => {
  const f = await fixture(t);
  const plan = await createPlan(f.config);
  await assert.rejects(applyPlan(plan, "yes"), /exact APPLY/);
  assert.equal(
    await fs.readFile(path.join(f.sourceDir, "a.tmp"), "utf8"),
    "archive",
  );
  const result = await applyPlan(plan, `APPLY ${plan.id}`);
  assert.equal(result.status, "completed");
  assert.equal(result.completed.length, 2);
  for (const entry of result.completed)
    assert.equal(
      await fs.readFile(entry.target, "utf8"),
      entry.action === "archive" ? "archive" : "move",
    );
  assert.equal(
    await fs.readFile(path.join(f.sourceDir, "important.pdf"), "utf8"),
    "keep",
  );
  await assert.rejects(fs.access(path.join(f.sourceDir, "a.tmp")), {
    code: "ENOENT",
  });
  assert.match(result.receiptPath, /receipt\.jsonl$/);
  const receipt = (await fs.readFile(result.receiptPath, "utf8"))
    .trim()
    .split("\n")
    .map(JSON.parse);
  assert.equal(receipt[0].actions.length, 2);
  assert.equal(
    receipt.filter((entry) => entry.status === "completed").length,
    2,
  );
});

test("fresh creation metadata is actually filtered and stale files/new files block all mutation", async (t) => {
  const f = await fixture(t, { "a.tmp": "original" });
  const future = await createPlan({
    ...f.config,
    rules: [
      { action: "archive", pattern: "*.tmp", createdAfter: "2100-01-01" },
    ],
  });
  assert.equal(future.entries[0].action, "keep");
  const bounded = await createPlan({
    ...f.config,
    rules: [
      {
        action: "archive",
        pattern: "*.tmp",
        createdAfter: "1970-01-01",
        createdBefore: "2100-01-01",
      },
    ],
  });
  assert.equal(bounded.entries[0].action, "archive");
  await fs.writeFile(path.join(f.sourceDir, "a.tmp"), "modified original");
  await assert.rejects(applyPlan(bounded, `APPLY ${bounded.id}`), /stale/);
  await assert.rejects(fs.access(f.archiveDir), { code: "ENOENT" });
  const newPlan = await createPlan(f.config);
  await fs.writeFile(path.join(f.sourceDir, "new.txt"), "new");
  await assert.rejects(applyPlan(newPlan, `APPLY ${newPlan.id}`), /stale/);
});

test("move collisions and symlink/junction destinations fail without overwriting or touching source", async (t) => {
  const f = await fixture(t, { "photo.jpg": "source" });
  await fs.mkdir(f.destination);
  await fs.writeFile(path.join(f.destination, "photo.jpg"), "existing");
  const plan = await createPlan(f.config);
  await assert.rejects(applyPlan(plan, `APPLY ${plan.id}`), /already exists/);
  assert.equal(
    await fs.readFile(path.join(f.destination, "photo.jpg"), "utf8"),
    "existing",
  );
  await fs.unlink(path.join(f.destination, "photo.jpg"));
  await fs.rmdir(f.destination);
  await fs.mkdir(path.join(f.root, "outside"));
  await fs.symlink(
    path.join(f.root, "outside"),
    f.destination,
    process.platform === "win32" ? "junction" : "dir",
  );
  await assert.rejects(createPlan(f.config), /Unsafe folder/);
  assert.equal(
    await fs.readFile(path.join(f.sourceDir, "photo.jpg"), "utf8"),
    "source",
  );
  await fs.rm(f.destination, { recursive: true, force: true });
  await fs.mkdir(f.destination);
  await fs.symlink(
    path.join(f.root, "outside"),
    path.join(f.sourceDir, "link"),
    process.platform === "win32" ? "junction" : "dir",
  );
  const withLink = await createPlan(f.config);
  assert.ok(
    withLink.skipped.some(
      (entry) =>
        entry.relativePath === "link" && entry.reason.includes("not followed"),
    ),
  );
  await assert.rejects(
    createPlan({ ...f.config, sourceDir: path.join(f.sourceDir, "link") }),
    /Unsafe folder/,
  );
});

test("copy fallback verifies bytes and preservation failures stop with explicit partial results", async (t) => {
  const f = await fixture(t, {
    "a.tmp": "first",
    "b.tmp": "second",
    "c.tmp": "pending",
  });
  const originalLink = fs.link.bind(fs);
  t.mock.method(fs, "link", async (source, target) => {
    if (source.endsWith("a.tmp")) {
      const error = new Error("different filesystem");
      error.code = "EXDEV";
      throw error;
    }
    if (source.endsWith("b.tmp")) {
      const error = new Error("locked");
      error.code = "EACCES";
      throw error;
    }
    return originalLink(source, target);
  });
  const plan = await createPlan(f.config);
  const result = await applyPlan(plan, `APPLY ${plan.id}`);
  assert.equal(result.status, "partial");
  assert.equal(result.completed.length, 1);
  assert.equal(result.failed.length, 1);
  assert.equal(result.pending, 1);
  assert.equal(await fs.readFile(result.completed[0].target, "utf8"), "first");
  assert.equal(
    await fs.readFile(path.join(f.sourceDir, "b.tmp"), "utf8"),
    "second",
  );
  assert.equal(
    await fs.readFile(path.join(f.sourceDir, "c.tmp"), "utf8"),
    "pending",
  );
});

test("configuration rejects dangerous roots, invalid ranges, invalid rules and archive containment", async (t) => {
  const f = await fixture(t);
  for (const override of [
    { sourceDir: path.parse(f.sourceDir).root },
    { sourceDir: os.homedir() },
    { sourceDir: "relative" },
    { sourceDir: "\\\\server\\share" },
    { sourceDir: "//server/share" },
    { archiveDir: f.sourceDir },
    { archiveDir: f.root },
    { rules: [] },
    { rules: [{ action: "delete", pattern: "*" }] },
    { rules: [{ action: "archive", pattern: "../*" }] },
    { rules: [{ action: "archive", pattern: "*", smallerThanBytes: -1 }] },
    {
      rules: [
        {
          action: "archive",
          pattern: "*",
          smallerThanBytes: 1,
          largerThanBytes: 2,
        },
      ],
    },
    {
      rules: [{ action: "archive", pattern: "*", createdBefore: "2026-02-30" }],
    },
    {
      rules: [
        {
          action: "archive",
          pattern: "*",
          createdBefore: "2025-01-01",
          createdAfter: "2026-01-01",
        },
      ],
    },
    { rules: [{ action: "move", pattern: "*", destination: f.root }] },
  ])
    assert.throws(() => validateConfiguration({ ...f.config, ...override }));
});

test("standalone CLI previews without changes, refuses plan overwrite, then archives with exact confirmation", async (t) => {
  const f = await fixture(t, { "a.tmp": "from CLI" });
  const settings = path.join(f.root, "settings.json"),
    planFile = path.join(f.root, "preview.json");
  await fs.writeFile(settings, JSON.stringify(f.config));
  const run = (args) =>
    spawnSync(
      process.execPath,
      [path.resolve("tools", "directory-cleaner", "clean.mjs"), ...args],
      { encoding: "utf8", timeout: 15000 },
    );
  let result = run(["--settings", settings, "--plan", planFile]);
  assert.equal(result.status, 0, result.stderr);
  assert.equal(
    await fs.readFile(path.join(f.sourceDir, "a.tmp"), "utf8"),
    "from CLI",
  );
  assert.notEqual(run(["--settings", settings, "--plan", planFile]).status, 0);
  const plan = JSON.parse(await fs.readFile(planFile, "utf8"));
  assert.notEqual(run(["--apply", planFile, "--confirm", "yes"]).status, 0);
  result = run(["--apply", planFile, "--confirm", `APPLY ${plan.id}`]);
  assert.equal(result.status, 0, result.stderr);
  assert.equal(JSON.parse(result.stdout).status, "completed");
});

test("local service rejects cross-site/tokenless requests and applies only its own confirmed preview", async (t) => {
  const f = await fixture(t, { "a.tmp": "service" });
  const app = createApplication();
  await new Promise((resolve) => app.server.listen(0, "127.0.0.1", resolve));
  t.after(() => app.close());
  const base = `http://127.0.0.1:${app.server.address().port}`;
  const config = await (await fetch(`${base}/api/config`)).json();
  assert.equal(config.deleteBehavior, "archive");
  assert.equal(
    (
      await fetch(`${base}/api/config`, {
        headers: { Origin: "https://evil.example" },
      })
    ).status,
    403,
  );
  const post = (resource, body, token = config.token) =>
    fetch(`${base}/api/${resource}`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Directory-Cleaner-Token": token,
      },
      body: JSON.stringify(body),
    });
  assert.equal((await post("preview", f.config, "wrong")).status, 403);
  const preview = await (await post("preview", f.config)).json();
  assert.equal(
    (await post("apply", { planId: preview.id, confirmation: "yes" })).status,
    400,
  );
  assert.equal(
    (
      await post("apply", {
        planId: preview.id,
        confirmation: `APPLY ${preview.id}`,
        entries: [],
      })
    ).status,
    400,
  );
  const accepted = await post("apply", {
    planId: preview.id,
    confirmation: `APPLY ${preview.id}`,
  });
  assert.equal(accepted.status, 202);
  let run;
  for (let attempt = 0; attempt < 100; attempt++) {
    run = await (
      await fetch(`${base}/api/runs/${preview.id}`, {
        headers: { "X-Directory-Cleaner-Token": config.token },
      })
    ).json();
    if (run.status !== "running") break;
    await new Promise((resolve) => setTimeout(resolve, 20));
  }
  assert.ok(isCleanerRun(run));
  assert.equal(run.status, "completed", JSON.stringify(run));
  assert.equal(
    await fs.readFile(run.result.completed[0].target, "utf8"),
    "service",
  );
  assert.equal(
    (
      await post("apply", {
        planId: preview.id,
        confirmation: `APPLY ${preview.id}`,
      })
    ).status,
    409,
  );
});

test("rule editor emits exact byte bounds, rejects invalid settings and validates native result shapes", () => {
  const rule = {
    ...EMPTY_RULE,
    action: "archive",
    smallerThan: "1",
    largerThan: "0.5",
    unit: "MiB",
  };
  const result = createCleanerConfiguration("C:\\Files", "", false, [rule]);
  assert.equal(result.rules[0].smallerThanBytes, 1048576);
  assert.equal(result.rules[0].largerThanBytes, 524288);
  assert.equal(result.recursive, false);
  assert.throws(() => createCleanerConfiguration("", "", false, [rule]));
  assert.throws(() =>
    createCleanerConfiguration("C:\\Files", "", false, [
      { ...rule, smallerThan: "-1" },
    ]),
  );
  assert.throws(() =>
    createCleanerConfiguration("C:\\Files", "", false, [
      { ...rule, largerThan: "2" },
    ]),
  );
  assert.equal(isCleanerPlan({}), false);
  assert.equal(isCleanerRun({ id: "x", status: "completed" }), false);
});

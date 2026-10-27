import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { test } from "node:test";
import {
  mkdtemp,
  mkdir,
  readFile,
  readdir,
  rm,
  symlink,
  writeFile,
} from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  createVisorySnapshot,
  provenanceName,
  updateVisoryProvenance,
  verifyVisorySnapshot,
} from "../../scripts/sync-visory-source.mjs";

const root = fileURLToPath(new URL("../../..", import.meta.url));
const allowlist = async () =>
  JSON.parse(
    await readFile(
      path.join(
        root,
        "tools",
        "visory-media-downloader",
        "Data",
        "Source",
        "source-files.json",
      ),
      "utf8",
    ),
  );

test("Visory source allowlist is complete, text-only and matches its recorded provenance", async () => {
  const names = await allowlist();
  const provenance = await verifyVisorySnapshot(
    path.join(root, "tools", "visory-media-downloader"),
    names,
  );
  assert.equal(provenance.repository, "Moorky/moorkyz-tools");
  assert.equal(provenance.sourceDirectory, "tools/visory-media-downloader");
  assert.equal(provenance.identity, "Moorksz Visory Media Downloader");
  assert.equal(provenance.files.length, names.length - 1);
  for (const required of [
    "README.md",
    "README.html",
    "RUN.cmd",
    "Data/Portal/portal.json",
    "Data/Source/source-files.json",
    provenanceName,
    "src/docs/ARCHITECTURE.md",
    "src/docs/VISORY_INTEGRATION.md",
    "src/docs/TEST_REPORT.md",
    "src/docs/RELEASE_NOTES.md",
    "src/MoorkszVisoryMediaDownloader.sln",
    "src/scripts/startup/run.ps1",
    "src/scripts/startup/diagnostics.ps1",
    "src/scripts/development/publish.ps1",
    "src/utils/diagnostics/RunLogger.cs",
    "src/tools/desktop/MoorkszVisoryMediaDownloader.App.csproj",
    "src/core/Providers/DiscoverySession.cs",
    "src/tests/MoorkszVisoryMediaDownloader.Tests/Scenarios.cs",
    "src/tests/MoorkszVisoryMediaDownloader.Tests/DiagnosticsScenarios.cs",
    "src/third-party/wpf-10.0.11-NOTICES.txt",
  ])
    assert.ok(names.includes(required), required);
  assert.ok(
    !names.some((name) =>
      /(?:^|\/)(?:bin|obj|artifacts|runtime|\.fixtures)\/|\.(?:exe|dll|db|zip|mp4|webm|part)$/i.test(
        name,
      ),
    ),
  );
});

test("Visory separates user-facing Data from src and preserves publish notices", async () => {
  const tool = path.join(root, "tools", "visory-media-downloader");
  const entries = await readdir(tool, { withFileTypes: true });
  assert.deepEqual(
    entries
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name)
      .sort(),
    ["Data", "src"],
  );
  assert.ok(!entries.some((entry) => entry.name.endsWith(".json")));
  const data = await readdir(path.join(tool, "Data"), { withFileTypes: true });
  assert.deepEqual(data.map((entry) => entry.name).sort(), [
    "Portal",
    "Source",
  ]);
  assert.ok(data.every((entry) => entry.isDirectory()));
  const publish = await readFile(
    path.join(tool, "src", "scripts", "development", "publish.ps1"),
    "utf8",
  );
  assert.match(publish, /OutputRoot = 'src\\artifacts\\release'/);
  assert.match(
    publish,
    /Source = '\.\\src\\third-party\\wpf-10\.0\.11-NOTICES\.txt'/,
  );
  await readFile(
    path.join(tool, "src", "third-party", "wpf-10.0.11-NOTICES.txt"),
  );
});

test("Git checkout rules preserve every Visory source byte", async () => {
  const names = (await allowlist()).filter((name) => name !== provenanceName);
  const input = `${names.map((name) => `tools/visory-media-downloader/${name}`).join("\n")}\n`;
  const options = { cwd: root, input, encoding: "utf8" };
  assert.equal(
    execFileSync("git", ["hash-object", "--stdin-paths"], options),
    execFileSync(
      "git",
      ["hash-object", "--no-filters", "--stdin-paths"],
      options,
    ),
    "Desktop source must not undergo EOL conversion.",
  );
});

test("Visory README is generated, offline and has a browser-normalized CSP stylesheet hash", async () => {
  const tool = path.join(root, "tools", "visory-media-downloader");
  const generator = path.join(
    tool,
    "src",
    "scripts",
    "development",
    "readme.mjs",
  );
  const check = spawnSync(process.execPath, [generator, "--check"], {
    encoding: "utf8",
  });
  assert.equal(check.status, 0, check.stderr);
  const before = await readFile(path.join(tool, "README.html"), "utf8");
  const invalid = spawnSync(process.execPath, [generator, "--invalid"], {
    encoding: "utf8",
  });
  assert.notEqual(invalid.status, 0);
  assert.equal(await readFile(path.join(tool, "README.html"), "utf8"), before);
  assert.doesNotMatch(before, /<script\b|<link\b[^>]*rel=["']stylesheet/i);
  const style = /<style>([\s\S]*?)<\/style>/.exec(before)?.[1];
  assert.ok(style);
  const hash = createHash("sha256")
    .update(style.replace(/\r\n/g, "\n"))
    .digest("base64");
  assert.ok(before.includes(`style-src 'sha256-${hash}'`));
  for (const match of before.matchAll(/href="([^"#]+)"/g)) {
    if (/^https:/.test(match[1])) continue;
    await readFile(path.resolve(tool, match[1]));
  }
  assert.match(
    await readFile(path.join(tool, "RUN.cmd"), "utf8"),
    /src\\scripts\\startup\\run\.ps1/,
  );
});

async function fixture(run) {
  const folder = await mkdtemp(path.join(tmpdir(), "visory-source-"));
  try {
    await writeFile(path.join(folder, "README.md"), "# Fixture\r\n");
    await run(folder);
  } finally {
    await rm(folder, { recursive: true, force: true });
  }
}

test("Visory provenance is deterministic, rejects changed bytes and refuses arguments", async () => {
  await fixture(async (folder) => {
    const names = ["README.md", provenanceName];
    const first = await createVisorySnapshot(folder, names);
    assert.deepEqual(first, await createVisorySnapshot(folder, names));
    assert.match(first.provenance.treeSha256, /^[a-f0-9]{64}$/);
    await updateVisoryProvenance(folder, names);
    assert.ok(await readFile(path.join(folder, provenanceName)));
    await verifyVisorySnapshot(folder, names);
    await writeFile(path.join(folder, "README.md"), "Modified");
    await assert.rejects(
      verifyVisorySnapshot(folder, names),
      /provenance mismatch/,
    );
    const result = spawnSync(
      process.execPath,
      [path.join(root, "src", "scripts", "sync-visory-source.mjs"), folder],
      { encoding: "utf8" },
    );
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Arguments are not supported/);
  });
});

test("Visory nested provenance rejects linked input and output directories", async () => {
  await fixture(async (folder) => {
    const outside = path.join(folder, "outside");
    await mkdir(outside);
    await symlink(outside, path.join(folder, "Data"), "junction");
    const names = ["README.md", provenanceName];
    await assert.rejects(
      updateVisoryProvenance(folder, names),
      /Linked provenance output/,
    );
    assert.deepEqual(await readdir(outside), []);
    await assert.rejects(verifyVisorySnapshot(folder, names), /Linked source/);
  });
});

test("Visory allowlist rejects traversal, duplicates, state, binaries, caches and links", async () => {
  await fixture(async (folder) => {
    for (const name of [
      "../README.md",
      "/README.md",
      "a\\README.md",
      ".git/config",
      "bin/app.json",
      "artifacts/release/README.md",
      ".fixtures/run.json",
      "state/settings.json",
      "runtime/runs/log.md",
      "src/artifacts/release/README.md",
      "src/.fixtures/run.json",
      "src/runtime/runs/log.md",
      "recording.mp4",
      "visory-backup.db",
      "MoorkszVisoryMediaDownloader.exe",
    ])
      await assert.rejects(
        createVisorySnapshot(folder, [name, provenanceName]),
        /Unsafe Moorksz Visory Media Downloader source path/,
      );
    await assert.rejects(
      createVisorySnapshot(folder, ["README.md"]),
      /Invalid Moorksz Visory Media Downloader source allowlist/,
    );
    await writeFile(path.join(folder, "README.md"), Buffer.from([0, 255]));
    await assert.rejects(
      createVisorySnapshot(folder, ["README.md", provenanceName]),
      /not UTF-8 text/,
    );
    const real = path.join(folder, "real");
    await mkdir(real);
    await writeFile(path.join(real, "File.cs"), "// fixture");
    await symlink(real, path.join(folder, "linked"), "junction");
    await assert.rejects(
      createVisorySnapshot(folder, ["linked/File.cs", provenanceName]),
      /Linked source/,
    );
  });
});

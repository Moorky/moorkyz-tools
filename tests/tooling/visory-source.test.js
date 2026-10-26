import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { test } from "node:test";
import {
  mkdtemp,
  mkdir,
  readFile,
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

const root = fileURLToPath(new URL("../..", import.meta.url));
const allowlist = async () =>
  JSON.parse(
    await readFile(
      path.join(root, "tools", "visory-media-downloader.files.json"),
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
    "ARCHITECTURE.md",
    "VISORY_INTEGRATION.md",
    "TEST_REPORT.md",
    "RELEASE_NOTES.md",
    "MoorkszVisoryMediaDownloader.sln",
    "scripts/publish.ps1",
    "src/MoorkszVisoryMediaDownloader.App/MoorkszVisoryMediaDownloader.App.csproj",
    "src/MoorkszVisoryMediaDownloader.Core/Providers/DiscoverySession.cs",
    "tests/MoorkszVisoryMediaDownloader.Tests/Scenarios.cs",
  ])
    assert.ok(names.includes(required), required);
  assert.ok(
    !names.some((name) =>
      /(?:^|\/)(?:bin|obj|artifacts|\.fixtures)\/|\.(?:exe|dll|db|zip|mp4|webm|part)$/i.test(
        name,
      ),
    ),
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
    await verifyVisorySnapshot(folder, names);
    await writeFile(path.join(folder, "README.md"), "Modified");
    await assert.rejects(
      verifyVisorySnapshot(folder, names),
      /provenance mismatch/,
    );
    const result = spawnSync(
      process.execPath,
      [path.join(root, "scripts", "sync-visory-source.mjs"), folder],
      { encoding: "utf8" },
    );
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Arguments are not supported/);
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

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
  createMediaRecoverSnapshot,
  checkSnapshotOutput,
  provenanceName,
  verifyMediaRecoverSnapshot,
  updateMediaRecoverProvenance,
} from "../../scripts/sync-mediarecover-source.mjs";

test("Git checkout rules preserve every pinned desktop source byte", async () => {
  const root = fileURLToPath(new URL("../../..", import.meta.url));
  const definition = JSON.parse(
    await readFile(
      path.join(root, "tools", "mediarecover", "source-files.json"),
      "utf8",
    ),
  );
  const names = Array.isArray(definition) ? definition : definition.files;
  const input = `${names.map((name) => `tools/mediarecover/${name}`).join("\n")}\n`;
  const options = { cwd: root, input, encoding: "utf8" };
  const stored = execFileSync("git", ["hash-object", "--stdin-paths"], options);
  const original = execFileSync(
    "git",
    ["hash-object", "--no-filters", "--stdin-paths"],
    options,
  );
  assert.equal(
    stored,
    original,
    "Pinned source must not undergo EOL conversion.",
  );
});

async function fixture(run) {
  const folder = await mkdtemp(path.join(tmpdir(), "mediarecover-snapshot-"));
  try {
    await writeFile(path.join(folder, "README.md"), "# Fixture\r\n");
    await run(folder);
  } finally {
    await rm(folder, { recursive: true, force: true });
  }
}

test("desktop source snapshot is deterministic, pinned and rejects changed provenance or bytes", async () => {
  await fixture(async (folder) => {
    const names = ["README.md", provenanceName];
    const first = await createMediaRecoverSnapshot(folder, names);
    const second = await createMediaRecoverSnapshot(folder, names);
    assert.deepEqual(first, second);
    assert.equal(first.provenance.repository, "Moorky/moorkyz-tools");
    assert.equal(first.provenance.sourceDirectory, "tools/mediarecover");
    assert.match(first.provenance.treeSha256, /^[a-f0-9]{64}$/);
    assert.equal(first.files[0].bytes.toString(), "# Fixture\r\n");
    const target = path.join(folder, provenanceName);
    await writeFile(target, JSON.stringify(first.provenance));
    assert.deepEqual(
      await verifyMediaRecoverSnapshot(folder, names),
      first.provenance,
    );
    await writeFile(path.join(folder, "README.md"), "Modified");
    await assert.rejects(
      verifyMediaRecoverSnapshot(folder, names),
      /provenance mismatch/,
    );
    await writeFile(path.join(folder, "README.md"), "# Fixture\r\n");
    await writeFile(
      target,
      JSON.stringify({ ...first.provenance, repository: "wrong/repository" }),
    );
    await assert.rejects(
      verifyMediaRecoverSnapshot(folder, names),
      /provenance mismatch/,
    );
  });
});

test("local source edits refresh only provenance and external-source CLI arguments are rejected", async () => {
  await fixture(async (folder) => {
    const names = ["README.md", provenanceName];
    await updateMediaRecoverProvenance(folder, names);
    const before = await readFile(path.join(folder, provenanceName), "utf8");
    const bytes = Buffer.from("# Locally maintained source\r\n");
    await writeFile(path.join(folder, "README.md"), bytes);
    await assert.rejects(
      verifyMediaRecoverSnapshot(folder, names),
      /provenance mismatch/,
    );
    await updateMediaRecoverProvenance(folder, names);
    assert.notEqual(
      await readFile(path.join(folder, provenanceName), "utf8"),
      before,
    );
    assert.deepEqual(await readFile(path.join(folder, "README.md")), bytes);
    await verifyMediaRecoverSnapshot(folder, names);
    const result = spawnSync(
      process.execPath,
      [
        fileURLToPath(
          new URL(
            "../../scripts/sync-mediarecover-source.mjs",
            import.meta.url,
          ),
        ),
        folder,
      ],
      { encoding: "utf8" },
    );
    assert.equal(result.status, 1);
    assert.match(
      result.stderr,
      /External source directories are not supported/,
    );
  });
});

test("desktop source allowlist rejects traversal, duplicates, state, binaries and caches", async () => {
  await fixture(async (folder) => {
    for (const name of [
      "../README.md",
      "a/../../README.md",
      "/README.md",
      "C:/README.md",
      "a\\README.md",
      "a//README.md",
      ".git/config",
      "obj/file.json",
      "artifacts/summary.json",
      "state/settings.json",
      "recording.mp4",
      "state.db",
      "program.exe",
    ])
      await assert.rejects(
        createMediaRecoverSnapshot(folder, [name, provenanceName]),
        /Unsafe Media Recover source path/,
      );
    for (const names of [
      ["README.md"],
      ["README.md", "README.md", provenanceName],
      null,
    ])
      await assert.rejects(
        createMediaRecoverSnapshot(folder, names),
        /Invalid Media Recover source allowlist/,
      );
    await writeFile(path.join(folder, "README.md"), Buffer.from([0, 255]));
    await assert.rejects(
      createMediaRecoverSnapshot(folder, ["README.md", provenanceName]),
      /not UTF-8 text/,
    );
  });
});

test("desktop snapshot refuses linked directories even when the link stays in the source", async () => {
  await fixture(async (folder) => {
    const real = path.join(folder, "real");
    await mkdir(real);
    await writeFile(path.join(real, "File.cs"), "// fixture");
    await symlink(real, path.join(folder, "linked"), "junction");
    await assert.rejects(
      createMediaRecoverSnapshot(folder, ["linked/File.cs", provenanceName]),
      /Linked source/,
    );
    await assert.rejects(
      checkSnapshotOutput(path.join(folder, "linked", "File.cs")),
      /Linked snapshot output/,
    );
    await checkSnapshotOutput(path.join(folder, "new", "File.cs"));
  });
});

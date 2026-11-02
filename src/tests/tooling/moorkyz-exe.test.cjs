"use strict";

const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { test } = require("node:test");

const root = path.resolve(__dirname, "..", "..", "..");
const builder = path.join(
  root,
  "src",
  "scripts",
  "deployment",
  "build-moorkyz-exe.ps1",
);

test(
  "Windows EXE builds with the project icon and launches the shared startup flow",
  { skip: process.platform !== "win32", timeout: 120000 },
  (t) => {
    const temporary = fs.mkdtempSync(path.join(os.tmpdir(), "moorkyz-exe-"));
    t.after(() => fs.rmSync(temporary, { recursive: true, force: true }));
    const output = path.join(temporary, "output with spaces", "Moorkyz.exe");
    const build = (destination = output, ...args) =>
      spawnSync(
        "powershell.exe",
        ["-NoProfile", "-File", builder, "-OutputPath", destination, ...args],
        { cwd: temporary, encoding: "utf8", timeout: 20000 },
      );
    const result = build();
    assert.equal(result.status, 0, result.stdout + result.stderr);
    assert.match(result.stdout, /PASS: Built unsigned launcher/);
    assert.match(result.stdout, /SHA256: [A-F0-9]{64}/);

    const exe = fs.readFileSync(output);
    const pe = exe.readUInt32LE(0x3c);
    assert.equal(exe.toString("ascii", pe, pe + 4), "PE\0\0");
    assert.equal(exe.readUInt16LE(pe + 24 + 68), 2, "windowed GUI subsystem");
    const icon = fs.readFileSync(
      path.join(root, "src", ".moorkyz", "public", "favicon.ico"),
    );
    assert.equal(icon.readUInt16LE(2), 1);
    assert.ok(icon.readUInt16LE(4) > 0);
    for (let i = 0; i < icon.readUInt16LE(4); i++) {
      const size = icon.readUInt32LE(6 + i * 16 + 8);
      const offset = icon.readUInt32LE(6 + i * 16 + 12);
      assert.ok(
        exe.includes(icon.subarray(offset, offset + size)),
        `embedded icon image ${i}`,
      );
    }

    const repeated = build();
    assert.notEqual(repeated.status, 0);
    assert.match(repeated.stderr, /Refusing to overwrite/);
    assert.deepEqual(fs.readFileSync(output), exe);
    const invalidIcon = build(
      path.join(temporary, "invalid.exe"),
      "-IconPath",
      path.join(temporary, "missing.ico"),
    );
    assert.notEqual(invalidIcon.status, 0);
    assert.match(invalidIcon.stderr, /Required file not found/);
    assert.equal(fs.existsSync(path.join(temporary, "invalid.exe")), false);
    const brokenIcon = path.join(temporary, "broken.ico");
    fs.writeFileSync(brokenIcon, "not an icon");
    const failedCompile = build(
      path.join(temporary, "failed.exe"),
      "-IconPath",
      brokenIcon,
    );
    assert.notEqual(failedCompile.status, 0);
    assert.match(failedCompile.stderr, /EXE compilation failed/);
    assert.equal(fs.existsSync(path.join(temporary, "failed.exe")), false);
    const invalidExtension = build(path.join(temporary, "wrong.dll"));
    assert.notEqual(invalidExtension.status, 0);
    assert.match(invalidExtension.stderr, /must name an .exe file/);
    assert.equal(fs.existsSync(path.join(temporary, "wrong.dll")), false);

    const run = (args, env = process.env) =>
      spawnSync(output, args, {
        cwd: temporary,
        env,
        encoding: "utf8",
        timeout: 45000,
      });
    const actual = run(["--root", root, "--check"]);
    assert.equal(actual.status, 0, actual.stdout + actual.stderr);
    assert.match(actual.stdout, /1\. Check required files/);
    assert.match(actual.stdout, /3\. Check workspace packages/);
    const unknown = run(["--check", "--unexpected"]);
    assert.equal(unknown.status, 1);
    assert.match(unknown.stderr, /Usage:/);
    const missing = run(["--check", "--root", temporary]);
    assert.equal(missing.status, 1);
    assert.match(missing.stderr, /Required checkout file is missing/);
    const noRoot = run(["--check"]);
    assert.equal(noRoot.status, 1);
    assert.match(noRoot.stderr, /Keep the EXE inside the checkout/);

    const checkout = path.join(temporary, "checkout & apostrophe's space");
    for (const relative of [
      "moorkyz",
      "src/.moorkyz/scripts/moorkyz.cjs",
      "src/.moorkyz/scripts/helpers/gui.cjs",
      "src/.moorkyz/scripts/helpers/startup.cjs",
      "src/package.json",
      "src/.moorkyz/config/moorkyz.config.json",
      "src/.moorkyz/gui/start.ps1",
    ]) {
      const file = path.join(checkout, relative);
      fs.mkdirSync(path.dirname(file), { recursive: true });
      fs.writeFileSync(file, "fixture only");
    }
    const emptyPath = run(["--check", "--root", checkout], {
      ...process.env,
      PATH: "",
    });
    assert.equal(emptyPath.status, 1);
    assert.match(emptyPath.stderr, /was not found on PATH/);

    const runtime = path.join(temporary, "fake runtime");
    fs.mkdirSync(runtime);
    const stub = path.join(runtime, "Runtime.cs");
    fs.writeFileSync(
      stub,
      `using System;
using System.IO;
internal static class Runtime {
  private static int Main(string[] args) {
    if (args.Length == 1 && args[0] == "--version") {
      Console.WriteLine(Environment.GetEnvironmentVariable("FIXTURE_NODE_VERSION") ?? "v22.18.0");
      return 0;
    }
    File.WriteAllLines(Environment.GetEnvironmentVariable("FIXTURE_ARGS"), args);
    return int.Parse(Environment.GetEnvironmentVariable("FIXTURE_EXIT_CODE") ?? "0");
  }
}`,
    );
    const compiler = ["Framework64", "Framework"]
      .map((folder) =>
        path.join(
          process.env.SystemRoot,
          "Microsoft.NET",
          folder,
          "v4.0.30319",
          "csc.exe",
        ),
      )
      .find((file) => fs.existsSync(file));
    const compile = spawnSync(
      compiler,
      ["/nologo", `/out:${path.join(runtime, "node.exe")}`, stub],
      { encoding: "utf8", timeout: 15000 },
    );
    assert.equal(compile.status, 0, compile.stdout + compile.stderr);
    const argumentLog = path.join(temporary, "arguments.txt");
    const env = {
      ...process.env,
      PATH: runtime,
      FIXTURE_ARGS: argumentLog,
    };
    const driveRelative = run(["--check", "--root", checkout], {
      ...env,
      PATH: path.parse(runtime).root.slice(0, 2) + "fake runtime",
    });
    assert.equal(driveRelative.status, 1);
    assert.match(driveRelative.stderr, /was not found on PATH/);
    const old = run(["--check", "--root", checkout], {
      ...env,
      FIXTURE_NODE_VERSION: "v22.17.0",
    });
    assert.equal(old.status, 1);
    assert.match(old.stderr, /22\.18\.0\+ is required/);
    assert.equal(fs.existsSync(argumentLog), false);
    const malformed = run(["--check", "--root", checkout], {
      ...env,
      FIXTURE_NODE_VERSION: "invalid version",
    });
    assert.equal(malformed.status, 1);
    assert.match(malformed.stderr, /Observed: invalid version/);
    const check = run(["--check", "--root", checkout], env);
    assert.equal(check.status, 0, check.stdout + check.stderr);
    assert.deepEqual(
      fs.readFileSync(argumentLog, "utf8").trim().split(/\r?\n/),
      [path.join(checkout, "moorkyz"), "launch", "--check"],
    );
    const launch = run(["--root", checkout], env);
    assert.equal(launch.status, 0, launch.stdout + launch.stderr);
    assert.deepEqual(
      fs.readFileSync(argumentLog, "utf8").trim().split(/\r?\n/),
      [path.join(checkout, "moorkyz"), "launch"],
    );
    const failedStartup = run(["--root", checkout, "--check"], {
      ...env,
      FIXTURE_EXIT_CODE: "7",
    });
    assert.equal(
      failedStartup.status,
      7,
      "preserve shared startup failure code",
    );

    const localExe = path.join(
      checkout,
      "src",
      ".moorkyz",
      "app",
      "Moorkyz.exe",
    );
    fs.mkdirSync(path.dirname(localExe), { recursive: true });
    fs.copyFileSync(output, localExe);
    const discovered = spawnSync(localExe, ["--check"], {
      cwd: os.tmpdir(),
      env,
      encoding: "utf8",
      timeout: 15000,
    });
    assert.equal(discovered.status, 0, discovered.stdout + discovered.stderr);
    assert.deepEqual(
      fs.readFileSync(argumentLog, "utf8").trim().split(/\r?\n/),
      [path.join(checkout, "moorkyz"), "launch", "--check"],
    );
  },
);

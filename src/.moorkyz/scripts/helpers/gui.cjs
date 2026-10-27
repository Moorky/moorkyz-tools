"use strict";

const fs = require("node:fs");
const path = require("node:path");
const { spawnSync } = require("node:child_process");

function guiInvocation({ platform, root, entrypoint, node, config }) {
  if (platform !== "win32")
    throw new Error(
      "The Moorkyz control GUI currently requires Windows with Windows PowerShell and WPF. CLI commands remain available on other platforms.",
    );
  const script = path.resolve(
    __dirname,
    "..",
    "..",
    "..",
    ".moorkyz",
    "gui",
    "start.ps1",
  );
  if (!fs.existsSync(script))
    throw new Error(`Moorkyz GUI source is missing: ${script}`);
  return {
    command: path.join(
      process.env.SystemRoot || "C:\\Windows",
      "System32",
      "WindowsPowerShell",
      "v1.0",
      "powershell.exe",
    ),
    args: [
      "-NoLogo",
      "-NoProfile",
      "-STA",
      "-File",
      script,
      "-Root",
      root,
      "-EntryPoint",
      entrypoint,
      "-NodePath",
      node,
      "-ProjectData",
      Buffer.from(
        JSON.stringify({
          names: Object.keys(config.projects),
          defaultProject: config.defaultProject,
        }),
      ).toString("base64"),
    ],
  };
}

function openGui(options) {
  const { command, args } = guiInvocation(options);
  const result = spawnSync(command, args, {
    cwd: options.root,
    stdio: "inherit",
  });
  if (result.error)
    throw new Error(`Could not open the Moorkyz GUI: ${result.error.message}`);
  return result.status ?? 1;
}

module.exports = { guiInvocation, openGui };

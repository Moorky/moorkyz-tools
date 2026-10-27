import { defineConfig } from "@playwright/test";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const standaloneRoot = path.resolve(
  repositoryRoot,
  "src/packages/portal/.next/standalone",
);

function configuredPort(name, fallback) {
  const value = process.env[name] ?? fallback;
  if (!/^\d{1,5}$/.test(value)) {
    throw new Error(`${name} must be a valid TCP port.`);
  }
  const port = Number(value);
  if (port < 1 || port > 65_535) {
    throw new Error(`${name} must be a valid TCP port.`);
  }
  return port;
}

const portalPort = configuredPort(
  "TOOLS_MOORKYZ_COM_PORTAL_PORT",
  process.env.PORTAL_TEST_PORT ?? "4174",
);

export default defineConfig({
  testDir: "./tests/browser",
  testMatch: "**/*.spec.js",
  testIgnore: [
    "**/local-service.spec.js",
    "**/directory-cleaner-service.spec.js",
  ],
  fullyParallel: true,
  workers: process.env.CI ? 3 : 1,
  reporter: [
    ["list"],
    [
      "json",
      {
        outputFile: path.join(
          repositoryRoot,
          "src",
          "test-results",
          "browser-results.json",
        ),
      },
    ],
  ],
  projects: [
    { name: "chromium", use: { browserName: "chromium" } },
    { name: "firefox", use: { browserName: "firefox" } },
    { name: "webkit", use: { browserName: "webkit" } },
  ],
  use: {
    baseURL: `http://127.0.0.1:${portalPort}`,
    browserName: "chromium",
    headless: true,
  },
  webServer: [
    {
      command: "node src/packages/portal/server.js",
      url: `http://127.0.0.1:${portalPort}`,
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
      cwd: standaloneRoot,
      env: {
        HOSTNAME: "127.0.0.1",
        NODE_ENV: "production",
        PORT: String(portalPort),
        NEXT_TELEMETRY_DISABLED: "1",
      },
    },
  ],
});

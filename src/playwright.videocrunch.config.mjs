import { defineConfig } from "@playwright/test";
import base, { repositoryRoot } from "./playwright.config.mjs";

export default defineConfig({
  ...base,
  testMatch: "**/local-service.spec.js",
  testIgnore: [],
  workers: 1,
  globalTeardown: "../tools/videocrunch/test/browser-teardown.js",
  webServer: [
    {
      ...base.webServer[0],
      reuseExistingServer: false,
      env: {
        ...base.webServer[0].env,
        TOOLS_MOORKYZ_COM_VIDEOCRUNCH_ENABLED: "1",
        TOOLS_MOORKYZ_COM_VIDEOCRUNCH_PORT: "8876",
      },
    },
    {
      command: "node tools/videocrunch/test/browser-fixture.js",
      cwd: repositoryRoot,
      url: "http://127.0.0.1:8876/api/config",
      reuseExistingServer: false,
      timeout: 30000,
    },
  ],
});

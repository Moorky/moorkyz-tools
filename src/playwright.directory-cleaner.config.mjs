import { defineConfig } from "@playwright/test";
import base, { repositoryRoot } from "./playwright.config.mjs";

export default defineConfig({
  ...base,
  testMatch: "**/directory-cleaner-service.spec.js",
  testIgnore: [],
  workers: 1,
  webServer: [
    {
      ...base.webServer[0],
      reuseExistingServer: false,
      env: {
        ...base.webServer[0].env,
        TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_ENABLED: "1",
        TOOLS_MOORKYZ_COM_DIRECTORY_CLEANER_PORT: "8878",
      },
    },
    {
      command: "node tools/directory-cleaner/server.mjs --port 8878",
      cwd: repositoryRoot,
      url: "http://127.0.0.1:8878/api/config",
      reuseExistingServer: false,
      timeout: 30000,
    },
  ],
});

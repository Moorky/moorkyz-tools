import { cp, mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const portalDirectory = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
);
const standaloneDirectory = path.resolve(
  portalDirectory,
  ".next/standalone/src/packages/portal",
);

await mkdir(path.join(standaloneDirectory, ".next"), { recursive: true });
await cp(
  path.join(portalDirectory, ".next/static"),
  path.join(standaloneDirectory, ".next/static"),
  { recursive: true },
);
await cp(
  path.join(portalDirectory, "public"),
  path.join(standaloneDirectory, "public"),
  { recursive: true },
);

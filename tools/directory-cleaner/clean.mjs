import * as fs from "node:fs/promises";
import { createPlan, applyPlan } from "./core.mjs";

async function run() {
  const args = process.argv.slice(2);
  if (args.length === 0 || args[0] === "--help") {
    console.log(
      'Moorky Directory Cleaner (Node.js 22+)\nPreview only: node clean.mjs --settings settings.json --plan preview.json\nApply preview: node clean.mjs --apply preview.json --confirm "APPLY <preview-id>"\nDelete always archives; originals are never overwritten. A plan file is created exclusively, not replaced.',
    );
    return;
  }
  const values = new Map();
  for (let index = 0; index < args.length; index += 2) {
    if (
      !["--settings", "--plan", "--apply", "--confirm"].includes(args[index]) ||
      !args[index + 1] ||
      values.has(args[index])
    )
      throw new Error("Unknown, duplicate or missing argument. Use --help.");
    values.set(args[index], args[index + 1]);
  }
  if (values.has("--settings") && values.has("--plan") && values.size === 2) {
    const plan = await createPlan(
      JSON.parse(await fs.readFile(values.get("--settings"), "utf8")),
    );
    await fs.writeFile(values.get("--plan"), JSON.stringify(plan, null, 2), {
      flag: "wx",
    });
    console.log(
      `Preview only: ${plan.entries.length} files, ${plan.entries.filter((entry) => entry.action !== "keep").length} actions.\nApply only after reviewing the plan:\nnode clean.mjs --apply "${values.get("--plan")}" --confirm "APPLY ${plan.id}"`,
    );
  } else if (
    values.has("--apply") &&
    values.has("--confirm") &&
    values.size === 2
  ) {
    const plan = JSON.parse(await fs.readFile(values.get("--apply"), "utf8"));
    const result = await applyPlan(plan, values.get("--confirm"));
    console.log(JSON.stringify(result, null, 2));
    if (result.status !== "completed") process.exitCode = 1;
  } else
    throw new Error(
      "Choose preview (--settings/--plan) or apply (--apply/--confirm), never both.",
    );
}

run().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});

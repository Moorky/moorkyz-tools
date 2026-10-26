import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";

const paths = execFileSync(
  "git",
  ["ls-files", "--cached", "--others", "--exclude-standard", "-z"],
  { encoding: "utf8" },
)
  .split("\0")
  .filter(Boolean);
const findings = [];
const patterns = [
  /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/,
  /\bAKIA[A-Z0-9]{16}\b/,
  /\bgh[pousr]_[A-Za-z0-9]{36,255}\b/,
  /\bgithub_pat_[A-Za-z0-9_]{70,255}\b/,
];
for (const path of paths) {
  if (
    !/\.(?:[cm]?[jt]sx?|ps1|json|ya?ml|md|html|css|cmd|sh|cs|xaml|csproj|slnx?|props|targets)$/.test(
      path,
    ) &&
    !/(?:^|\/)(?:Dockerfile|LICENSE)$/.test(path)
  )
    continue;
  const text = readFileSync(path, "utf8");
  for (const [index, line] of text.split(/\r?\n/).entries()) {
    if (patterns.some((pattern) => pattern.test(line))) {
      findings.push(
        `${path}:${index + 1}: possible credential (value redacted)`,
      );
    }
  }
  if (
    /^(?:packages\/portal\/src\/|tools\/directory-cleaner\/).*\.(?:ts|tsx|mjs)$/.test(
      path,
    )
  ) {
    if (
      /\beval\s*\(|\bnew Function\s*\(|\.innerHTML\s*=|\.outerHTML\s*=/.test(
        text,
      )
    ) {
      findings.push(`${path}: forbidden executable/HTML sink`);
    }
  }
}
if (findings.length > 0) {
  console.error(findings.join("\n"));
  process.exitCode = 1;
} else {
  console.log(
    `PASS: ${paths.length} repository paths checked for selected credential patterns; portal/cleaner source has no eval/HTML assignment sinks. This bounded scan is not a complete secret or security audit.`,
  );
}

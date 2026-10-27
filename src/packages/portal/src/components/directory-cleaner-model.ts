export type CleanerAction = "keep" | "move" | "archive";
export interface RuleDraft {
  readonly action: CleanerAction;
  readonly pattern: string;
  readonly destination: string;
  readonly createdBefore: string;
  readonly createdAfter: string;
  readonly smallerThan: string;
  readonly largerThan: string;
  readonly unit: "B" | "KiB" | "MiB" | "GiB";
}
export interface CleanerConfiguration {
  readonly version: 1;
  readonly sourceDir: string;
  readonly archiveDir?: string;
  readonly recursive: boolean;
  readonly rules: readonly {
    readonly action: CleanerAction;
    readonly pattern: string;
    readonly destination?: string;
    readonly createdBefore?: string;
    readonly createdAfter?: string;
    readonly smallerThanBytes?: number;
    readonly largerThanBytes?: number;
  }[];
}
export const EMPTY_RULE: RuleDraft = {
  action: "keep",
  pattern: "*",
  destination: "",
  createdBefore: "",
  createdAfter: "",
  smallerThan: "",
  largerThan: "",
  unit: "MiB",
};

export function createCleanerConfiguration(
  sourceDir: string,
  archiveDir: string,
  recursive: boolean,
  drafts: readonly RuleDraft[],
): CleanerConfiguration {
  if (!sourceDir.trim())
    throw new Error(
      "Choose an absolute source folder before previewing or downloading settings.",
    );
  const units = { B: 1, KiB: 1024, MiB: 1048576, GiB: 1073741824 };
  function bytes(text: string, unit: RuleDraft["unit"]): number | undefined {
    if (text.trim() === "") return undefined;
    const value = Number(text) * units[unit];
    if (!Number.isSafeInteger(value) || value < 0)
      throw new Error("Size limits must resolve to non-negative whole bytes.");
    return value;
  }
  if (drafts.length === 0 || drafts.length > 100)
    throw new Error("Use between 1 and 100 rules.");
  return {
    version: 1,
    sourceDir: sourceDir.trim(),
    ...(archiveDir.trim() ? { archiveDir: archiveDir.trim() } : {}),
    recursive,
    rules: drafts.map((rule) => {
      if (!rule.pattern.trim())
        throw new Error(
          "Every rule needs a filename or relative-path pattern.",
        );
      if (rule.action === "move" && !rule.destination.trim())
        throw new Error("Each Move rule needs a destination folder.");
      const smaller = bytes(rule.smallerThan, rule.unit),
        larger = bytes(rule.largerThan, rule.unit);
      if (smaller !== undefined && larger !== undefined && smaller <= larger)
        throw new Error(
          "Smaller-than must exceed larger-than when both size bounds are set.",
        );
      if (
        rule.createdAfter &&
        rule.createdBefore &&
        rule.createdAfter >= rule.createdBefore
      )
        throw new Error(
          "Created-after must precede created-before when both dates are set.",
        );
      return {
        action: rule.action,
        pattern: rule.pattern.trim(),
        ...(rule.action === "move"
          ? { destination: rule.destination.trim() }
          : {}),
        ...(rule.createdBefore ? { createdBefore: rule.createdBefore } : {}),
        ...(rule.createdAfter ? { createdAfter: rule.createdAfter } : {}),
        ...(smaller !== undefined ? { smallerThanBytes: smaller } : {}),
        ...(larger !== undefined ? { largerThanBytes: larger } : {}),
      };
    }),
  };
}

function record(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}
const isId = (value: unknown): value is string =>
  typeof value === "string" && /^[0-9a-f-]{36}$/.test(value);
const isAction = (value: unknown): value is CleanerAction =>
  value === "keep" || value === "move" || value === "archive";

export interface CleanerPlan {
  readonly id: string;
  readonly configuration: {
    readonly sourceDir: string;
    readonly archiveDir: string;
  };
  readonly entries: readonly {
    readonly relativePath: string;
    readonly bytes: number;
    readonly createdMs: number;
    readonly action: CleanerAction;
    readonly reason: string;
    readonly destination?: string;
  }[];
  readonly skipped: readonly {
    readonly relativePath: string;
    readonly reason: string;
  }[];
}
export function isCleanerPlan(value: unknown): value is CleanerPlan {
  return (
    record(value) &&
    value.version === 1 &&
    isId(value.id) &&
    record(value.configuration) &&
    typeof value.configuration.sourceDir === "string" &&
    typeof value.configuration.archiveDir === "string" &&
    Array.isArray(value.entries) &&
    value.entries.length <= 10000 &&
    value.entries.every(
      (entry: unknown) =>
        record(entry) &&
        typeof entry.relativePath === "string" &&
        typeof entry.bytes === "number" &&
        Number.isSafeInteger(entry.bytes) &&
        entry.bytes >= 0 &&
        typeof entry.createdMs === "number" &&
        Number.isFinite(new Date(entry.createdMs).getTime()) &&
        isAction(entry.action) &&
        typeof entry.reason === "string" &&
        (entry.destination === undefined ||
          typeof entry.destination === "string") &&
        (entry.action !== "move" || typeof entry.destination === "string"),
    ) &&
    Array.isArray(value.skipped) &&
    value.skipped.every(
      (entry: unknown) =>
        record(entry) &&
        typeof entry.relativePath === "string" &&
        typeof entry.reason === "string",
    )
  );
}
export function cleanerPreviewTarget(
  plan: CleanerPlan,
  entry: CleanerPlan["entries"][number],
): string {
  if (entry.action === "keep") return "Unchanged";
  const root =
    entry.action === "archive"
      ? plan.configuration.archiveDir
      : entry.destination;
  if (root === undefined) throw new Error("Move preview has no destination.");
  const separator = plan.configuration.sourceDir.includes("\\") ? "\\" : "/";
  return [
    root,
    ...(entry.action === "archive" ? [plan.id, "files"] : []),
    ...entry.relativePath.split("/"),
  ].join(separator);
}
export interface CleanerRun {
  readonly id: string;
  readonly status: "running" | "completed" | "partial" | "failed";
  readonly error: string | null;
  readonly result: null | {
    readonly status: "completed" | "partial" | "failed";
    readonly completed: readonly {
      readonly relativePath: string;
      readonly target: string;
    }[];
    readonly failed: readonly {
      readonly relativePath: string;
      readonly error: string;
    }[];
    readonly receiptPath: string | null;
    readonly pending: number;
  };
}
export function isCleanerRun(value: unknown): value is CleanerRun {
  if (
    !record(value) ||
    !isId(value.id) ||
    !["running", "completed", "partial", "failed"].includes(
      String(value.status),
    ) ||
    (value.error !== null && typeof value.error !== "string")
  )
    return false;
  if (value.result === null)
    return (
      value.status === "running" ||
      (value.status === "failed" && typeof value.error === "string")
    );
  const result = value.result;
  return (
    record(result) &&
    ["completed", "partial", "failed"].includes(String(result.status)) &&
    result.status === value.status &&
    typeof result.pending === "number" &&
    Number.isSafeInteger(result.pending) &&
    result.pending >= 0 &&
    Array.isArray(result.completed) &&
    result.completed.every(
      (entry: unknown) =>
        record(entry) &&
        typeof entry.relativePath === "string" &&
        typeof entry.target === "string",
    ) &&
    Array.isArray(result.failed) &&
    (result.status !== "completed" || result.failed.length === 0) &&
    result.failed.every(
      (entry: unknown) =>
        record(entry) &&
        typeof entry.relativePath === "string" &&
        typeof entry.error === "string",
    ) &&
    (result.receiptPath === null || typeof result.receiptPath === "string")
  );
}
export function isCleanerConnection(
  value: unknown,
): value is { token: string; activeRunId: string | null } {
  return (
    record(value) &&
    value.version === 1 &&
    value.deleteBehavior === "archive" &&
    typeof value.token === "string" &&
    /^[0-9a-f]{64}$/.test(value.token) &&
    (value.activeRunId === null || isId(value.activeRunId))
  );
}
export async function cleanerRequest(
  resource: string,
  signal: AbortSignal | null,
  token = "",
  body?: unknown,
): Promise<unknown> {
  const response = await fetch(
    `/api/local-tools/directory-cleaner/api/${resource}`,
    {
      method: body === undefined ? "GET" : "POST",
      signal,
      cache: "no-store",
      headers: {
        "Content-Type": "application/json",
        "X-Directory-Cleaner-Token": token,
      },
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    },
  );
  const data: unknown = await response.json();
  if (!response.ok)
    throw new Error(
      record(data) && typeof data.error === "string"
        ? data.error
        : `Moorkyz Directory Cleaner request failed (${response.status}).`,
    );
  return data;
}

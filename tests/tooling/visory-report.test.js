import assert from "node:assert/strict";
import { test } from "node:test";
import {
  summarize,
  performance,
  groupDays,
  filterRows,
  validateRange,
} from "../../tools/visory-media-downloader/src/utils/documents/report.mjs";

const row = (id, day, status, seconds, size, message = "") =>
  [
    id,
    `${day} 12:00:00`,
    status,
    `${id}.mp4`,
    "",
    seconds,
    size,
    "",
    "",
    "",
    "",
    message,
  ].map(String);
const rows = [
  row("fast", "2026-10-25", "SUCCESS", "1", "1048576"),
  row("slow", "2026-10-26", "SUCCESS", "6", "2097152"),
  row("zero", "2026-10-26", "SUCCESS", "", "0"),
  row("fail", "2026-10-27", "FAIL", "100", "", "Camera offline"),
  row("error", "2026-10-27", "ERROR", "", "", "disk full"),
];

test("Visory performance uses size-weighted successful all-time measurements only", () => {
  const baseline = summarize(rows);
  assert.equal(baseline.secondsPerMiB, 7 / 3);
  assert.equal(baseline.miBPerSecond, 3 / 7);
  assert.equal(baseline.successRatio, "60.00%");
  assert.equal(performance(rows[0], baseline).comparison, "better");
  assert.equal(performance(rows[1], baseline).comparison, "worse");
  assert.equal(performance(rows[2], baseline), null);
  assert.equal(performance(rows[3], baseline), null);
  assert.equal(summarize([]).secondsPerMiB, null);
  const measuredZero = summarize([
    row("z", "2026-10-27", "SUCCESS", "0", "1048576"),
  ]);
  assert.equal(measuredZero.secondsPerMiB, 0);
  assert.equal(measuredZero.miBPerSecond, null);
  assert.equal(performance(rows[0], measuredZero).comparison, "worse");
});

test("Visory search covers the full data, including rows not yet displayed", () => {
  assert.deepEqual(filterRows(rows, "CAMERA fail"), [rows[3]]);
  assert.deepEqual(filterRows(rows, "disk"), [rows[4]]);
  assert.deepEqual(filterRows(rows, "unknown"), []);
  assert.deepEqual(filterRows(rows, "", "2026-10-26", "2026-10-26"), [
    rows[1],
    rows[2],
  ]);
  assert.equal(
    summarize(rows).secondsPerMiB,
    7 / 3,
    "filtered selection never mutates all-time baseline",
  );
});

test("Visory statistics aggregate per day and reject out-of-snapshot or reversed ranges", () => {
  const days = groupDays(rows);
  assert.equal(days.length, 3);
  assert.equal(days[0].day, "2026-10-27");
  assert.equal(days[0].fail, 1);
  assert.equal(days[0].error, 1);
  assert.equal(days[0].successRatio, "0.00%");
  assert.equal(
    validateRange("2026-10-25", "2026-10-27", "2026-10-25", "2026-10-27"),
    "",
  );
  for (const [from, to] of [
    ["2026-10-24", "2026-10-27"],
    ["2026-10-25", "2026-10-28"],
    ["2026-10-27", "2026-10-25"],
    ["", ""],
  ])
    assert.notEqual(validateRange(from, to, "2026-10-25", "2026-10-27"), "");
  assert.notEqual(validateRange("", "", "", ""), "");
  assert.notEqual(
    validateRange("2026-02-31", "2026-03-01", "2026-01-01", "2026-12-31"),
    "",
  );
});

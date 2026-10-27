export const batchSize = 50;
export const taskColumns = [
  "ID",
  "Timestamp",
  "Status",
  "Filename",
  "Camera",
  "Full Path",
  "Time Spent (s)",
  "File Size (bytes)",
  "Length (s)",
  "Log",
  "Error",
  "Success Ratio",
  "Message",
];
export const statisticColumns = [
  "Day",
  "Outcomes",
  "SUCCESS",
  "FAIL",
  "ERROR",
  "Success Ratio",
  "Known Time (s)",
  "Successful Size (bytes)",
  "Known Length (s)",
];
const mib = 1_048_576;
const number = (value) =>
  value === "" || value == null ? null : Number(value);
const finite = (value) => Number.isFinite(value) && value >= 0;
const ratio = (success, total) =>
  total ? `${((100 * success) / total).toFixed(2)}%` : "";

export function summarize(rows) {
  const result = {
    total: rows.length,
    success: 0,
    fail: 0,
    error: 0,
    seconds: 0,
    bytes: 0,
    length: 0,
    measuredBytes: 0,
    measuredSeconds: 0,
  };
  for (const row of rows) {
    if (row[2] === "SUCCESS") result.success++;
    else if (row[2] === "FAIL") result.fail++;
    else if (row[2] === "ERROR") result.error++;
    const seconds = number(row[6]),
      bytes = number(row[7]),
      length = number(row[8]);
    if (finite(seconds)) result.seconds += seconds;
    if (finite(length)) result.length += length;
    if (row[2] === "SUCCESS" && finite(bytes)) result.bytes += bytes;
    if (row[2] === "SUCCESS" && finite(seconds) && finite(bytes) && bytes > 0) {
      result.measuredSeconds += seconds;
      result.measuredBytes += bytes;
    }
  }
  result.successRatio = ratio(result.success, result.total);
  result.secondsPerMiB =
    result.measuredBytes > 0
      ? result.measuredSeconds / (result.measuredBytes / mib)
      : null;
  result.miBPerSecond =
    result.measuredSeconds > 0
      ? result.measuredBytes / mib / result.measuredSeconds
      : null;
  return result;
}

export function performance(row, baseline) {
  const seconds = number(row[6]),
    bytes = number(row[7]);
  if (row[2] !== "SUCCESS" || !finite(seconds) || !finite(bytes) || bytes <= 0)
    return null;
  const secondsPerMiB = seconds / (bytes / mib);
  const miBPerSecond = seconds > 0 ? bytes / mib / seconds : null;
  const reference = baseline.secondsPerMiB;
  return {
    secondsPerMiB,
    miBPerSecond,
    comparison:
      reference == null || Math.abs(secondsPerMiB - reference) <= 1e-9
        ? "neutral"
        : secondsPerMiB < reference
          ? "better"
          : "worse",
    differencePercent:
      reference > 0 ? (secondsPerMiB / reference - 1) * 100 : null,
  };
}

export function filterRows(rows, query = "", from = "", to = "") {
  const words = query.trim().toLocaleLowerCase().split(/\s+/).filter(Boolean);
  return rows.filter(
    (row) =>
      (!from || row[1].slice(0, 10) >= from) &&
      (!to || row[1].slice(0, 10) <= to) &&
      words.every((word) => row.join(" ").toLocaleLowerCase().includes(word)),
  );
}

export function groupDays(rows) {
  const groups = new Map();
  for (const row of rows) {
    const day = row[1].slice(0, 10);
    if (!groups.has(day)) groups.set(day, []);
    groups.get(day).push(row);
  }
  return [...groups]
    .sort(([a], [b]) => b.localeCompare(a))
    .map(([day, values]) => ({ day, ...summarize(values) }));
}

export function validateRange(from, to, min, max) {
  const date = /^\d{4}-\d{2}-\d{2}$/;
  if (!min || !max) return "There are no recorded dates to select.";
  const valid = (value) =>
    date.test(value) &&
    Number.isFinite(Date.parse(value + "T00:00:00Z")) &&
    new Date(value + "T00:00:00Z").toISOString().slice(0, 10) === value;
  if (!valid(from) || !valid(to)) return "Choose both From and To dates.";
  if (from < min || to > max || from > max || to < min)
    return `Choose dates between ${min} and ${max}.`;
  if (from > to) return "From must be on or before To.";
  return "";
}

function init() {
  const data = document.querySelector("#report-data");
  if (!data) return;
  const rows = JSON.parse(data.textContent);
  if (
    !Array.isArray(rows) ||
    rows.some(
      (row) =>
        !Array.isArray(row) ||
        row.length !== taskColumns.length ||
        row.some((cell) => typeof cell !== "string") ||
        !["SUCCESS", "FAIL", "ERROR"].includes(row[2]),
    )
  ) {
    throw new Error("The embedded report data is invalid.");
  }
  const search = document.querySelector("#report-search");
  const views = [...document.querySelectorAll('input[name="report-view"]')];
  const mode = () => views.find((input) => input.checked).value;
  const metrics = document.querySelector("#report-performance");
  const dates = document.querySelector("#report-dates");
  const from = document.querySelector("#report-from"),
    to = document.querySelector("#report-to");
  const error = document.querySelector("#report-error");
  const scroll = document.querySelector("#report-scroll");
  const table = document.querySelector("#report-table");
  const head = table.querySelector("thead"),
    body = table.querySelector("tbody");
  const count = document.querySelector("#report-count");
  const more = document.querySelector("#report-more");
  const totals = document.querySelector("#report-totals");
  const baseline = summarize(rows);
  const recorded = rows.map((row) => row[1].slice(0, 10)).sort();
  const min = recorded[0] ?? "",
    max = recorded.at(-1) ?? "";
  let visibleRows = [],
    shown = 0,
    comparing = false;
  const precise = (value) => (value == null ? "" : value.toFixed(3));
  const add = (tag, text, parent, className = "") => {
    const element = document.createElement(tag);
    element.textContent = text;
    if (className) element.className = className;
    parent.append(element);
    return element;
  };
  function reportLink(cell, value) {
    if (!value) return;
    if (
      !/^\.\.\/runs\/(?:[A-Za-z0-9._%~-]+\/)+[A-Za-z0-9._%~-]+\.md#L[1-9]\d*$/.test(
        value,
      )
    )
      throw new Error("A report log link is unsafe.");
    const [file, line] = value.split("#L");
    const label = `${decodeURIComponent(file.split("/").at(-1))} : line ${line}`;
    add("a", label, cell).setAttribute("href", value);
  }
  function metricCell(parent, value, comparison, label) {
    const cell = add(
      "td",
      value,
      parent,
      comparison === "neutral" ? "mono" : `mono metric-${comparison}`,
    );
    cell.title = label;
    return cell;
  }
  function comparisonLabel(metric) {
    if (!metric) return "Unknown: no measured successful size/time pair";
    if (metric.comparison === "neutral")
      return "Same as all-time weighted average";
    const delta = metric.differencePercent;
    return delta == null
      ? "Slower than the zero-second all-time average"
      : `${Math.abs(delta).toFixed(2)}% ${delta < 0 ? "faster" : "slower"} than all-time weighted average`;
  }
  function renderTask(row) {
    const tr = add("tr", "", body);
    tr.id = `result-${row[0]}`;
    const metric = comparing ? performance(row, baseline) : null;
    for (let index = 0; index < row.length; index++) {
      if (comparing && index === 7) {
        metricCell(
          tr,
          precise(metric?.secondsPerMiB),
          metric?.comparison ?? "neutral",
          comparisonLabel(metric),
        );
        continue;
      }
      if (comparing && index === 8) {
        metricCell(
          tr,
          precise(metric?.miBPerSecond),
          metric?.comparison ?? "neutral",
          comparisonLabel(metric),
        );
        continue;
      }
      if (comparing && index === 11) {
        metricCell(
          tr,
          comparisonLabel(metric),
          metric?.comparison ?? "neutral",
          "Compared with all recorded successful size/time pairs, independent of filters.",
        );
        continue;
      }
      const cell = add(
        "td",
        "",
        tr,
        index <= 1
          ? "mono"
          : index === 5
            ? "path"
            : index === 12
              ? "message"
              : "",
      );
      if (index === 9 || index === 10) reportLink(cell, row[index]);
      else if (index === 2)
        add(
          "span",
          row[index],
          cell,
          `status status-${row[index].toLowerCase()}`,
        );
      else {
        cell.textContent = row[index];
        if (index === 12 && row[2] !== "SUCCESS")
          cell.classList.add("error-message");
      }
    }
  }
  function renderStatistics(day) {
    const tr = add("tr", "", body);
    tr.id = `day-${day.day}`;
    const values = [
      day.day,
      day.total,
      day.success,
      day.fail,
      day.error,
      day.successRatio,
      precise(day.seconds),
      String(day.bytes),
      precise(day.length),
    ];
    for (const [index, value] of values.entries()) {
      if (comparing && index === 7) {
        const comparison =
          day.secondsPerMiB == null ||
          baseline.secondsPerMiB == null ||
          Math.abs(day.secondsPerMiB - baseline.secondsPerMiB) <= 1e-9
            ? "neutral"
            : day.secondsPerMiB < baseline.secondsPerMiB
              ? "better"
              : "worse";
        metricCell(
          tr,
          precise(day.secondsPerMiB),
          comparison,
          "Weighted seconds per MiB compared with all-time successful measurements.",
        );
      } else if (comparing && index === 8) {
        const comparison =
          day.miBPerSecond == null ||
          baseline.miBPerSecond == null ||
          Math.abs(day.miBPerSecond - baseline.miBPerSecond) <= 1e-9
            ? "neutral"
            : day.miBPerSecond > baseline.miBPerSecond
              ? "better"
              : "worse";
        metricCell(
          tr,
          precise(day.miBPerSecond),
          comparison,
          "Weighted MiB per second compared with all-time successful measurements.",
        );
      } else
        add(
          "td",
          String(value),
          tr,
          index === 0
            ? "mono"
            : index === 2
              ? "metric-better"
              : index === 3
                ? "error-message"
                : index === 4
                  ? "status status-error"
                  : "",
        );
    }
  }
  function appendBatch() {
    const end = Math.min(shown + batchSize, visibleRows.length);
    for (; shown < end; shown++) {
      if (mode() === "tasks") renderTask(visibleRows[shown]);
      else renderStatistics(visibleRows[shown]);
    }
    const noun = mode() === "tasks" ? "tasks" : "days";
    count.textContent = visibleRows.length
      ? `Showing ${shown} of ${visibleRows.length} ${noun}.`
      : `No matching ${noun}.`;
    more.hidden = shown >= visibleRows.length;
    more.disabled = more.hidden;
  }
  function refresh() {
    const statistics = mode() === "statistics";
    table.caption.textContent = statistics
      ? "Daily statistics for selected outcomes"
      : "Recorded outcomes";
    dates.hidden = !statistics;
    const invalid =
      statistics && rows.length
        ? validateRange(from.value, to.value, min, max)
        : "";
    error.textContent = invalid;
    from.setAttribute("aria-invalid", invalid ? "true" : "false");
    to.setAttribute("aria-invalid", invalid ? "true" : "false");
    from.setCustomValidity(invalid);
    to.setCustomValidity(invalid);
    const filtered = invalid
      ? []
      : filterRows(
          rows,
          search.value,
          statistics ? from.value : "",
          statistics ? to.value : "",
        );
    const summary = summarize(filtered);
    totals.replaceChildren();
    for (const [label, value, className] of [
      ["SUCCESS", summary.success, "metric-better"],
      ["FAIL", summary.fail, "error-message"],
      ["ERROR", summary.error, "status status-error"],
      ["Success ratio", summary.successRatio || "N/A", ""],
      ["Known time", `${precise(summary.seconds)} s`, ""],
    ])
      add("li", `${label}: ${value}`, totals, className);
    const columns = [...(statistics ? statisticColumns : taskColumns)];
    if (comparing) {
      columns[7] = "Seconds / MiB";
      columns[8] = "MiB / second";
      if (!statistics) columns[11] = "vs all-time average";
    }
    head.replaceChildren();
    const header = add("tr", "", head);
    for (const label of columns)
      add("th", label, header).setAttribute("scope", "col");
    body.replaceChildren();
    scroll.scrollTop = 0;
    visibleRows = statistics ? groupDays(filtered) : filtered;
    shown = 0;
    appendBatch();
  }
  for (const input of [from, to]) {
    input.min = min;
    input.max = max;
    input.disabled = rows.length === 0;
  }
  from.value = min;
  to.value = max;
  document.querySelector("#report-available").textContent = min
    ? `Available: ${min} to ${max} (inclusive). Dates outside this snapshot are unavailable.`
    : "No recorded dates.";
  document.querySelector("#report-reset").disabled = rows.length === 0;
  document.querySelector("#report-reset").addEventListener("click", () => {
    from.value = min;
    to.value = max;
    refresh();
  });
  const calendar = document.querySelector("#report-calendar");
  const calendarDays = document.querySelector("#calendar-days");
  const previous = document.querySelector("#calendar-previous");
  const next = document.querySelector("#calendar-next");
  let dateTarget = from,
    month = min.slice(0, 7),
    calendarOpener = null;
  const iso = (date) => date.toISOString().slice(0, 10);
  function drawCalendar() {
    const first = new Date(month + "-01T00:00:00Z");
    document.querySelector("#calendar-month").textContent =
      new Intl.DateTimeFormat("en", {
        month: "long",
        year: "numeric",
        timeZone: "UTC",
      }).format(first);
    previous.disabled = month <= min.slice(0, 7);
    next.disabled = month >= max.slice(0, 7);
    calendarDays.replaceChildren();
    for (const label of ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"])
      add("span", label, calendarDays, "note");
    const start = first.getTime() - first.getUTCDay() * 86_400_000;
    for (let day = 0; day < 42; day++) {
      const date = new Date(start + day * 86_400_000),
        value = iso(date);
      const button = add("button", String(date.getUTCDate()), calendarDays);
      button.type = "button";
      button.setAttribute("aria-label", value);
      button.setAttribute("aria-pressed", String(value === dateTarget.value));
      button.disabled = value < min || value > max;
      if (value.slice(0, 7) !== month) button.classList.add("outside-month");
      button.addEventListener("click", () => {
        dateTarget.value = value;
        refresh();
        calendar.close();
      });
    }
  }
  for (const [target, buttonId, title] of [
    [from, "report-from-calendar", "From date calendar"],
    [to, "report-to-calendar", "To date calendar"],
  ]) {
    const button = document.querySelector("#" + buttonId);
    button.disabled = rows.length === 0;
    button.addEventListener("click", () => {
      dateTarget = target;
      calendarOpener = button;
      month = validateRange(target.value, target.value, min, max)
        ? min.slice(0, 7)
        : target.value.slice(0, 7);
      document.querySelector("#calendar-title").textContent = title;
      drawCalendar();
      calendar.showModal();
      (
        calendarDays.querySelector(
          'button[aria-pressed="true"]:not(:disabled)',
        ) ?? calendarDays.querySelector("button:not(:disabled)")
      )?.focus();
    });
  }
  for (const [button, direction] of [
    [previous, -1],
    [next, 1],
  ])
    button.addEventListener("click", () => {
      const date = new Date(month + "-01T00:00:00Z");
      date.setUTCMonth(date.getUTCMonth() + direction);
      month = iso(date).slice(0, 7);
      drawCalendar();
    });
  calendarDays.addEventListener("keydown", (event) => {
    const delta = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[
      event.key
    ];
    if (!delta || !(event.target instanceof HTMLButtonElement)) return;
    event.preventDefault();
    const buttons = [...calendarDays.querySelectorAll("button")];
    let index = buttons.indexOf(event.target) + delta;
    while (index >= 0 && index < buttons.length && buttons[index].disabled)
      index += Math.sign(delta);
    if (buttons[index] && !buttons[index].disabled) buttons[index].focus();
  });
  document
    .querySelector("#calendar-close")
    .addEventListener("click", () => calendar.close());
  calendar.addEventListener("close", () => calendarOpener?.focus());
  metrics.addEventListener("click", () => {
    comparing = !comparing;
    metrics.setAttribute("aria-pressed", String(comparing));
    document.querySelector("#report-baseline").hidden = !comparing;
    refresh();
  });
  document.querySelector("#report-baseline").textContent =
    `All-time weighted baseline: ${precise(baseline.secondsPerMiB) || "unknown"} s/MiB; ${precise(baseline.miBPerSecond) || "unknown"} MiB/s. ` +
    "Only SUCCESS rows with known time and positive size qualify. Green is faster, red is slower; equal or unknown is neutral. 1 MiB = 1,048,576 bytes. Filters never change the baseline.";
  search.addEventListener("input", refresh);
  for (const input of views) input.addEventListener("change", refresh);
  from.addEventListener("input", refresh);
  to.addEventListener("input", refresh);
  more.addEventListener("click", appendBatch);
  scroll.addEventListener(
    "scroll",
    () => {
      if (
        scroll.scrollTop > 0 &&
        scroll.scrollHeight - scroll.clientHeight - scroll.scrollTop < 160 &&
        shown < visibleRows.length
      )
        appendBatch();
    },
    { passive: true },
  );
  window.addEventListener("beforeprint", () => {
    while (shown < visibleRows.length) appendBatch();
  });
  document.querySelector("#report-controls").hidden = false;
  document.querySelector("#report-no-script").hidden = true;
  refresh();
}

if (typeof document !== "undefined") {
  try {
    init();
  } catch (error) {
    const notice = document.querySelector("#report-error");
    if (notice)
      notice.textContent = `The interactive report could not load: ${error.message}. Use the complete CSV or Markdown report.`;
    console.error(error);
  }
}

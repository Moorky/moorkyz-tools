"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { downloadJSON } from "./download-json";
import { usePortalSettings } from "./portal-shell";
import {
  EMPTY_RULE,
  cleanerRequest,
  cleanerPreviewTarget,
  createCleanerConfiguration,
  isCleanerConnection,
  isCleanerPlan,
  isCleanerRun,
  type CleanerPlan,
  type CleanerRun,
  type RuleDraft,
} from "./directory-cleaner-model";

export function DirectoryCleaner({ enabled }: { readonly enabled: boolean }) {
  const { themeReady } = usePortalSettings();
  const [sourceDir, setSourceDir] = useState("");
  const [archiveDir, setArchiveDir] = useState("");
  const [recursive, setRecursive] = useState(false);
  const [rules, setRules] = useState<RuleDraft[]>([
    { ...EMPTY_RULE, pattern: "*.pdf" },
    { ...EMPTY_RULE, action: "archive", pattern: "*.tmp", smallerThan: "1" },
  ]);
  const [token, setToken] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [plan, setPlan] = useState<CleanerPlan | null>(null);
  const [confirmation, setConfirmation] = useState("");
  const [run, setRun] = useState<CleanerRun | null>(null);
  const [retryPoll, setRetryPoll] = useState(0);
  const abort = useRef<AbortController | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    abort.current = controller;
    return () => controller.abort();
  }, []);
  const settings = useMemo(() => {
    try {
      return {
        value: createCleanerConfiguration(
          sourceDir,
          archiveDir,
          recursive,
          rules,
        ),
        error: "",
      };
    } catch (cause) {
      return {
        value: null,
        error: cause instanceof Error ? cause.message : "Invalid settings.",
      };
    }
  }, [sourceDir, archiveDir, recursive, rules]);
  useEffect(() => {
    setPlan(null);
    setConfirmation("");
  }, [settings]);
  const running = run?.status === "running";
  const runId = run?.id;
  useEffect(() => {
    if (!runId || !running || !token) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;
    async function poll() {
      try {
        const data = await cleanerRequest(
          `runs/${runId}`,
          controller.signal,
          token,
        );
        if (!isCleanerRun(data))
          throw new Error(
            "Cleanup returned an invalid result. Inspect the local service terminal and receipt.",
          );
        if (controller.signal.aborted) return;
        setRun(data);
        if (data.status === "running")
          timer = setTimeout(() => void poll(), 1000);
        else {
          setPlan(null);
          setConfirmation("");
        }
      } catch (cause) {
        if (!controller.signal.aborted)
          setError(
            cause instanceof Error
              ? cause.message
              : "Could not read cleanup status. Reconnect or check the receipt.",
          );
      }
    }
    void poll();
    return () => {
      controller.abort();
      if (timer !== undefined) clearTimeout(timer);
    };
  }, [runId, running, token, retryPoll]);

  function updateRule(index: number, value: Partial<RuleDraft>) {
    setRules((current) =>
      current.map((rule, item) =>
        item === index ? { ...rule, ...value } : rule,
      ),
    );
  }
  async function connect() {
    setBusy(true);
    setError("");
    try {
      const data = await cleanerRequest(
        "config",
        abort.current?.signal ?? null,
      );
      if (!isCleanerConnection(data))
        throw new Error(
          "Moorky Directory Cleaner returned an invalid configuration.",
        );
      if (abort.current?.signal.aborted) return;
      setToken(data.token);
      if (data.activeRunId)
        setRun({
          id: data.activeRunId,
          status: "running",
          error: null,
          result: null,
        });
      setNotice(
        "Connected to the local service. Preview does not change files.",
      );
      setRetryPoll((value) => value + 1);
    } catch (cause) {
      if (!abort.current?.signal.aborted)
        setError(
          cause instanceof Error
            ? cause.message
            : "Could not connect to the local service.",
        );
    } finally {
      if (!abort.current?.signal.aborted) setBusy(false);
    }
  }
  async function preview() {
    if (!settings.value) {
      setError(settings.error);
      return;
    }
    setBusy(true);
    setError("");
    setPlan(null);
    setRun(null);
    setConfirmation("");
    try {
      const data = await cleanerRequest(
        "preview",
        abort.current?.signal ?? null,
        token,
        settings.value,
      );
      if (!isCleanerPlan(data))
        throw new Error("The service returned an invalid preview.");
      if (!abort.current?.signal.aborted) {
        setPlan(data);
        setNotice("Preview only. No files or folders have changed.");
      }
    } catch (cause) {
      if (!abort.current?.signal.aborted)
        setError(cause instanceof Error ? cause.message : "Preview failed.");
    } finally {
      if (!abort.current?.signal.aborted) setBusy(false);
    }
  }
  async function apply() {
    if (!plan || confirmation !== `APPLY ${plan.id}`) {
      setError("Enter the exact confirmation for the displayed preview.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      const data = await cleanerRequest(
        "apply",
        abort.current?.signal ?? null,
        token,
        { planId: plan.id, confirmation },
      );
      if (!isCleanerRun(data))
        throw new Error(
          "Cleanup returned an invalid run. Check the service terminal and receipt before retrying.",
        );
      if (!abort.current?.signal.aborted) {
        setRun(data);
        setNotice(
          "Cleanup accepted. Closing this page stops status polling, not the confirmed run. Its receipt remains in the archive folder.",
        );
      }
    } catch (cause) {
      if (!abort.current?.signal.aborted)
        setError(
          cause instanceof Error
            ? cause.message
            : "Could not apply the preview. Check the service before retrying.",
        );
    } finally {
      if (!abort.current?.signal.aborted) setBusy(false);
    }
  }
  const changes =
    plan?.entries.filter((entry) => entry.action !== "keep").length ?? 0;
  return (
    <div className="directory-cleaner tool-form">
      <p>
        Keep what matters. Move what belongs elsewhere. Delete always means{" "}
        <strong>archive</strong>. Rules combine conditions with AND; Keep wins,
        otherwise the first match applies. Unmatched files stay untouched.
      </p>
      <fieldset disabled={busy || running || !themeReady}>
        <legend>Folders and rules</legend>
        <div className="cleaner-folders">
          <label>
            Source folder
            <input
              placeholder="C:\Users\YourName\Downloads"
              value={sourceDir}
              onChange={(event) => setSourceDir(event.currentTarget.value)}
            />
          </label>
          <label>
            Archive folder (optional)
            <input
              placeholder="Default: .directory-cleaner-archive inside source"
              value={archiveDir}
              onChange={(event) => setArchiveDir(event.currentTarget.value)}
            />
          </label>
        </div>
        <label className="checkbox-label">
          <input
            type="checkbox"
            checked={recursive}
            onChange={(event) => setRecursive(event.currentTarget.checked)}
          />
          Include subfolders
        </label>
        {rules.map((rule, index) => (
          <fieldset className="cleaner-rule" key={index}>
            <legend>Rule {index + 1}</legend>
            <div className="cleaner-rule-main">
              <label>
                Action
                <select
                  value={rule.action}
                  onChange={(event) =>
                    updateRule(index, {
                      action:
                        event.currentTarget.value === "move"
                          ? "move"
                          : event.currentTarget.value === "archive"
                            ? "archive"
                            : "keep",
                    })
                  }
                >
                  <option value="keep">Keep</option>
                  <option value="move">Move</option>
                  <option value="archive">Delete (archive)</option>
                </select>
              </label>
              <label>
                Pattern
                <input
                  value={rule.pattern}
                  maxLength={256}
                  onChange={(event) =>
                    updateRule(index, { pattern: event.currentTarget.value })
                  }
                />
              </label>
              {rule.action === "move" && (
                <label>
                  Move destination
                  <input
                    value={rule.destination}
                    placeholder="C:\Sorted"
                    onChange={(event) =>
                      updateRule(index, {
                        destination: event.currentTarget.value,
                      })
                    }
                  />
                </label>
              )}
              <button
                className="button button-secondary"
                type="button"
                disabled={rules.length <= 1}
                onClick={() =>
                  setRules((current) =>
                    current.filter((_, item) => item !== index),
                  )
                }
              >
                Remove rule {index + 1}
              </button>
            </div>
            <details>
              <summary>Creation date and size filters</summary>
              <div className="cleaner-rule-filters">
                <label>
                  Created after (UTC)
                  <input
                    type="date"
                    value={rule.createdAfter}
                    onChange={(event) =>
                      updateRule(index, {
                        createdAfter: event.currentTarget.value,
                      })
                    }
                  />
                </label>
                <label>
                  Created before (UTC)
                  <input
                    type="date"
                    value={rule.createdBefore}
                    onChange={(event) =>
                      updateRule(index, {
                        createdBefore: event.currentTarget.value,
                      })
                    }
                  />
                </label>
                <label>
                  Smaller than
                  <input
                    type="number"
                    min="0"
                    step="any"
                    value={rule.smallerThan}
                    onChange={(event) =>
                      updateRule(index, {
                        smallerThan: event.currentTarget.value,
                      })
                    }
                  />
                </label>
                <label>
                  Larger than
                  <input
                    type="number"
                    min="0"
                    step="any"
                    value={rule.largerThan}
                    onChange={(event) =>
                      updateRule(index, {
                        largerThan: event.currentTarget.value,
                      })
                    }
                  />
                </label>
                <label>
                  Size unit
                  <select
                    value={rule.unit}
                    onChange={(event) => {
                      const value = event.currentTarget.value;
                      updateRule(index, {
                        unit:
                          value === "B" || value === "KiB" || value === "GiB"
                            ? value
                            : "MiB",
                      });
                    }}
                  >
                    <option>B</option>
                    <option>KiB</option>
                    <option>MiB</option>
                    <option>GiB</option>
                  </select>
                </label>
              </div>
            </details>
          </fieldset>
        ))}
        <button
          className="button button-secondary"
          type="button"
          disabled={rules.length >= 100}
          onClick={() => setRules((current) => [...current, { ...EMPTY_RULE }])}
        >
          Add rule
        </button>
      </fieldset>
      <div className="hero-actions">
        <button
          className="button button-secondary"
          type="button"
          disabled={!settings.value}
          onClick={() => {
            if (!settings.value) {
              setError(settings.error);
              return;
            }
            try {
              downloadJSON(
                JSON.stringify(settings.value, null, 2),
                "directory-cleaner-settings.json",
              );
              setNotice(
                "Settings downloaded. Use the complete script ZIP and preview locally before applying.",
              );
            } catch (cause) {
              setError(
                cause instanceof Error
                  ? cause.message
                  : "Settings download failed.",
              );
            }
          }}
        >
          Download settings
        </button>
        <a
          className="button button-secondary"
          href="/tool-sources/directory-cleaner.zip"
          download
        >
          Download script (.zip)
        </a>
      </div>
      {!settings.value && <p role="status">{settings.error}</p>}
      {!enabled ? (
        <p className="tool-service-notice" role="status">
          Local service connection is off. The settings editor and script
          download still work; no local requests or polling run. Follow Setup to
          enable the service.
        </p>
      ) : (
        <div className="hero-actions">
          <button
            className="button button-secondary"
            type="button"
            disabled={busy}
            onClick={() =>
              token
                ? (setToken(""),
                  setNotice(
                    "Disconnected. Confirmed cleanup may continue; reconnect or inspect its archive receipt.",
                  ))
                : void connect()
            }
          >
            {token ? "Disconnect local service" : "Connect local service"}
          </button>
          <button
            className="button button-primary"
            type="button"
            disabled={!token || busy || running || !settings.value}
            onClick={() => void preview()}
          >
            Preview files
          </button>
        </div>
      )}
      {busy && <p role="status">Working with the local service...</p>}
      {error && (
        <div className="tool-error" role="alert">
          <p>{error}</p>
          {running && (
            <button
              className="button button-secondary"
              type="button"
              onClick={() => {
                setError("");
                setRetryPoll((value) => value + 1);
              }}
            >
              Retry status
            </button>
          )}
        </div>
      )}
      {notice && <p role="status">{notice}</p>}
      {plan && (
        <section
          className="cleaner-preview"
          aria-labelledby="cleaner-preview-heading"
        >
          <h3 id="cleaner-preview-heading">
            Preview: {plan.entries.length} files, {changes} changes
          </h3>
          <p>
            Archive: <code>{plan.configuration.archiveDir}</code>. No changes
            until confirmed. Preview expires after 10 minutes.
          </p>
          <div
            className="cleaner-table-scroll"
            tabIndex={0}
            aria-label="File preview"
          >
            <table>
              <thead>
                <tr>
                  <th>File</th>
                  <th>Action</th>
                  <th>Destination</th>
                  <th>Bytes</th>
                  <th>Created (UTC)</th>
                </tr>
              </thead>
              <tbody>
                {plan.entries.map((entry) => (
                  <tr key={entry.relativePath}>
                    <td>
                      {entry.relativePath}
                      <small>{entry.reason}</small>
                    </td>
                    <td>
                      {entry.action === "archive"
                        ? "Delete → archive"
                        : entry.action}
                    </td>
                    <td>
                      <code>{cleanerPreviewTarget(plan, entry)}</code>
                    </td>
                    <td>{entry.bytes}</td>
                    <td>
                      {new Date(entry.createdMs).toISOString().slice(0, 10)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {plan.skipped.length > 0 && (
            <details>
              <summary>{plan.skipped.length} excluded entries</summary>
              <ul>
                {plan.skipped.map((entry) => (
                  <li key={entry.relativePath}>
                    {entry.relativePath}: {entry.reason}
                  </li>
                ))}
              </ul>
            </details>
          )}
          <p>
            To apply this exact preview, type <code>APPLY {plan.id}</code>.
            Never apply while other programs are writing these files.
          </p>
          <label>
            Apply confirmation
            <input
              value={confirmation}
              disabled={busy || running || !token}
              onChange={(event) => setConfirmation(event.currentTarget.value)}
              autoComplete="off"
              spellCheck={false}
            />
          </label>
          <button
            className="button button-primary"
            type="button"
            disabled={
              !token ||
              busy ||
              running ||
              changes === 0 ||
              confirmation !== `APPLY ${plan.id}`
            }
            onClick={() => void apply()}
          >
            Archive and move confirmed files
          </button>
        </section>
      )}
      {run && (
        <section aria-labelledby="cleanup-result-heading">
          <h3 id="cleanup-result-heading">Cleanup: {run.status}</h3>
          {run.error && (
            <p className="tool-error" role="alert">
              {run.error}
            </p>
          )}
          {run.result && (
            <>
              <p>
                {run.result.completed.length} files preserved at their
                destinations. {run.result.failed.length} failures.{" "}
                {run.result.pending} files not attempted.
              </p>
              {run.result.receiptPath && (
                <p>
                  Recovery receipt: <code>{run.result.receiptPath}</code>
                </p>
              )}
              {run.result.failed.map((entry) => (
                <p role="alert" key={entry.relativePath}>
                  {entry.relativePath}: {entry.error}
                </p>
              ))}
              <ul>
                {run.result.completed.map((entry) => (
                  <li key={entry.relativePath}>
                    {entry.relativePath} → <code>{entry.target}</code>
                  </li>
                ))}
              </ul>
            </>
          )}
        </section>
      )}
    </div>
  );
}

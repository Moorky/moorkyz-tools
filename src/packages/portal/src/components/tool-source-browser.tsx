"use client";

import { useEffect, useRef, useState } from "react";
import { CopyButton } from "./copy-button";
import { downloadText } from "./download-json";

function FileHierarchy({
  files,
  selected,
  onSelect,
  prefix = "",
}: {
  readonly files: readonly string[];
  readonly selected: string | null;
  readonly onSelect: (file: string) => void;
  readonly prefix?: string;
}) {
  const entries = [
    ...new Set(files.map((file) => file.slice(prefix.length).split("/")[0])),
  ];
  return (
    <ul>
      {entries.map((entry) => {
        const full = `${prefix}${entry}`;
        const folder = files.some((file) => file.startsWith(`${full}/`));
        return (
          <li key={full}>
            {folder ? (
              <details open>
                <summary>{entry}/</summary>
                <FileHierarchy
                  files={files.filter((file) => file.startsWith(`${full}/`))}
                  prefix={`${full}/`}
                  selected={selected}
                  onSelect={onSelect}
                />
              </details>
            ) : (
              <button
                type="button"
                aria-pressed={selected === full}
                onClick={() => onSelect(full)}
              >
                {entry}
              </button>
            )}
          </li>
        );
      })}
    </ul>
  );
}

export function ToolSourceBrowser({
  bundle,
  files,
}: {
  readonly bundle: string;
  readonly files: readonly string[];
}) {
  const [selected, setSelected] = useState<string | null>(null);
  const [sources, setSources] = useState<Record<string, string> | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [retry, setRetry] = useState(0);
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    if (selected === null || sources !== null) return;
    const controller = new AbortController();
    setLoading(true);
    setError("");
    async function load() {
      try {
        const response = await fetch(`/tool-sources/${bundle}.json`, {
          signal: controller.signal,
        });
        if (!response.ok)
          throw new Error(`Source download failed (${response.status}).`);
        const data: unknown = await response.json();
        if (data === null || typeof data !== "object" || Array.isArray(data)) {
          throw new Error("The tool source bundle is incomplete or invalid.");
        }
        const entries: [string, string][] = [];
        for (const file of files) {
          const text: unknown = Reflect.get(data, file);
          if (!Object.hasOwn(data, file) || typeof text !== "string")
            throw new Error(`Missing tool source: ${file}`);
          entries.push([file, text]);
        }
        setSources(Object.fromEntries(entries));
      } catch (cause) {
        if (!controller.signal.aborted)
          setError(
            cause instanceof Error
              ? cause.message
              : "Could not load tool sources.",
          );
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    void load();
    return () => controller.abort();
  }, [bundle, files, selected, sources, retry]);
  useEffect(() => {
    if (selected !== null) heading.current?.focus();
  }, [selected]);

  const text = selected === null ? null : (sources?.[selected] ?? null);
  return (
    <div className="tool-source-browser">
      <div className="source-browser-heading">
        <p>
          Browse the complete local tool. Select any file to inspect its source;
          no script runs in this viewer.
        </p>
        {files.length > 1 && (
          <a
            className="button button-secondary"
            href={`/tool-sources/${bundle}.zip`}
            download={`${bundle}.zip`}
          >
            Download all (.zip)
          </a>
        )}
      </div>
      <div className="source-browser-layout">
        <nav className="source-file-list" aria-label="Tool files">
          <FileHierarchy
            files={files}
            selected={selected}
            onSelect={(file) => {
              setSelected(file);
              setError("");
            }}
          />
        </nav>
        <div className="source-console">
          <div className="source-console-bar">
            <h3 ref={heading} tabIndex={-1}>
              {selected ?? "Select a file"}
            </h3>
            {text !== null && selected !== null && (
              <div className="source-console-actions">
                <CopyButton key={selected} text={text} label="Copy file" />
                <button
                  type="button"
                  onClick={() => {
                    setError("");
                    try {
                      downloadText(
                        text,
                        selected.split("/").at(-1) ?? selected,
                      );
                    } catch (cause) {
                      setError(
                        cause instanceof Error
                          ? cause.message
                          : "Could not download file. Copy or select its source instead.",
                      );
                    }
                  }}
                >
                  Download file
                </button>
              </div>
            )}
          </div>
          {loading && <p role="status">Loading source...</p>}
          {error && (
            <div role="alert">
              <p>{error}</p>
              {sources === null && (
                <button
                  type="button"
                  onClick={() => setRetry((value) => value + 1)}
                >
                  Retry source download
                </button>
              )}
            </div>
          )}
          {text !== null ? (
            <pre tabIndex={0} aria-label={`${selected} source`}>
              <code>{text}</code>
            </pre>
          ) : (
            !loading &&
            !error && (
              <p className="source-console-empty">
                Your tool, down to the source. Pick a file from the hierarchy to
                begin.
              </p>
            )
          )}
        </div>
      </div>
    </div>
  );
}

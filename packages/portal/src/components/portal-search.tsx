"use client";

import { useEffect, useRef, useState } from "react";
import { usePortalSettings } from "./portal-shell";
import { TOOLS } from "../data/tools";

export const PORTAL_REFERENCE = [
  ...TOOLS.map((tool) => ({
    title: tool.title,
    text: tool.summary,
    href: `/tools/${tool.slug}`,
  })),
  {
    title: "Contributing",
    text: "Local development, tool registration and verification.",
    href: "/contributing",
  },
];

export function PortalSearch() {
  const { themeReady } = usePortalSettings();
  const dialog = useRef<HTMLDialogElement>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const input = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState("");
  function open() {
    if (!dialog.current?.open) dialog.current?.showModal();
    input.current?.focus();
  }
  useEffect(() => {
    function key(event: KeyboardEvent) {
      if (
        event.defaultPrevented ||
        event.repeat ||
        event.altKey ||
        event.shiftKey ||
        (event.ctrlKey && event.metaKey) ||
        !(event.ctrlKey || event.metaKey) ||
        event.key.toLowerCase() !== "k"
      )
        return;
      const target = event.target;
      if (
        target instanceof Element &&
        target.closest("input, textarea, select, [contenteditable]")
      )
        return;
      event.preventDefault();
      dialog.current?.showModal();
      input.current?.focus();
    }
    document.addEventListener("keydown", key);
    return () => document.removeEventListener("keydown", key);
  }, []);
  const terms = query.toLowerCase().trim().split(/\s+/).filter(Boolean);
  const results = PORTAL_REFERENCE.filter((entry) =>
    terms.every((term) =>
      `${entry.title} ${entry.text} ${entry.href}`.toLowerCase().includes(term),
    ),
  );
  return (
    <>
      <button
        ref={trigger}
        type="button"
        className="search-trigger"
        aria-label="Search documentation and navigation"
        aria-keyshortcuts="Control+K Meta+K"
        disabled={!themeReady}
        onClick={open}
      >
        <svg width="16" height="16" viewBox="0 0 24 24" aria-hidden="true">
          <circle
            cx="10"
            cy="10"
            r="6"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          />
          <path
            d="m15 15 6 6"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          />
        </svg>
        <span className="search-trigger-copy">Search</span>
      </button>
      <dialog
        ref={dialog}
        className="portal-search"
        aria-labelledby="portal-search-heading"
        onClose={() => trigger.current?.focus()}
        onKeyDown={(event) => {
          if (event.key !== "Escape") return;
          event.preventDefault();
          event.stopPropagation();
          dialog.current?.close();
        }}
      >
        <div className="search-heading">
          <h2 id="portal-search-heading">Find your next step.</h2>
          <button type="button" onClick={() => dialog.current?.close()}>
            Close search
          </button>
        </div>
        <label htmlFor="portal-query">
          Search documentation and navigation
        </label>
        <input
          ref={input}
          id="portal-query"
          type="search"
          value={query}
          onChange={(event) => setQuery(event.currentTarget.value)}
        />
        <p>
          Local reference search. Tab through results; Escape closes. Ctrl/Cmd K
          never overrides editors or application inputs.
        </p>
        <ul>
          {results.map((entry) => (
            <li key={entry.title}>
              <a href={entry.href} onClick={() => dialog.current?.close()}>
                <strong>{entry.title}</strong>
                <span>{entry.text}</span>
              </a>
            </li>
          ))}
        </ul>
        {!results.length && (
          <p role="status">
            No matching tools. Try “video”, “directory” or “toolbar”.
          </p>
        )}
      </dialog>
    </>
  );
}

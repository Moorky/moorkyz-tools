"use client";

import { useMemo, useState } from "react";
import { buildToolbar } from "../../../../../tools/toolbar-builder/builder.mjs";
import { CopyButton } from "./copy-button";
import { downloadText } from "./download-json";
import { usePortalSettings } from "./portal-shell";

export function CompactToolbarBuilder({ nonce }: { readonly nonce: string }) {
  const { themeReady } = usePortalSettings();
  const [items, setItems] = useState([
    { label: "Home", href: "#home" },
    { label: "Contact", href: "#contact" },
  ]);
  const [theme, setTheme] = useState<"light" | "dark">("light");
  const [position, setPosition] = useState<"top" | "bottom">("bottom");
  const [accent, setAccent] = useState("#6c35ab");
  const [message, setMessage] = useState("");
  const result = useMemo(() => {
    try {
      return {
        value: buildToolbar({ items, theme, position, accent }),
        error: "",
      };
    } catch (error) {
      return {
        value: null,
        error:
          error instanceof Error
            ? error.message
            : "Could not generate the toolbar.",
      };
    }
  }, [items, theme, position, accent]);
  function update(index: number, field: "label" | "href", value: string) {
    setItems((current) =>
      current.map((item, itemIndex) =>
        itemIndex === index ? { ...item, [field]: value } : item,
      ),
    );
    setMessage("");
  }
  return (
    <div className="compact-builder tool-form">
      <p>Up to eight links. One small HTML file. No SDK or setup.</p>
      <fieldset className="builder-controls" disabled={!themeReady}>
        <legend className="sr-only">Toolbar settings</legend>
        <div className="builder-links">
          {items.map((item, index) => (
            <div className="builder-link-row" key={index}>
              <label>
                Label {index + 1}
                <input
                  value={item.label}
                  maxLength={40}
                  onChange={(event) =>
                    update(index, "label", event.currentTarget.value)
                  }
                />
              </label>
              <label>
                Link {index + 1}
                <input
                  value={item.href}
                  maxLength={512}
                  onChange={(event) =>
                    update(index, "href", event.currentTarget.value)
                  }
                />
              </label>
              <button
                className="button button-secondary"
                type="button"
                disabled={items.length === 1}
                aria-label={`Remove link ${index + 1}`}
                onClick={() =>
                  setItems((current) =>
                    current.filter((_, itemIndex) => itemIndex !== index),
                  )
                }
              >
                Remove
              </button>
            </div>
          ))}
        </div>
        <div className="builder-options">
          <button
            className="button button-secondary"
            type="button"
            disabled={items.length >= 8}
            onClick={() =>
              setItems((current) => [
                ...current,
                { label: "New link", href: "#new" },
              ])
            }
          >
            Add link
          </button>
          <label>
            Appearance
            <select
              value={theme}
              onChange={(event) =>
                setTheme(
                  event.currentTarget.value === "dark" ? "dark" : "light",
                )
              }
            >
              <option value="light">Light</option>
              <option value="dark">Dark</option>
            </select>
          </label>
          <label>
            Position
            <select
              value={position}
              onChange={(event) =>
                setPosition(
                  event.currentTarget.value === "top" ? "top" : "bottom",
                )
              }
            >
              <option value="bottom">Bottom</option>
              <option value="top">Top</option>
            </select>
          </label>
          <label>
            Accent border
            <input
              type="color"
              value={accent}
              onChange={(event) => setAccent(event.currentTarget.value)}
            />
          </label>
        </div>
      </fieldset>
      {result.error && (
        <p className="tool-error" role="alert">
          {result.error}
        </p>
      )}
      <div className="builder-preview" aria-label="Toolbar preview">
        {themeReady && result.value && (
          <>
            <style nonce={nonce}>
              {result.value.css.replaceAll(
                ".tiny-toolbar",
                "#builder-preview-nav",
              ) + "#builder-preview-nav{position:absolute;}"}
            </style>
            <nav id="builder-preview-nav" aria-label="Custom toolbar preview">
              {result.value.config.items.map((item, index) => (
                <a
                  key={index}
                  href={item.href}
                  onClick={(event) => {
                    event.preventDefault();
                    setMessage(
                      `Preview selected: ${item.label}. Exported links navigate normally.`,
                    );
                  }}
                >
                  {item.label}
                </a>
              ))}
            </nav>
          </>
        )}
      </div>
      <div className="hero-actions">
        <CopyButton
          text={result.value?.html ?? ""}
          label="Copy HTML"
          disabled={!result.value || !themeReady}
        />
        <button
          className="button button-primary"
          type="button"
          disabled={!result.value || !themeReady}
          onClick={() => {
            if (!result.value) return;
            try {
              downloadText(
                result.value.html,
                "toolbar.html",
                "text/html;charset=utf-8",
              );
              setMessage("Toolbar HTML downloaded.");
            } catch (error) {
              setMessage(
                error instanceof Error
                  ? error.message
                  : "Download failed. Copy the HTML instead.",
              );
            }
          }}
        >
          Download HTML
        </button>
        <span className="builder-size">
          {result.value
            ? `${new TextEncoder().encode(result.value.html).length} bytes`
            : "Invalid settings"}
        </span>
      </div>
      <p role="status">{message}</p>
    </div>
  );
}

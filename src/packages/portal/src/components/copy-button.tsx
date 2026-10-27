"use client";

import { useState } from "react";

export function CopyButton({
  text,
  label = "Copy code",
  disabled = false,
}: {
  readonly text: string;
  readonly label?: string;
  readonly disabled?: boolean;
}) {
  const [message, setMessage] = useState("");

  async function copyText() {
    if (navigator.clipboard === undefined) {
      setMessage("Copy is unavailable in this browser context.");
      return;
    }
    try {
      await navigator.clipboard.writeText(text);
      setMessage("Copied to clipboard.");
    } catch {
      setMessage("Could not copy. Select the text and copy it manually.");
    }
  }

  return (
    <span className="copy-control">
      <button
        className="copy-button"
        type="button"
        onClick={copyText}
        disabled={disabled}
      >
        {label}
      </button>
      <span className="copy-feedback" role="status" aria-live="polite">
        {message}
      </span>
    </span>
  );
}

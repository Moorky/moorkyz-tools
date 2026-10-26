"use client";

import { useEffect, useState } from "react";

export function LocalToolWorkspace({ enabled }: { readonly enabled: boolean }) {
  const [opened, setOpened] = useState(false);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    if (!enabled || !opened) return;
    const controller = new AbortController();
    setReady(false);
    setError("");
    async function connect() {
      try {
        const response = await fetch(
          "/api/local-tools/videocrunch/api/config",
          { signal: controller.signal, cache: "no-store" },
        );
        const data: unknown = await response.json();
        if (!response.ok) {
          const message =
            data !== null &&
            typeof data === "object" &&
            "error" in data &&
            typeof data.error === "string"
              ? data.error
              : `Moorky Video Crunch connection failed (${response.status}).`;
          throw new Error(message);
        }
        if (
          data === null ||
          typeof data !== "object" ||
          !("token" in data) ||
          typeof data.token !== "string"
        )
          throw new Error(
            "Moorky Video Crunch returned an invalid configuration. Check the local service terminal.",
          );
        if (!controller.signal.aborted) setReady(true);
      } catch (cause) {
        if (!controller.signal.aborted)
          setError(
            cause instanceof Error
              ? cause.message
              : "Could not connect to Moorky Video Crunch.",
          );
      }
    }
    void connect();
    return () => controller.abort();
  }, [enabled, opened, retry]);
  if (!enabled) {
    return (
      <p className="tool-service-notice" role="status">
        Moorky Video Crunch connection is off. Follow Setup to start the Windows
        service and explicitly enable the portal connection. No service requests
        or background polling run while it is off.
      </p>
    );
  }
  return (
    <div className="local-tool-workspace">
      <p>
        The service runs on your Windows PC. Opening the workspace connects to
        it and reads your local export history. Closing this view stops its
        polling; any running export continues in the service terminal.
      </p>
      <button
        className="button button-primary"
        type="button"
        onClick={() => {
          setReady(false);
          setError("");
          setOpened(!opened);
        }}
      >
        {opened
          ? "Close Moorky Video Crunch workspace"
          : "Open Moorky Video Crunch workspace"}
      </button>
      {opened && !ready && !error && (
        <p role="status">
          Connecting to your local Moorky Video Crunch service...
        </p>
      )}
      {opened && error && (
        <div role="alert">
          <p>{error}</p>
          <button type="button" onClick={() => setRetry((value) => value + 1)}>
            Retry connection
          </button>
        </div>
      )}
      {opened && ready && (
        <iframe
          title="Moorky Video Crunch local workspace"
          src="/api/local-tools/videocrunch/index.html"
          className="local-tool-frame"
        />
      )}
    </div>
  );
}

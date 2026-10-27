"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";

interface PortalSettings {
  readonly theme: "light" | "dark";
  readonly toggleTheme: () => void;
  readonly themeReady: boolean;
  readonly themeStorageWarning: boolean;
}

const PortalSettingsContext = createContext<PortalSettings | null>(null);

export function usePortalSettings(): PortalSettings {
  const settings = useContext(PortalSettingsContext);
  if (settings === null)
    throw new Error("Portal settings must be used inside PortalShell.");
  return settings;
}

export function PortalShell({ children }: Readonly<{ children: ReactNode }>) {
  const [theme, setTheme] = useState<"light" | "dark">("light");
  const [themeReady, setThemeReady] = useState(false);
  const [themeStorageWarning, setThemeStorageWarning] = useState(false);
  useEffect(() => {
    document.body.dataset.theme = "light";
    try {
      const stored = window.localStorage.getItem("tools.moorkyz.com-theme");
      if (stored === "dark" || stored === "light") {
        setTheme(stored);
        document.documentElement.dataset.theme = stored;
        document.body.dataset.theme = stored;
      }
    } catch {
      setThemeStorageWarning(true);
    }
    setThemeReady(true);
  }, []);
  const toggleTheme = useCallback(() => {
    const next = theme === "light" ? "dark" : "light";
    setTheme(next);
    document.documentElement.dataset.theme = next;
    document.body.dataset.theme = next;
    try {
      window.localStorage.setItem("tools.moorkyz.com-theme", next);
      setThemeStorageWarning(false);
    } catch {
      setThemeStorageWarning(true);
    }
  }, [theme]);
  return (
    <PortalSettingsContext.Provider
      value={{ theme, toggleTheme, themeReady, themeStorageWarning }}
    >
      {children}
    </PortalSettingsContext.Provider>
  );
}

export function ThemeToggle() {
  const { theme, toggleTheme, themeReady, themeStorageWarning } =
    usePortalSettings();
  return (
    <span className="theme-control">
      <button
        className="theme-toggle"
        type="button"
        aria-label={`${theme}. Switch to ${theme === "light" ? "dark" : "light"} theme`}
        aria-pressed={theme === "dark"}
        disabled={!themeReady}
        onClick={toggleTheme}
      >
        <span aria-hidden="true">{theme === "light" ? "◐" : "☼"}</span>
        <span className="theme-toggle-label">{theme}</span>
      </button>
      {themeStorageWarning && (
        <span className="sr-only" role="status">
          Theme changed for this visit but could not be saved on this device.
        </span>
      )}
    </span>
  );
}

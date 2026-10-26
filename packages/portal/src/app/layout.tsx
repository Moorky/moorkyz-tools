import type { Metadata, Viewport } from "next";
import type { ReactNode } from "react";
import { PortalShell } from "../components/portal-shell";
import "./globals.css";
import "./tools.css";

export const metadata: Metadata = {
  metadataBase: new URL("https://tools.moorkyz.com"),
  title: {
    default: "Moorkyz Tools",
    template: "%s — Moorkyz Tools",
  },
  description:
    "Independent tools for building interfaces and working with video. Local-first workspaces, clear guides and useful source files.",
  icons: {
    icon: [
      { url: "/favicon.ico", type: "image/x-icon" },
      { url: "/favicon-16x16.png", type: "image/png", sizes: "16x16" },
      { url: "/favicon-32x32.png", type: "image/png", sizes: "32x32" },
      { url: "/moorkyz-tools.svg", type: "image/svg+xml", sizes: "any" },
    ],
    apple: [
      {
        url: "/apple-touch-icon.png",
        type: "image/png",
        sizes: "180x180",
      },
    ],
  },
  manifest: "/site.webmanifest",
  alternates: {
    canonical: "/",
  },
  openGraph: {
    type: "website",
    url: "https://tools.moorkyz.com/",
    siteName: "Moorkyz Tools",
    title: "Moorkyz Tools — A little more useful.",
    description:
      "Explore independent tools for interfaces and video, with local workspaces and clear setup guides.",
    images: [
      {
        url: "/tools.moorkyz.com-mark.png",
        width: 512,
        height: 512,
        alt: "Moorkyz Tools",
      },
    ],
  },
  twitter: {
    card: "summary",
    title: "Moorkyz Tools — A little more useful.",
    description: "Local-first tools for interfaces and video.",
    images: ["/tools.moorkyz.com-mark.png"],
  },
  robots: {
    index: true,
    follow: true,
  },
};

export const viewport: Viewport = {
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#fafafa" },
    { media: "(prefers-color-scheme: dark)", color: "#08060d" },
  ],
};

export const dynamic = "force-dynamic";

export default function RootLayout({
  children,
}: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="en" data-theme="light">
      <head>
        <link
          rel="preload"
          href="/fonts/DMSans.ttf"
          as="font"
          type="font/ttf"
          crossOrigin="anonymous"
        />
        <link
          rel="preload"
          href="/fonts/DMMono-Regular.ttf"
          as="font"
          type="font/ttf"
          crossOrigin="anonymous"
        />
      </head>
      <body>
        <PortalShell>{children}</PortalShell>
      </body>
    </html>
  );
}

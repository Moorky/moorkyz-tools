import { TOOL_INDEX } from "./tool-index.ts";

export interface ToolDefinition {
  readonly slug: string;
  readonly id: string;
  readonly title: string;
  readonly category: string;
  readonly version: string;
  readonly releaseDate: string | null;
  readonly updatedOn: string;
  readonly summary: string;
  readonly catalogDescription: string;
  readonly usefulness: number;
  readonly description: string;
  readonly howItWorks?: readonly {
    readonly title: string;
    readonly text: string;
  }[];
  readonly features?: readonly {
    readonly title: string;
    readonly text: string;
  }[];
  readonly limitations?: readonly string[];
  readonly downloadNotice?: string;
  readonly useCases: readonly string[];
  readonly setup: readonly {
    readonly title: string;
    readonly text: string;
    readonly command?: string;
  }[];
  readonly workspace?: "toolbar" | "videocrunch" | "directory-cleaner";
  readonly source?: {
    readonly bundle: string;
    readonly files: readonly string[];
    readonly notice?: string;
  };
}

export const TOOLS: readonly ToolDefinition[] = TOOL_INDEX;

export function findTool(slug: string): ToolDefinition | undefined {
  return TOOLS.find((tool) => tool.slug === slug);
}

export function formatToolDate(value: string): string {
  return new Intl.DateTimeFormat("en", {
    dateStyle: "long",
    timeZone: "UTC",
  }).format(new Date(`${value}T00:00:00Z`));
}

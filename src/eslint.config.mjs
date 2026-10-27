import tseslint from "typescript-eslint";

export default tseslint.config(
  ...tseslint.configs.recommended,
  {
    files: ["**/*.cjs"],
    rules: { "@typescript-eslint/no-require-imports": "off" },
  },
  {
    ignores: [
      "**/dist/**",
      "**/node_modules/**",
      "**/test-results/**",
      "**/playwright-report/**",
      "trash/**",
      "logs/**",
      "archive/**",
      ".moorkyz/**",
    ],
  },
);

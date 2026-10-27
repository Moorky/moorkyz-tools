export function downloadText(
  text: string,
  filename: string,
  type = "text/plain;charset=utf-8",
): void {
  const url = URL.createObjectURL(new Blob([text], { type }));
  try {
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    link.click();
  } finally {
    URL.revokeObjectURL(url);
  }
}

export function downloadJSON(text: string, filename: string): void {
  downloadText(text, filename, "application/json");
}

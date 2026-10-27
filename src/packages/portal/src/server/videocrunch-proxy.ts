import { proxyLocalTool } from "./local-tool-proxy.ts";

export function proxyVideoCrunch(
  request: Request,
  segments: readonly string[],
): Promise<Response> {
  return proxyLocalTool(request, segments, "videocrunch");
}

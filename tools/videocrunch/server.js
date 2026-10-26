'use strict';

const http = require('node:http');
const fs = require('node:fs');
const fsp = require('node:fs/promises');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const { spawn, execFile } = require('node:child_process');
const { promisify } = require('node:util');
const { pipeline } = require('node:stream/promises');

const execFileAsync = promisify(execFile);
const publicDir = path.join(__dirname, 'public');
const scriptPath = path.join(__dirname, 'Get-WebVideoCrunch.ps1');
const mimeTypes = {
  '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8', '.json': 'application/json; charset=utf-8',
  '.mp4': 'video/mp4', '.webm': 'video/webm', '.mkv': 'video/x-matroska',
  '.mov': 'video/quicktime', '.m4v': 'video/mp4', '.txt': 'text/plain; charset=utf-8',
};

class HttpError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

function validateExport(body) {
  if (!body || typeof body !== 'object' || Array.isArray(body)) {
    throw new HttpError(400, 'Expected an export configuration.');
  }
  if (typeof body.url !== 'string' || body.url.length > 8192) {
    throw new HttpError(400, 'Enter a valid HTTP or HTTPS video URL.');
  }
  let url;
  try { url = new URL(body.url); } catch {
    throw new HttpError(400, 'Enter a valid HTTP or HTTPS video URL.');
  }
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password) {
    throw new HttpError(400, 'Use a public HTTP or HTTPS URL without embedded credentials.');
  }
  if (typeof body.outputDir !== 'string' || body.outputDir.length > 1024 ||
      !/^[a-z]:\\/i.test(body.outputDir) || /[<>"|?*\x00-\x1f]/.test(body.outputDir) ||
      body.outputDir.slice(2).includes(':')) {
    throw new HttpError(400, 'Choose an absolute local Windows folder, such as C:\\Users\\marvi\\Desktop\\VideoExports.');
  }
  if (!['Balanced', 'Crunch', 'Extreme'].includes(body.compression)) {
    throw new HttpError(400, 'Choose Balanced, Crunch, or Extreme compression.');
  }
  if (typeof body.skipCompression !== 'boolean') {
    throw new HttpError(400, 'The original-only option must be true or false.');
  }
  return { url: url.href, outputDir: path.win32.normalize(body.outputDir),
    compression: body.compression, skipCompression: body.skipCompression };
}

function powerShellEnvironment(extra = {}) {
  const environment = { ...process.env, ...extra };
  // A PowerShell 7 parent can otherwise make Windows PowerShell load incompatible modules.
  for (const key of Object.keys(environment)) {
    if (key.toLowerCase() === 'psmodulepath') delete environment[key];
  }
  return environment;
}

async function checkTools() {
  return Promise.all([
    { name: 'PowerShell', command: 'powershell.exe', args: ['-NoProfile', '-NonInteractive', '-Command', '$PSVersionTable.PSVersion.ToString()'] },
    { name: 'yt-dlp', command: 'yt-dlp', args: ['--version'] },
    { name: 'FFmpeg', command: 'ffmpeg', args: ['-version'] },
  ].map(async (tool) => {
    try {
      const { stdout } = await execFileAsync(tool.command, tool.args, {
        windowsHide: true, timeout: 15000,
        env: tool.name === 'PowerShell' ? powerShellEnvironment() : process.env,
      });
      return { name: tool.name, available: true, version: stdout.trim().split(/\r?\n/)[0] };
    } catch (error) {
      return { name: tool.name, available: false, error: error.message };
    }
  }));
}

function runExport(job, resultPath, onLog) {
  // User input is passed as JSON in the environment, never interpolated into PowerShell code.
  const command = [
    "$ErrorActionPreference = 'Stop'",
    '[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)',
    '$r = $env:WEB_VIDEO_CRUNCH_REQUEST | ConvertFrom-Json',
    '$p = @{ Url = $r.url; OutputDir = $r.outputDir; Compression = $r.compression; SkipCompression = [bool]$r.skipCompression; ResultPath = $r.resultPath }',
    '& $env:WEB_VIDEO_CRUNCH_SCRIPT @p',
    'if (-not $?) { exit 1 }',
  ].join('; ');
  const child = spawn('powershell.exe',
    ['-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-Command', command], {
      windowsHide: true,
      env: powerShellEnvironment({ WEB_VIDEO_CRUNCH_SCRIPT: scriptPath,
        WEB_VIDEO_CRUNCH_REQUEST: JSON.stringify({ ...job, resultPath }) }),
    });
  child.stdout.setEncoding('utf8');
  child.stderr.setEncoding('utf8');
  child.stdout.on('data', onLog);
  child.stderr.on('data', onLog);
  return new Promise((resolve, reject) => {
    child.once('error', reject);
    child.once('close', (code, signal) => {
      if (code === 0) resolve();
      else reject(new Error(`The video tool stopped (${signal || `exit code ${code}`}). Check the export log.`));
    });
  });
}

async function readJson(file) {
  return JSON.parse((await fsp.readFile(file, 'utf8')).replace(/^\uFEFF/, ''));
}

async function readBody(req) {
  if (!/^application\/json(?:;|$)/i.test(req.headers['content-type'] || '')) {
    throw new HttpError(415, 'Send the export configuration as JSON.');
  }
  const chunks = [];
  let size = 0;
  for await (const chunk of req.iterator({ destroyOnReturn: false })) {
    size += chunk.length;
    if (size > 16384) {
      req.resume();
      throw new HttpError(413, 'The request is too large.');
    }
    chunks.push(chunk);
  }
  try { return JSON.parse(Buffer.concat(chunks).toString('utf8')); } catch {
    throw new HttpError(400, 'The export configuration is not valid JSON.');
  }
}

function sendJson(res, status, data) {
  res.writeHead(status, { 'Content-Type': mimeTypes['.json'], 'Cache-Control': 'no-store' });
  res.end(JSON.stringify(data));
}

async function sendFile(req, res, file, download = false) {
  const stat = await fsp.stat(file);
  if (!stat.isFile()) throw new HttpError(404, 'File not found.');
  let start = 0;
  let end = stat.size - 1;
  let status = 200;
  const headers = {
    'Content-Type': mimeTypes[path.extname(file).toLowerCase()] || 'application/octet-stream',
    'Accept-Ranges': 'bytes', 'Cache-Control': 'no-store',
  };
  if (download) headers['Content-Disposition'] = `attachment; filename="${path.basename(file).replace(/["\r\n]/g, '_')}"`;
  if (req.headers.range) {
    const match = /^bytes=(\d*)-(\d*)$/.exec(req.headers.range);
    if (!match || (!match[1] && !match[2]) || stat.size === 0) {
      res.writeHead(416, { 'Content-Range': `bytes */${stat.size}` }); res.end(); return;
    }
    start = match[1] ? Number(match[1]) : Math.max(0, stat.size - Number(match[2]));
    end = match[1] && match[2] ? Math.min(Number(match[2]), end) : end;
    if (!Number.isSafeInteger(start) || !Number.isSafeInteger(end) || start > end || start >= stat.size) {
      res.writeHead(416, { 'Content-Range': `bytes */${stat.size}` }); res.end(); return;
    }
    status = 206;
    headers['Content-Range'] = `bytes ${start}-${end}/${stat.size}`;
  }
  headers['Content-Length'] = stat.size === 0 ? 0 : end - start + 1;
  res.writeHead(status, headers);
  if (req.method === 'HEAD' || stat.size === 0) { res.end(); return; }
  try { await pipeline(fs.createReadStream(file, { start, end }), res); } catch (error) {
    if (error.code !== 'ERR_STREAM_PREMATURE_CLOSE' && error.code !== 'ECONNRESET') throw error;
  }
}

async function createApplication(options = {}) {
  const dataDir = options.dataDir || path.join(os.homedir(), '.web-video-crunch');
  const defaultOutputDir = options.defaultOutputDir || path.join(os.homedir(), 'Desktop', 'VideoExports');
  const runner = options.runExport || runExport;
  const inspectTools = options.checkTools || checkTools;
  const historyFile = path.join(dataDir, 'history.json');
  const token = crypto.randomBytes(32).toString('hex');
  let jobs = [];
  let activePromise = null;
  let closing = false;
  let closePromise = null;
  let saveChain = Promise.resolve();
  let storageError = null;
  await fsp.mkdir(dataDir, { recursive: true });
  try {
    jobs = await readJson(historyFile);
    if (!Array.isArray(jobs)) throw new Error('Expected a job history array.');
  } catch (error) {
    if (error.code !== 'ENOENT') throw new Error(`Cannot read export history: ${error.message}`);
  }
  for (const job of jobs) {
    if (job.status === 'running') {
      job.status = 'failed';
      job.stage = 'Export interrupted';
      job.error = 'The app stopped before this export finished. Files already downloaded were retained.';
      job.completedAt = new Date().toISOString();
    }
  }
  function persist() {
    const snapshot = JSON.stringify(jobs, null, 2);
    const operation = saveChain.then(async () => {
      await fsp.writeFile(`${historyFile}.tmp`, snapshot, 'utf8');
      await fsp.rename(`${historyFile}.tmp`, historyFile);
    });
    saveChain = operation.catch((error) => {
      storageError = `Could not save export history: ${error.message}`;
      console.error(storageError);
    });
    return operation;
  }
  await persist();
  const tools = await inspectTools();

  function publicJob(job, includeLogs = false) {
    const { logs, ...visible } = job;
    return includeLogs ? { ...visible, logs } : visible;
  }

  async function execute(job) {
    const resultPath = path.join(dataDir, `${job.id}.json`);
    let runError = null;
    try {
      await runner(job, resultPath, (text) => {
        job.logs = (job.logs + text).slice(-100000);
        if (text.includes('Downloading candidate')) job.stage = 'Downloading source';
        if (text.includes('Compressing video')) job.stage = 'Compressing video';
      });
    } catch (error) { runError = error; }
    try {
      const result = await readJson(resultPath);
      if (typeof result.exportDir !== 'string' || !path.isAbsolute(result.exportDir)) {
        throw new Error('The video tool reported an invalid export directory.');
      }
      const root = path.resolve(job.outputDir);
      const relative = path.relative(root, result.exportDir);
      if (!relative || relative.startsWith('..') || path.isAbsolute(relative)) {
        throw new Error('The video tool reported an export outside the selected output folder.');
      }
      job.exportDir = result.exportDir;
      const manifest = await readJson(path.join(job.exportDir, 'manifest.json'));
      job.manifest = manifest;
      if (manifest.status !== 'completed' && !runError) {
        runError = new Error(manifest.error || 'The video export did not complete.');
      }
    } catch (error) {
      if (runError) job.logs += `\nCould not load export details: ${error.message}\n`;
      else runError = new Error(`Could not load export details: ${error.message}`);
    }
    job.status = runError ? 'failed' : 'completed';
    job.stage = runError ? 'Export failed' : 'Ready to watch';
    job.error = runError ? (job.manifest?.error || runError.message) : null;
    job.completedAt = new Date().toISOString();
    if (runError) job.logs += `\n${job.error}\n`;
    await persist();
  }

  async function exportedFile(job, requested) {
    if (!job.exportDir || !job.manifest) throw new HttpError(404, 'No exported files are available.');
    const allowed = ['manifest.json', job.manifest.original?.path, job.manifest.compressed?.path];
    if (job.status === 'completed') allowed.push('index.html');
    if (!allowed.includes(requested)) throw new HttpError(404, 'Exported file not found.');
    const root = await fsp.realpath(job.exportDir);
    const file = await fsp.realpath(path.join(root, requested));
    const relative = path.relative(root, file);
    if (relative.startsWith('..') || path.isAbsolute(relative)) throw new HttpError(403, 'File access denied.');
    return file;
  }

  const server = http.createServer(async (req, res) => {
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('Referrer-Policy', 'no-referrer');
    res.setHeader('Cross-Origin-Resource-Policy', 'same-origin');
    res.setHeader('Content-Security-Policy',
      "default-src 'self'; style-src 'self' 'unsafe-inline'; media-src 'self'; img-src 'self' data:; script-src 'self'; connect-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'");
    try {
      const address = server.address();
      const host = `127.0.0.1:${address.port}`;
      if (req.headers.host !== host) throw new HttpError(403, 'Use the local app address printed in the terminal.');
      if (req.headers.origin && req.headers.origin !== `http://${host}`) {
        throw new HttpError(403, 'Requests from other websites are not allowed.');
      }
      if (req.headers['sec-fetch-site'] === 'cross-site') throw new HttpError(403, 'Cross-site requests are not allowed.');
      const url = new URL(req.url, `http://${host}`);
      if (req.method === 'GET' && url.pathname === '/api/config') {
        sendJson(res, 200, { token, defaultOutputDir, tools, storageError }); return;
      }
      if (req.method === 'GET' && url.pathname === '/api/jobs') {
        sendJson(res, 200, { jobs: jobs.map((job) => publicJob(job)), storageError }); return;
      }
      if (req.method === 'POST' && url.pathname === '/api/jobs') {
        if (req.headers['x-webvideocrunch-token'] !== token) throw new HttpError(403, 'Refresh the app before starting an export.');
        if (closing) throw new HttpError(503, 'The app is shutting down.');
        if (storageError) throw new HttpError(503, storageError);
        if (activePromise) throw new HttpError(409, 'An export is already running. Let it finish before starting another.');
        const missing = tools.filter((tool) => !tool.available);
        if (missing.length) throw new HttpError(503, `Install ${missing.map((tool) => tool.name).join(', ')} and restart the app.`);
        const input = validateExport(await readBody(req));
        // Recheck after reading the request: another tab may have started a job.
        if (activePromise) throw new HttpError(409, 'An export is already running.');
        const job = { id: crypto.randomUUID(), ...input, status: 'running', stage: 'Inspecting source',
          startedAt: new Date().toISOString(), completedAt: null, exportDir: null,
          manifest: null, error: null, logs: '' };
        jobs.unshift(job);
        jobs = jobs.slice(0, 50);
        activePromise = (async () => {
          try { await persist(); await execute(job); } catch (error) {
            job.status = 'failed'; job.stage = 'Export failed'; job.error = error.message;
            job.completedAt = new Date().toISOString();
            console.error(`Export ${job.id}: ${error.message}`);
          } finally { activePromise = null; }
        })();
        sendJson(res, 202, publicJob(job, true)); return;
      }
      const jobRoute = /^\/api\/jobs\/([0-9a-f-]+)(?:\/files\/(.+))?$/.exec(url.pathname);
      if (jobRoute && ['GET', 'HEAD'].includes(req.method)) {
        const job = jobs.find((entry) => entry.id === jobRoute[1]);
        if (!job) throw new HttpError(404, 'Export not found.');
        if (!jobRoute[2]) { sendJson(res, 200, publicJob(job, true)); return; }
        let requested;
        try { requested = decodeURIComponent(jobRoute[2]); } catch { throw new HttpError(400, 'Invalid file URL.'); }
        await sendFile(req, res, await exportedFile(job, requested), url.searchParams.has('download')); return;
      }
      const staticFiles = { '/': 'index.html', '/app.js': 'app.js', '/styles.css': 'styles.css', '/favicon.svg': 'favicon.svg' };
      if (['GET', 'HEAD'].includes(req.method) && staticFiles[url.pathname]) {
        await sendFile(req, res, path.join(publicDir, staticFiles[url.pathname])); return;
      }
      throw new HttpError(404, 'Not found.');
    } catch (error) {
      if (res.headersSent || res.destroyed) {
        if (!res.destroyed) res.destroy(error);
        console.error(`Response failed: ${error.message}`);
        return;
      }
      const status = error.status || (error.code === 'ENOENT' ? 404 : 500);
      if (status === 500) console.error(error);
      sendJson(res, status, { error: status === 500 ? 'The local app encountered an error. See the terminal for details.' : error.message });
    }
  });
  server.requestTimeout = 30000;

  return {
    server,
    close() {
      if (!closePromise) {
        closing = true;
        closePromise = (async () => {
          const stopped = server.listening
            ? new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()))
            : Promise.resolve();
          await activePromise;
          await saveChain;
          await stopped;
        })();
      }
      return closePromise;
    },
  };
}

async function main() {
  if (Number(process.versions.node.split('.')[0]) < 22) throw new Error('Node.js 22 or newer is required.');
  const portIndex = process.argv.indexOf('--port');
  const port = portIndex === -1 ? 8765 : Number(process.argv[portIndex + 1]);
  if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Port must be between 1 and 65535.');
  const app = await createApplication();
  await new Promise((resolve, reject) => {
    app.server.once('error', reject);
    app.server.listen(port, '127.0.0.1', resolve);
  });
  const url = `http://127.0.0.1:${port}`;
  console.log(`\n  Moorky Video Crunch is ready at ${url}\n  Keep this terminal open. Press Ctrl+C to stop.\n`);
  if (process.argv.includes('--open')) {
    execFile('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', `Start-Process '${url}'`],
      { windowsHide: true, env: powerShellEnvironment() },
      (error) => { if (error) console.error(`Could not open the browser. Open ${url} manually. ${error.message}`); });
  }
  let stopping = false;
  const stop = () => {
    if (stopping) return;
    stopping = true;
    console.log('Stopping the web app. If an export is running, it will finish before shutdown.');
    app.close().catch((error) => { console.error(error); process.exitCode = 1; });
  };
  process.on('SIGINT', stop);
  process.on('SIGTERM', stop);
}

if (require.main === module) {
  main().catch((error) => {
    console.error(error.code === 'EADDRINUSE'
      ? 'This port is already in use. Open the running app or start with --port followed by another port.'
      : error.message);
    process.exitCode = 1;
  });
}

module.exports = { createApplication, validateExport };

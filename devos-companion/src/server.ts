import * as http from 'http';
import * as vscode from 'vscode';
import { contextRoute } from './routes/context';
import { diffRoute, registerDiffProvider, attachAcceptCommand } from './routes/diff';
import { applyRoute } from './routes/apply';
import { reviewRoute } from './routes/review';
import { notifyRoute } from './routes/notify';

export interface ServerHandle {
  server: http.Server;
  port: number;
  token: string;
}

/**
 * Tiny request router. We intentionally avoid express/koa to keep the VSIX small
 * and dependency-free (other than `marked` for review rendering).
 */
export type Handler = (req: ParsedRequest, res: http.ServerResponse, ctx: vscode.ExtensionContext) => Promise<void> | void;

export interface ParsedRequest {
  method: string;
  path: string;
  headers: http.IncomingHttpHeaders;
  body: unknown;
  raw: http.IncomingMessage;
}

const ROUTES: Record<string, Handler> = {
  'GET /v1/context':  contextRoute,
  'POST /v1/diff':    diffRoute,
  'POST /v1/apply':   applyRoute,
  'POST /v1/review':  reviewRoute,
  'POST /v1/notify':  notifyRoute,
  'GET /v1/ping':     async (_req, res) => sendJson(res, 200, { ok: true }),
};

export async function startServer(
  context: vscode.ExtensionContext,
  token: string,
  desiredPort: number,
): Promise<ServerHandle> {
  registerDiffProvider(context);
  attachAcceptCommand(context);

  const server = http.createServer(async (req, res) => {
    try {
      await handle(req, res, context, token);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      sendJson(res, 500, { error: 'internal_error', message });
    }
  });

  return new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(desiredPort, '127.0.0.1', () => {
      const addr = server.address();
      const port = typeof addr === 'object' && addr ? addr.port : 0;
      resolve({ server, port, token });
    });
  });
}

async function handle(
  req: http.IncomingMessage,
  res: http.ServerResponse,
  ctx: vscode.ExtensionContext,
  token: string,
): Promise<void> {
  const auth = req.headers['authorization'];
  if (auth !== `Bearer ${token}`) {
    return sendJson(res, 401, { error: 'unauthorized' });
  }

  const url = new URL(req.url ?? '/', 'http://127.0.0.1');
  const key = `${req.method} ${url.pathname}`;
  const handler = ROUTES[key];
  if (!handler) {
    return sendJson(res, 404, { error: 'not_found', path: url.pathname });
  }

  const body = await readJson(req);
  await handler({ method: req.method!, path: url.pathname, headers: req.headers, body, raw: req }, res, ctx);
}

async function readJson(req: http.IncomingMessage): Promise<unknown> {
  if (req.method === 'GET' || req.method === 'HEAD') return null;
  const chunks: Buffer[] = [];
  for await (const c of req) chunks.push(c as Buffer);
  if (!chunks.length) return null;
  const raw = Buffer.concat(chunks).toString('utf8');
  try { return JSON.parse(raw); } catch { return null; }
}

export function sendJson(res: http.ServerResponse, status: number, body: unknown): void {
  const json = JSON.stringify(body);
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(json),
  });
  res.end(json);
}

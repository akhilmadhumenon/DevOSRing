import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import * as http from 'http';
import { startServer, ServerHandle } from '../src/server';
import { __setWorkspaceFolder } from './mocks/vscode';

const TOKEN = 'test-token';
let handle: ServerHandle;

beforeAll(async () => {
  __setWorkspaceFolder(process.cwd());
  handle = await startServer({ subscriptions: [], extension: { packageJSON: { version: '1.0.0' } } } as any, TOKEN, 0);
});

afterAll(async () => {
  await new Promise<void>((resolve) => handle.server.close(() => resolve()));
});

function req(method: string, path: string, body?: unknown, token = TOKEN): Promise<{ status: number; body: any }> {
  return new Promise((resolve, reject) => {
    const data = body ? Buffer.from(JSON.stringify(body)) : undefined;
    const r = http.request({
      host: '127.0.0.1', port: handle.port, method, path,
      headers: {
        'Content-Type': 'application/json',
        ...(data ? { 'Content-Length': data.length } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    }, (res) => {
      const chunks: Buffer[] = [];
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => {
        const text = Buffer.concat(chunks).toString('utf8');
        let json: any = null; try { json = JSON.parse(text); } catch { /* */ }
        resolve({ status: res.statusCode ?? 0, body: json });
      });
    });
    r.on('error', reject);
    if (data) r.write(data);
    r.end();
  });
}

describe('server', () => {
  it('rejects unauthenticated requests with 401', async () => {
    const { status } = await req('GET', '/v1/ping', undefined, '');
    expect(status).toBe(401);
  });

  it('answers ping with 200', async () => {
    const { status, body } = await req('GET', '/v1/ping');
    expect(status).toBe(200);
    expect(body.ok).toBe(true);
  });

  it('returns 404 for unknown route', async () => {
    const { status } = await req('GET', '/v1/nope');
    expect(status).toBe(404);
  });

  it('serves a context payload', async () => {
    const { status, body } = await req('GET', '/v1/context');
    expect(status).toBe(200);
    expect(body).toHaveProperty('workspaceRoot');
    expect(body).toHaveProperty('ide');
  });

  it('rejects malformed notify body', async () => {
    const { status, body } = await req('POST', '/v1/notify', { level: 'info' });
    expect(status).toBe(400);
    expect(body.error).toBe('bad_request');
  });
});

import * as crypto from 'crypto';
import * as vscode from 'vscode';
import { startServer, ServerHandle } from './server';
import {
  writeDiscovery,
  updateDiscovery,
  markStopped,
  discoveryPath,
} from './discovery';

const HEARTBEAT_INTERVAL_MS = 5_000;

let handle: ServerHandle | undefined;
let statusBar: vscode.StatusBarItem | undefined;
let heartbeatTimer: NodeJS.Timeout | undefined;
let startedAt = 0;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
  statusBar.text = '$(plug) DevOS: starting...';
  statusBar.tooltip = 'DevOS Companion';
  statusBar.command = 'devos.status';
  statusBar.show();
  context.subscriptions.push(statusBar);

  context.subscriptions.push(
    vscode.commands.registerCommand('devos.restart', () => restart(context)),
    vscode.commands.registerCommand('devos.status', () => showStatus()),
  );

  await start(context);
}

export async function deactivate(): Promise<void> {
  await stop();
}

async function start(context: vscode.ExtensionContext): Promise<void> {
  const config = vscode.workspace.getConfiguration('devos');
  const desiredPort = config.get<number>('port', 0) ?? 0;
  const token = crypto.randomBytes(32).toString('hex');

  try {
    handle = await startServer(context, token, desiredPort);
    const version = context.extension.packageJSON.version ?? '0.0.0';
    startedAt = Date.now();
    await writeDiscovery({
      port: handle.port,
      token,
      pid: process.pid,
      version,
      ide: vscode.env.appName || 'unknown',
      state: 'ready',
      startedAt,
      heartbeatAt: startedAt,
    });
    startHeartbeat();
    if (statusBar) {
      statusBar.text = `$(plug) DevOS:${handle.port}`;
      statusBar.tooltip = `DevOS Companion v${version} on 127.0.0.1:${handle.port}\nDiscovery: ${discoveryPath()}`;
    }
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    if (statusBar) {
      statusBar.text = '$(error) DevOS';
      statusBar.tooltip = `DevOS Companion failed to start: ${message}`;
    }
    vscode.window.showErrorMessage(`DevOS Companion failed to start: ${message}`);
  }
}

async function stop(): Promise<void> {
  stopHeartbeat();
  // Mark stopped so the plugin-side auto-wake knows the extension was installed and just needs
  // to be re-activated (e.g. via launching/focusing the IDE). We intentionally do NOT delete
  // the file here — that used to cause "companion offline" right after a window reload.
  await markStopped();
  if (!handle) return;
  await new Promise<void>((resolve) => handle!.server.close(() => resolve()));
  handle = undefined;
}

async function restart(context: vscode.ExtensionContext): Promise<void> {
  await stop();
  await start(context);
  vscode.window.showInformationMessage('DevOS Companion restarted.');
}

function showStatus(): void {
  if (!handle) {
    vscode.window.showWarningMessage('DevOS Companion is not running.');
    return;
  }
  vscode.window.showInformationMessage(
    `DevOS Companion listening on 127.0.0.1:${handle.port}. Discovery file: ${discoveryPath()}`,
  );
}

function startHeartbeat(): void {
  stopHeartbeat();
  heartbeatTimer = setInterval(() => {
    void updateDiscovery({ heartbeatAt: Date.now() });
  }, HEARTBEAT_INTERVAL_MS);
  // Don't let the timer keep the extension host alive on shutdown.
  heartbeatTimer.unref?.();
}

function stopHeartbeat(): void {
  if (heartbeatTimer) {
    clearInterval(heartbeatTimer);
    heartbeatTimer = undefined;
  }
}

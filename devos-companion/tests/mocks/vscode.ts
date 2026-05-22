/**
 * Minimal `vscode` API stub for unit tests. Only the surface used by the routes
 * is implemented; everything else throws so we notice when route code starts
 * relying on something we have not stubbed.
 */
import { EventEmitter } from 'events';

export const ViewColumn = { Beside: -2, Active: -1, One: 1 } as const;

export const env = { appName: 'TestIDE' };

export class Position { constructor(public line: number, public character: number) {} }
export class Range {
  constructor(public start: Position, public end: Position) {}
  get isEmpty() { return this.start.line === this.end.line && this.start.character === this.end.character; }
}
export class Selection extends Range {}

export class Uri {
  private constructor(public scheme: string, public fsPath: string, public path: string, public query: string = '') {}
  static file(p: string): Uri { return new Uri('file', p, p); }
  static parse(s: string): Uri {
    const [scheme, rest] = s.split(':');
    const [path, query = ''] = (rest ?? '').split('?');
    return new Uri(scheme ?? 'file', path ?? '', path ?? '', query);
  }
  toString() { return `${this.scheme}:${this.path}${this.query ? '?' + this.query : ''}`; }
}

class EmitterShim<T> {
  private bus = new EventEmitter();
  event = (listener: (e: T) => void) => { this.bus.on('e', listener); return { dispose() {} }; };
  fire(e: T) { this.bus.emit('e', e); }
}

export const EventEmitter_ = EmitterShim;
export { EmitterShim as EventEmitter };

let activeEditor: any = null;
let workspaceFolder: string | null = null;
let lastNotice: { level: string; msg: string } | null = null;

export const window = {
  get activeTextEditor() { return activeEditor; },
  showInformationMessage(m: string, ..._buttons: string[]) {
    lastNotice = { level: 'info', msg: m };
    return Promise.resolve(undefined);
  },
  showWarningMessage(m: string) { lastNotice = { level: 'warning', msg: m }; return Promise.resolve(undefined); },
  showErrorMessage(m: string)   { lastNotice = { level: 'error',   msg: m }; return Promise.resolve(undefined); },
  createWebviewPanel(_id: string, title: string, _opt: unknown) {
    return {
      title,
      webview: { html: '' },
      reveal() {},
      onDidDispose() { return { dispose() {} }; },
    };
  },
  createStatusBarItem() {
    return { text: '', tooltip: '', command: '', show() {}, dispose() {} };
  },
};

export const workspace = {
  workspaceFolders: workspaceFolder ? [{ uri: Uri.file(workspaceFolder) }] : undefined,
  getConfiguration() { return { get<T>(_k: string, d: T) { return d; } }; },
  applyEdit() { return Promise.resolve(true); },
  openTextDocument() { return Promise.resolve({ save() { return Promise.resolve(true); } }); },
  registerTextDocumentContentProvider() { return { dispose() {} }; },
};

export const commands = {
  registerCommand() { return { dispose() {} }; },
  executeCommand() { return Promise.resolve(); },
};

export const StatusBarAlignment = { Right: 2, Left: 1 };

export class WorkspaceEdit { replace(_uri: Uri, _range: Range, _text: string) { /* noop */ } }

// Test helpers
export function __setActiveEditor(e: any) { activeEditor = e; }
export function __setWorkspaceFolder(p: string | null) {
  workspaceFolder = p;
  (workspace as any).workspaceFolders = p ? [{ uri: Uri.file(p) }] : undefined;
}
export function __lastNotice() { return lastNotice; }

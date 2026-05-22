import * as vscode from 'vscode';
import { Handler, sendJson } from '../server';
import { DiffRequest, DiffResponse } from '../types';

const SCHEME = 'devos-refactor';

/**
 * In-memory store of refactored contents keyed by a synthetic devos-refactor:// URI.
 * The diff view shows {original (file://)} vs {refactored (devos-refactor://)} and the
 * user clicks the "Accept DevOS Refactor" command to write the refactored text back.
 */
const refactoredStore = new Map<string, string>();
const pendingDecisions = new Map<string, (accepted: boolean) => void>();

class RefactorProvider implements vscode.TextDocumentContentProvider {
  onDidChangeEmitter = new vscode.EventEmitter<vscode.Uri>();
  onDidChange = this.onDidChangeEmitter.event;

  provideTextDocumentContent(uri: vscode.Uri): string {
    return refactoredStore.get(uri.toString()) ?? '';
  }
}

const provider = new RefactorProvider();

export function registerDiffProvider(ctx: vscode.ExtensionContext): void {
  ctx.subscriptions.push(
    vscode.workspace.registerTextDocumentContentProvider(SCHEME, provider),
  );
}

export function attachAcceptCommand(ctx: vscode.ExtensionContext): void {
  ctx.subscriptions.push(
    vscode.commands.registerCommand('devos.acceptRefactor', async (uriString?: string) => {
      const key = uriString ?? vscode.window.activeTextEditor?.document.uri.toString();
      if (!key) return;
      const refactored = refactoredStore.get(key);
      if (refactored === undefined) {
        vscode.window.showWarningMessage('DevOS: no pending refactor in this diff.');
        return;
      }
      const filePath = decodeFilePath(key);
      const fileUri = vscode.Uri.file(filePath);
      const edit = new vscode.WorkspaceEdit();
      const fullRange = new vscode.Range(
        new vscode.Position(0, 0),
        new vscode.Position(1_000_000, 0),
      );
      edit.replace(fileUri, fullRange, refactored);
      const applied = await vscode.workspace.applyEdit(edit);
      if (applied) {
        await vscode.workspace.openTextDocument(fileUri).then(d => d.save());
        vscode.window.showInformationMessage('DevOS: refactor applied.');
      } else {
        vscode.window.showErrorMessage('DevOS: failed to apply refactor edit.');
      }
      const decide = pendingDecisions.get(key);
      decide?.(applied);
      pendingDecisions.delete(key);
      refactoredStore.delete(key);
    }),
    vscode.commands.registerCommand('devos.discardRefactor', async (uriString?: string) => {
      const key = uriString ?? vscode.window.activeTextEditor?.document.uri.toString();
      if (!key) return;
      const decide = pendingDecisions.get(key);
      decide?.(false);
      pendingDecisions.delete(key);
      refactoredStore.delete(key);
    }),
  );
}

export const diffRoute: Handler = async (req, res) => {
  const body = req.body as DiffRequest | null;
  if (!body?.path || typeof body.refactoredText !== 'string') {
    return sendJson(res, 400, { error: 'bad_request', message: 'path and refactoredText required' });
  }
  const fileUri = vscode.Uri.file(body.path);
  const refactorUri = vscode.Uri.parse(`${SCHEME}:${encodeURIComponent(body.path)}?ts=${Date.now()}`);
  refactoredStore.set(refactorUri.toString(), body.refactoredText);
  provider.onDidChangeEmitter.fire(refactorUri);

  const title = body.title ?? 'DevOS Refactor';
  await vscode.commands.executeCommand('vscode.diff', fileUri, refactorUri, title, {
    preview: false,
  });

  // Offer accept/discard buttons. We deliberately use a notification (not a code lens)
  // so it stays in view while the user reads the diff.
  const decision = await new Promise<boolean>((resolve) => {
    pendingDecisions.set(refactorUri.toString(), resolve);
    vscode.window
      .showInformationMessage('DevOS: apply this refactor?', 'Apply', 'Discard')
      .then(async (choice) => {
        if (choice === 'Apply') {
          await vscode.commands.executeCommand('devos.acceptRefactor', refactorUri.toString());
        } else {
          await vscode.commands.executeCommand('devos.discardRefactor', refactorUri.toString());
        }
      });
  });

  const response: DiffResponse = { accepted: decision };
  sendJson(res, 200, response);
};

function decodeFilePath(refactorUriString: string): string {
  const uri = vscode.Uri.parse(refactorUriString);
  return decodeURIComponent(uri.path);
}

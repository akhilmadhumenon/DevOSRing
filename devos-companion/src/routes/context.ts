import * as vscode from 'vscode';
import { Handler, sendJson } from '../server';
import { probeGit } from '../git';
import { WorkspaceContext } from '../types';

export const contextRoute: Handler = async (_req, res) => {
  const editor = vscode.window.activeTextEditor;
  const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? null;

  const activeFilePath = editor?.document.uri.scheme === 'file' ? editor.document.uri.fsPath : null;
  const language = editor?.document.languageId ?? null;

  const selection = editor && !editor.selection.isEmpty
    ? {
        text: editor.document.getText(editor.selection),
        startLine: editor.selection.start.line + 1,
        endLine: editor.selection.end.line + 1,
        isEmpty: false,
      }
    : editor
    ? { text: '', startLine: 0, endLine: 0, isEmpty: true }
    : null;

  const git = await probeGit(activeFilePath
    ? require('path').dirname(activeFilePath)
    : workspaceRoot);

  const body: WorkspaceContext = {
    activeFilePath,
    workspaceRoot,
    language,
    selection,
    gitRoot: git.root,
    gitBranch: git.branch,
    isDirty: git.isDirty,
    ide: detectIde(),
  };
  sendJson(res, 200, body);
};

function detectIde(): string {
  // VS Code, Cursor, and Antigravity all derive from the same Electron host but
  // each ship a distinct app name. vscode.env.appName reports it verbatim.
  return vscode.env.appName || 'unknown';
}

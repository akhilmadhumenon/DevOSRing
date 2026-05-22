import * as vscode from 'vscode';
import { Handler, sendJson } from '../server';
import { ApplyRequest } from '../types';

export const applyRoute: Handler = async (req, res) => {
  const body = req.body as ApplyRequest | null;
  if (!body?.path || typeof body.text !== 'string') {
    return sendJson(res, 400, { error: 'bad_request', message: 'path and text required' });
  }
  const fileUri = vscode.Uri.file(body.path);
  const edit = new vscode.WorkspaceEdit();
  const fullRange = new vscode.Range(
    new vscode.Position(0, 0),
    new vscode.Position(1_000_000, 0),
  );
  edit.replace(fileUri, fullRange, body.text);
  const applied = await vscode.workspace.applyEdit(edit);
  if (applied) {
    const doc = await vscode.workspace.openTextDocument(fileUri);
    await doc.save();
  }
  sendJson(res, applied ? 200 : 500, { applied });
};

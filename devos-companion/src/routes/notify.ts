import * as vscode from 'vscode';
import { Handler, sendJson } from '../server';
import { NotifyRequest } from '../types';

export const notifyRoute: Handler = async (req, res) => {
  const body = req.body as NotifyRequest | null;
  if (!body?.message) {
    return sendJson(res, 400, { error: 'bad_request', message: 'message required' });
  }
  switch (body.level) {
    case 'error':   vscode.window.showErrorMessage(body.message);   break;
    case 'warning': vscode.window.showWarningMessage(body.message); break;
    default:        vscode.window.showInformationMessage(body.message); break;
  }
  sendJson(res, 200, { ok: true });
};

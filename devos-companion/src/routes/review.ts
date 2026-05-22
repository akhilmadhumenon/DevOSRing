import * as vscode from 'vscode';
import { marked } from 'marked';
import { Handler, sendJson } from '../server';
import { ReviewRequest } from '../types';

let panel: vscode.WebviewPanel | undefined;

export const reviewRoute: Handler = async (req, res) => {
  const body = req.body as ReviewRequest | null;
  if (typeof body?.markdown !== 'string') {
    return sendJson(res, 400, { error: 'bad_request', message: 'markdown required' });
  }

  const title = body.title ?? 'DevOS AI Review';
  if (panel) {
    panel.title = title;
    panel.reveal(vscode.ViewColumn.Beside, true);
  } else {
    panel = vscode.window.createWebviewPanel(
      'devosReview', title, { viewColumn: vscode.ViewColumn.Beside, preserveFocus: true },
      { enableScripts: false, retainContextWhenHidden: true },
    );
    panel.onDidDispose(() => { panel = undefined; });
  }
  panel.webview.html = render(title, body.markdown);
  sendJson(res, 200, { ok: true });
};

function render(title: string, md: string): string {
  const escapedTitle = escapeHtml(title);
  const html = marked.parse(md, { async: false }) as string;
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; img-src https: data:;" />
  <title>${escapedTitle}</title>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif; padding: 1.5em 2em; line-height: 1.55; color: var(--vscode-foreground); }
    h1, h2, h3 { border-bottom: 1px solid var(--vscode-panel-border); padding-bottom: 0.25em; }
    code { background: var(--vscode-textCodeBlock-background); padding: 0.1em 0.35em; border-radius: 3px; }
    pre code { display: block; padding: 0.85em; overflow: auto; }
    ul, ol { padding-left: 1.5em; }
    hr { border: none; border-top: 1px solid var(--vscode-panel-border); margin: 1.5em 0; }
    blockquote { border-left: 3px solid var(--vscode-textBlockQuote-border); padding-left: 1em; color: var(--vscode-descriptionForeground); margin: 0.5em 0; }
  </style>
</head>
<body>
${html}
</body>
</html>`;
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

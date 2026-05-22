export interface WorkspaceContext {
  activeFilePath: string | null;
  workspaceRoot: string | null;
  language: string | null;
  selection: {
    text: string;
    startLine: number;
    endLine: number;
    isEmpty: boolean;
  } | null;
  gitRoot: string | null;
  gitBranch: string | null;
  isDirty: boolean;
  ide: string;
}

export interface DiffRequest {
  path: string;
  refactoredText: string;
  title?: string;
}

export interface DiffResponse {
  accepted: boolean;
}

export interface ApplyRequest {
  path: string;
  text: string;
}

export interface ReviewRequest {
  markdown: string;
  title?: string;
}

export interface NotifyRequest {
  level: 'info' | 'warning' | 'error';
  message: string;
}

export interface DiscoveryDoc {
  port: number;
  token: string;
  pid: number;
  version: string;
  ide: string;
}

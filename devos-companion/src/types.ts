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
  /** True if the user has already accepted; usually false because accept happens async. */
  accepted: boolean;
  /** True when the diff view was opened in the IDE. */
  opened?: boolean;
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

export type CompanionState = 'starting' | 'ready' | 'stopped';

export interface DiscoveryDoc {
  port: number;
  token: string;
  pid: number;
  version: string;
  ide: string;
  /** Lifecycle state — plugins refuse to use anything other than "ready". */
  state: CompanionState;
  /** Epoch ms when the server first bound. */
  startedAt: number;
  /** Epoch ms updated every few seconds while the extension is alive. */
  heartbeatAt: number;
  /** Epoch ms when deactivate() ran. Only present when state === "stopped". */
  stoppedAt?: number;
}

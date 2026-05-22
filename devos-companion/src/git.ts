import { exec } from 'child_process';
import { promisify } from 'util';

const pExec = promisify(exec);

export interface GitState {
  root: string | null;
  branch: string | null;
  isDirty: boolean;
}

/**
 * Probes git state from the CLI rather than re-implementing it. All commands are
 * short-lived and bounded by a 2-second timeout per invocation.
 */
export async function probeGit(cwd: string | null): Promise<GitState> {
  if (!cwd) {
    return { root: null, branch: null, isDirty: false };
  }
  const root = await safeOne('git rev-parse --show-toplevel', cwd);
  if (!root) {
    return { root: null, branch: null, isDirty: false };
  }
  const [branch, status] = await Promise.all([
    safeOne('git rev-parse --abbrev-ref HEAD', root),
    safeOne('git status --porcelain', root),
  ]);
  return {
    root,
    branch,
    isDirty: !!(status && status.length > 0),
  };
}

async function safeOne(cmd: string, cwd: string): Promise<string | null> {
  try {
    const { stdout } = await pExec(cmd, { cwd, timeout: 2000, encoding: 'utf8' });
    return stdout.trim() || null;
  } catch {
    return null;
  }
}

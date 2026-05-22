import * as fs from 'fs/promises';
import * as os from 'os';
import * as path from 'path';
import { DiscoveryDoc } from './types';

const FOLDER = path.join(os.homedir(), '.devos');
const FILE = path.join(FOLDER, 'companion.json');

/** Writes the discovery doc with mode 0600. The .NET CompanionDiscovery reads this file. */
export async function writeDiscovery(doc: DiscoveryDoc): Promise<void> {
  await fs.mkdir(FOLDER, { recursive: true, mode: 0o700 });
  const tmp = FILE + '.tmp';
  await fs.writeFile(tmp, JSON.stringify(doc, null, 2), { mode: 0o600 });
  await fs.rename(tmp, FILE);
}

/**
 * Merges a partial update into the on-disk doc. Used by the heartbeat tick and lifecycle
 * transitions (starting → ready → stopped). Best-effort: if the file is missing or unreadable
 * we silently no-op, because the next full writeDiscovery() will recover.
 */
export async function updateDiscovery(patch: Partial<DiscoveryDoc>): Promise<void> {
  try {
    const raw = await fs.readFile(FILE, 'utf8');
    const current = JSON.parse(raw) as DiscoveryDoc;
    const next: DiscoveryDoc = { ...current, ...patch };
    const tmp = FILE + '.tmp';
    await fs.writeFile(tmp, JSON.stringify(next, null, 2), { mode: 0o600 });
    await fs.rename(tmp, FILE);
  } catch {
    /* ignore - heartbeat is best-effort */
  }
}

/**
 * Marks the companion as stopped without removing the file. This lets plugins distinguish
 * "extension was here recently but currently sleeping" from "extension never installed",
 * which the auto-wake path in DevOSCore uses to decide whether to relaunch the IDE.
 */
export async function markStopped(): Promise<void> {
  try {
    await updateDiscovery({ state: 'stopped', stoppedAt: Date.now() });
  } catch {
    /* ignore */
  }
}

/** Removes the file entirely. Reserved for uninstall flows; normal deactivate uses markStopped. */
export async function clearDiscovery(): Promise<void> {
  try {
    await fs.unlink(FILE);
  } catch {
    /* ignore */
  }
}

export function discoveryPath(): string {
  return FILE;
}

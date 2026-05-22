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

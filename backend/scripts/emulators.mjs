import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdir, mkdtemp, readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { assertSafeOverrides, LOCAL_PROJECT, LOCAL_ENDPOINTS } from '../functions/lib/localEnvironment.js';

const root = fileURLToPath(new URL('../', import.meta.url));
const mode = process.argv[2];
if (!['start', 'test'].includes(mode) || process.argv.length !== 3) {
  throw new Error('Use npm run emulators or npm run test:emulators; extra arguments are not allowed.');
}
if (Number(process.versions.node.split('.')[0]) !== 22) throw new Error('Node.js 22 required.');
assertSafeOverrides(process.env);
const config = JSON.parse(await readFile(path.join(root, 'firebase.json'), 'utf8'));
const expectedPorts = { auth: 9099, firestore: 8080, functions: 5001, hub: 4400, logging: 4500 };
for (const [name, port] of Object.entries(expectedPorts)) {
  if (config.emulators[name]?.host !== '127.0.0.1' || config.emulators[name]?.port !== port) {
    throw new Error(`Unsafe emulator configuration: ${name}.`);
  }
}
if (config.emulators.ui?.enabled !== false || config.emulators.firestore.websocketPort !== 9150 ||
    Object.keys(config.emulators).some(key => ![...Object.keys(expectedPorts), 'ui', 'singleProjectMode'].includes(key))) {
  throw new Error('Unexpected emulator configuration.');
}
// Probe only; never kill a process occupying an emulator port.
for (const port of [...Object.values(expectedPorts), 9150]) {
  await new Promise((resolve, reject) => {
    const server = createServer();
    server.once('error', () => reject(new Error(`Local port ${port} is occupied; no process was stopped.`)));
    server.listen(port, '127.0.0.1', () => server.close(resolve));
  });
}

// Child environment is allowlisted: no Firebase token, ADC path, proxy or arbitrary SDK overrides.
const env = {};
for (const [key, value] of Object.entries(process.env)) {
  if (/^(path|systemroot|windir|comspec|pathext|temp|tmp|java_home|lang|lc_all)$/i.test(key)) env[key] = value;
}
const local = path.join(root, '.local');
await mkdir(path.join(local, 'config'), { recursive: true });
const isolatedConfig = await mkdtemp(path.join(local, 'config', 'run-'));
Object.assign(env, LOCAL_ENDPOINTS, {
  GCLOUD_PROJECT: LOCAL_PROJECT,
  GOOGLE_CLOUD_PROJECT: LOCAL_PROJECT,
  FIREBASE_CONFIG: JSON.stringify({ projectId: LOCAL_PROJECT }),
  XDG_CONFIG_HOME: isolatedConfig,
  APPDATA: isolatedConfig,
  METADATA_SERVER_DETECTION: 'none',
  FIREBASE_EMULATORS_PATH: path.join(local, 'emulators'),
  CI: 'true',
  FIREBASE_CLI_DISABLE_TELEMETRY: 'true',
  NO_UPDATE_NOTIFIER: '1',
});
const args = [path.join(root, 'node_modules/firebase-tools/lib/bin/firebase.js'),
  mode === 'test' ? 'emulators:exec' : 'emulators:start',
  '--project', LOCAL_PROJECT, '--config', path.join(root, 'firebase.json'),
  '--only', 'auth,firestore,functions', '--non-interactive'];
if (mode === 'test') args.push('node --test tests/emulators.test.mjs');
const child = spawn(process.execPath, args, { cwd: root, env, stdio: 'inherit', windowsHide: true });
child.on('error', error => { console.error(error.message); process.exitCode = 1; });
child.on('exit', code => { process.exitCode = code ?? 1; });
// CLI receives terminal Ctrl+C and performs its own emulator shutdown.
process.on('SIGINT', () => {});

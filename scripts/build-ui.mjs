// Compiles the DOM companion (src/ui) against the Engine pair's own UI
// declarations. The Host project runs this as its RustyEngineProductUiBuildCommand
// and passes $(RustyEngineProductUiTypes), the pair's `@rusty-engine/product-ui`
// and `@rusty-engine/live-debug` declarations. `tsc -p` cannot take an extra
// input file, so a throwaway config extends src/ui/tsconfig.json with it: the
// compiler options keep one home. Node runs the repository's own TypeScript,
// so the command is the same under cmd.exe and sh.
//
// usage: node scripts/build-ui.mjs <path to rusty-engine-product-ui.d.ts>
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const types = process.argv[2];
if (!types || !existsSync(types)) {
  console.error(`Engine UI declarations not found: ${types} (run rusty install).`);
  process.exit(1);
}
const repoRoot = resolve(import.meta.dirname, '..');
const tsc = createRequire(join(repoRoot, 'package.json')).resolve('typescript/bin/tsc');
const configDir = mkdtempSync(join(tmpdir(), 'build-ui-'));
try {
  const config = join(configDir, 'tsconfig.json');
  writeFileSync(config, JSON.stringify({
    extends: join(repoRoot, 'src', 'ui', 'tsconfig.json'),
    files: [join(repoRoot, 'src', 'ui', 'main.ts'), resolve(types)],
  }));
  execFileSync(process.execPath, [tsc, '-p', config], { stdio: 'inherit' });
} catch (error) {
  process.exitCode = error.status ?? 1;
} finally {
  rmSync(configDir, { recursive: true, force: true });
}

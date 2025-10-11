import { promises as fs } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { transform } from 'esbuild';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webRoot = path.resolve(__dirname, 'wwwroot');
const distPath = path.join(webRoot, 'dist');

const cssSources = [
  path.join('lib', 'bootstrap', 'dist', 'css', 'bootstrap.min.css'),
  path.join('css', 'design-system.css'),
  path.join('css', 'utilities.css'),
  path.join('css', 'components', 'buttons.css'),
  path.join('css', 'components', 'forms.css'),
  path.join('css', 'components', 'modals.css'),
  path.join('css', 'responsive.css'),
  path.join('css', 'site.css'),
  path.join('css', 'home.css'),
  path.join('css', 'admin.css')
];

const jsSources = [
  path.join('lib', 'jquery', 'dist', 'jquery.min.js'),
  path.join('lib', 'bootstrap', 'dist', 'js', 'bootstrap.bundle.min.js'),
  path.join('js', 'site.js')
];

async function readFiles(files) {
  const results = [];
  for (const relativePath of files) {
    const absolutePath = path.join(webRoot, relativePath);
    try {
      await fs.access(absolutePath);
    } catch (error) {
      throw new Error(`Missing bundle source: ${absolutePath}`);
    }

    const content = await fs.readFile(absolutePath, 'utf8');
    results.push(content);
  }

  return results;
}

async function buildCss() {
  const contents = await readFiles(cssSources);
  const combined = contents.join('\n');
  const { code } = await transform(combined, {
    loader: 'css',
    minify: true,
    logLevel: 'error'
  });

  const destination = path.join(distPath, 'styles.min.css');
  await fs.writeFile(destination, `${code}\n`, 'utf8');
}

async function buildJs() {
  const contents = await readFiles(jsSources);
  const combined = contents.join('\n;\n');
  const { code } = await transform(combined, {
    loader: 'js',
    minify: true,
    logLevel: 'error'
  });

  const destination = path.join(distPath, 'scripts.min.js');
  await fs.writeFile(destination, `${code}\n`, 'utf8');
}

async function main() {
  await fs.mkdir(distPath, { recursive: true });

  await Promise.all([buildCss(), buildJs()]);

  console.log('Static assets bundled to', distPath);
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});

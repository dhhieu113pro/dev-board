import assert from 'node:assert/strict';
import { access, readFile } from 'node:fs/promises';
import test from 'node:test';

const dashboardUrl = new URL('../src/Views/DevSpaceDashboard.axaml', import.meta.url);

const agents = [
  ['Copilot', 'githubcopilot.ico'],
  ['Codex', 'codex.ico'],
  ['Antigravity', 'antigravity.ico'],
];

test('DevSpace quick-start agents render their bundled brand icons', async () => {
  const dashboard = await readFile(dashboardUrl, 'utf8');

  for (const [label, fileName] of agents) {
    const iconUrl = new URL(`../src/Resources/Images/ExternalToolIcons/${fileName}`, import.meta.url);
    await access(iconUrl);

    const escapedFileName = fileName.replace('.', '\\.');
    const pattern = new RegExp(
      `<Image[^>]*Source="/Resources/Images/ExternalToolIcons/${escapedFileName}"[^>]*/>\\s*<TextBlock[^>]*Text="${label}"`,
    );

    assert.match(dashboard, pattern, `${label} should render ${fileName}`);
  }
});

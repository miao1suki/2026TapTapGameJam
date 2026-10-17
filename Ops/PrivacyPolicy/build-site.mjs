import { createHash } from 'node:crypto';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const repository = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
export const policyPath = 'Assets/_Project/Resources/Privacy/privacy-policy.txt';
const nativePath = 'Assets/Plugins/Android/PrivacyGate.androidlib/src/com/miao1suki/shapeofcolor/privacy/PrivacyConsent.java';

export function buildSite() {
  const policy = readFileSync(resolve(repository, policyPath), 'utf8');
  if (!policy.startsWith('<h1>形之彩隐私政策</h1>') || !policy.includes('如何联系我们') ||
      /<(?:script|iframe|object|embed)\b|\bon\w+\s*=/i.test(policy))
    throw new Error('政策正文不完整或包含主动执行内容，停止生成。');
  const native = readFileSync(resolve(repository, nativePath), 'utf8');
  const url = native.match(/public static final String POLICY_URL = "([^"]+)";/)?.[1];
  if (!url || !url.startsWith('https://meowgame.cloud/') || new URL(url).search || new URL(url).hash)
    throw new Error('缺少固定 HTTPS 隐私政策地址。');
  const digest = createHash('sha256').update(policy).digest('hex');
  const template = readFileSync(resolve(repository, 'Ops/PrivacyPolicy/site-shell.html'), 'utf8');
  const html = template.replace('{{POLICY_URL}}', url).replace('{{POLICY_SHA256}}', digest)
    .replace('{{POLICY_HTML}}', policy);
  return { html, policy, url, digest };
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const destination = resolve(repository, process.argv[2] ?? 'Library/PrivacyWebsite/index.html');
  const site = buildSite();
  mkdirSync(dirname(destination), { recursive: true });
  writeFileSync(destination, site.html, 'utf8');
  console.log(JSON.stringify({ destination, url: site.url, policySha256: site.digest, bytes: Buffer.byteLength(site.html) }));
}

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { buildSite } from './build-site.mjs';

test('网站正文逐字等于游戏内唯一政策源，不改授权版本', () => {
  const { html, policy, digest } = buildSite();
  const body = html.split('<!-- privacy-policy:start -->')[1].split('<!-- privacy-policy:end -->')[0];
  assert.equal(body, policy);
  assert.equal(digest, createHash('sha256').update(policy).digest('hex'));
  assert.equal((html.match(/<h1>/g) ?? []).length, 1);
});

test('仅固定HTTPS政策地址与主页现有ICP备案，没有脚本或外部资源', () => {
  const { html, url } = buildSite();
  assert.equal(url, 'https://meowgame.cloud/shape-of-color/privacy/');
  assert.ok(html.includes(`rel="canonical" href="${url}"`));
  assert.ok(html.includes('href="https://beian.miit.gov.cn/"'));
  assert.ok(html.includes('豫ICP备2026018798号'));
  assert.doesNotMatch(html, /<(?:script|iframe|img|object|embed|form)\b/i);
  assert.doesNotMatch(html, /\bsrc\s*=|@import|url\(|<link[^>]+stylesheet|\{\{/i);
});

test('页面为自适应中文长文，只有政策和备案页脚', () => {
  const { html } = buildSite();
  assert.ok(html.includes('lang="zh-CN"'));
  assert.ok(html.includes('width=device-width'));
  assert.ok(html.includes('overflow-x: auto'));
  assert.doesNotMatch(html, /<nav|<header|<button|<input|<video|<audio/i);
  assert.equal((html.match(/<footer>/g) ?? []).length, 1);
});

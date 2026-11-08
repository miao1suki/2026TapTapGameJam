import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { reviewPrepare, prototypePrepare, apiBase, serviceConfig } from './build-workflows.mjs';
import { upgradeWorkflow } from './update-local-workflows.mjs';
import vm from 'node:vm';
const AsyncFunction = Object.getPrototypeOf(async function(){}).constructor;
// Mirror the task runner's restricted globals; a normal Node function would hide URL-global bugs.
const run = async (source, input, bytes = Buffer.from('fixture')) => vm.runInNewContext('(async function($input){'+source+'})',{Buffer}).call({helpers:{getBinaryDataBuffer:async()=>bytes}}, {first:()=>({...input,json:{serviceConfig,...input.json}})});
assert.equal(apiBase('https://sui-xiang.com/v1/','v1/images/edits'),'https://sui-xiang.com/v1/images/edits');
assert.equal(apiBase('https://sui-xiang.com/v1beta','v1beta/models/x:generateContent'),'https://sui-xiang.com/v1beta/models/x:generateContent');
assert.throws(()=>apiBase('https://user:secret@example.com','v1/responses'));
assert.throws(()=>apiBase('https://example.com?key=secret','v1/responses'));
assert.throws(()=>apiBase('http://example.com','v1/responses'));
const png = {mimeType:'image/png',data:'fixture'};
const review = await run(reviewPrepare,{json:{制作说明:'测试墙面',动画时序与效果说明:'静态'},binary:{image:png}});
assert.equal(review[0].json.image,Buffer.from('fixture').toString('base64'));
assert.equal(new URL(review[0].json.openaiUrl).origin,'https://sui-xiang.com');
assert.equal(new URL(review[0].json.geminiUrl).origin,'https://sui-xiang.com');
await assert.rejects(run(reviewPrepare,{json:{制作说明:'测试'},binary:{}}),/请选择/);
await assert.rejects(run(reviewPrepare,{json:{制作说明:'测试'},binary:{image:png}},Buffer.alloc(5*1024*1024+1)),/5 MB/);
const text = await run(prototypePrepare,{json:{原型需求:'洞穴房间'},binary:{}});
assert.equal(text[0].json.model,'gpt-image-2');
assert.equal(text[0].json.hasReference,false);
assert.equal(text[0].json.n,1);
assert.equal(text[0].json.generateUrl,'https://sui-xiang.com/v1/images/generations');
const reference = await run(prototypePrepare,{json:{原型需求:'保持轮廓'},binary:{upload:png}});
assert.equal(reference[0].json.hasReference,true);
assert.equal(reference[0].binary.reference,png);
for(const name of ['review','prototype-image2','offline']) {
 const workflow=JSON.parse(await readFile(new URL(`./workflows/${name}.json`,import.meta.url),'utf8'));
 assert.equal(workflow.active,false);
 const names=new Set(workflow.nodes.map(n=>n.name));
 assert.equal(names.size,workflow.nodes.length);
 for(const [source,outputs] of Object.entries(workflow.connections)) {
  assert(names.has(source));
  for(const group of outputs.main) for(const edge of group??[]) assert(names.has(edge.node));
 }
 for(const node of workflow.nodes) {
  assert.equal(node.credentials,undefined);
  if(node.type==='n8n-nodes-base.code')new AsyncFunction('$input',node.parameters.jsCode);
  assert(!node.retryOnFail,'No automatic paid retries');
  assert.notEqual(node.parameters.nodeCredentialType,'anthropicApi','Claude must only run through its client');
  if(node.type==='n8n-nodes-base.httpRequest') assert(!/api\.openai\.com|api\.anthropic\.com|googleapis\.com/.test(node.parameters.url),'No hidden official destination');
 }
 if(name==='review') {
  assert.equal(workflow.nodes.filter(n=>n.type==='n8n-nodes-base.httpRequest').length,2);
  assert.equal(workflow.nodes.find(n=>n.name==='汇集审查').parameters.numberInputs,2);
  const local=structuredClone(workflow);
  local.nodes.find(n=>n.name==='OpenAI 审查').credentials={openAiApi:{id:'local-fixture-id',name:'自定义本机凭据'}};
  const migrated=upgradeWorkflow(local,workflow,[]);
  assert.deepEqual(migrated.nodes.find(n=>n.name==='OpenAI 审查').credentials,local.nodes.find(n=>n.name==='OpenAI 审查').credentials);
  local.nodes.push({name:'用户自定义节点'});
  assert.throws(()=>upgradeWorkflow(local,workflow,[]),/自定义节点/);
 }
 if(name==='offline')assert(workflow.nodes.every(n=>n.type!=='n8n-nodes-base.httpRequest'));
}
console.log('PASS offline fixtures, reference/text routes, upload limit, code syntax, inactive graphs. No model/API called.');

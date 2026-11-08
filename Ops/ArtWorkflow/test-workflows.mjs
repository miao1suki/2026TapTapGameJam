import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { reviewPrepare, prototypePrepare } from './build-workflows.mjs';
const AsyncFunction = Object.getPrototypeOf(async function(){}).constructor;
const run = async (source, input, bytes = Buffer.from('fixture')) => new AsyncFunction('$input', source).call({helpers:{getBinaryDataBuffer:async()=>bytes}}, {first:()=>input});
const png = {mimeType:'image/png',data:'fixture'};
const review = await run(reviewPrepare,{json:{制作说明:'测试墙面',动画时序与效果说明:'静态'},binary:{image:png}});
assert.equal(review[0].json.image,Buffer.from('fixture').toString('base64'));
await assert.rejects(run(reviewPrepare,{json:{制作说明:'测试'},binary:{}}),/请选择/);
await assert.rejects(run(reviewPrepare,{json:{制作说明:'测试'},binary:{image:png}},Buffer.alloc(5*1024*1024+1)),/5 MB/);
const text = await run(prototypePrepare,{json:{原型需求:'洞穴房间'},binary:{}});
assert.equal(text[0].json.model,'gpt-image-2');
assert.equal(text[0].json.hasReference,false);
assert.equal(text[0].json.n,1);
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
 }
 if(name==='offline')assert(workflow.nodes.every(n=>n.type!=='n8n-nodes-base.httpRequest'));
}
console.log('PASS offline fixtures, reference/text routes, upload limit, code syntax, inactive graphs. No model/API called.');

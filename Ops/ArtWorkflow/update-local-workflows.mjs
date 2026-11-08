// Only workflow definitions and credential IDs/names/types are read. Never select credential data.
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

export function upgradeWorkflow(current, template, credentials = []) {
  const known = new Set([...template.nodes.map(n=>n.name),'Claude 审查','整理 Claude']);
  if(current.nodes.some(n=>!known.has(n.name))) throw new Error('检测到自定义节点，停止覆盖；请人工合并本机导出。');
  const nodes = template.nodes.map(source=>{
    const node=structuredClone(source);
    const previous=current.nodes.find(n=>n.id===node.id || n.name===node.name);
    if(previous) {
      node.position=previous.position;
      if(previous.credentials) node.credentials=structuredClone(previous.credentials);
    }
    if(node.type==='n8n-nodes-base.httpRequest') {
      const type=node.parameters.nodeCredentialType;
      if(!node.credentials?.[type]) {
        const candidates=credentials.filter(c=>c.type===type && (type!=='openAiApi' ||
          (template.id==='xzc-image2-prototype-v1' ? c.name==='工作流Image2' : c.name!=='工作流Image2')));
        if(candidates.length===1) node.credentials={[type]:{id:candidates[0].id,name:candidates[0].name}};
      }
      const selected=credentials.find(c=>c.id===node.credentials?.[type]?.id);
      if(selected)node.credentials[type].name=selected.name;
    }
    return node;
  });
  return {...current,name:template.name,nodes,connections:template.connections,active:false};
}

if(process.argv[1]===fileURLToPath(import.meta.url)) {
  const [snapshotPath,outputDirectory]=process.argv.slice(2);
  if(!snapshotPath || !outputDirectory)throw new Error('需要本机工作流备份与输出目录');
  const root=path.join(process.env.LOCALAPPDATA,'XingZhiCai','ArtWorkflow');
  const require=createRequire(path.join(root,'package.json'));
  const sqlite3=require('sqlite3');
  const database=await new Promise((resolve,reject)=>{const db=new sqlite3.Database(path.join(root,'data','.n8n','database.sqlite'),sqlite3.OPEN_READONLY,e=>e?reject(e):resolve(db));});
  let credentials;
  try { credentials=await new Promise((resolve,reject)=>database.all('SELECT id, name, type FROM credentials_entity',(e,rows)=>e?reject(e):resolve(rows))); }
  finally { await new Promise((resolve,reject)=>database.close(e=>e?reject(e):resolve())); }
  const snapshot=JSON.parse(await readFile(snapshotPath,'utf8'));
  await mkdir(outputDirectory,{recursive:true});
  for(const name of ['review','prototype-image2']) {
    const template=JSON.parse(await readFile(new URL(`./workflows/${name}.json`,import.meta.url),'utf8'));
    const current=snapshot.find(w=>w.id===template.id);
    if(!current)throw new Error('当前工作流未找到，停止升级：'+template.id);
    const updated=upgradeWorkflow(current,template,credentials);
    await writeFile(path.join(outputDirectory,`${name}.json`),JSON.stringify(updated,null,2)+'\n');
    console.log(`${updated.name}: ${updated.nodes.filter(n=>n.type==='n8n-nodes-base.httpRequest' && n.credentials?.[n.parameters.nodeCredentialType]).length} 个请求已绑定凭据。`);
  }
  console.log('仅生成两份本机升级定义；密钥未读取，原始导出已保留。');
}

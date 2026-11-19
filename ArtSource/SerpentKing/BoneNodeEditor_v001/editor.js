const KEYS=[0,2,4,6,8,9],LABELS=['颈根','下颈','中颈','上颈','颈口','头部'];
const NAMES=['Stone','Venom','Fang','Time'],CN={Stone:'巨石',Venom:'毒液',Fang:'突刺',Time:'时间'};
const COLORS={Stone:'#ffa64b',Venom:'#57d7cf',Fang:'#ff7598',Time:'#ead872'};
const POSES=['左侧到位','左侧下沉','右侧到位','右侧下沉'],DURATIONS=[.12,.38,.12,.38],STORE='aloysius_serpent_bone_chain_v2';
function blankHead(){return {root:[0,0],rotations:Array(10).fill(0)}}
function blank(){return {version:2,frequency:.36,poses:POSES.map(name=>({name,heads:Object.fromEntries(NAMES.map(n=>[n,blankHead()]))}))}}
let project=blank(),head='Stone',poseIndex=0,nodeIndex=9,playing=false,scrubbing=false,cycle=0,last=0,drag=null,history=[],ready=false;
const $=id=>document.getElementById(id),clone=x=>JSON.parse(JSON.stringify(x));
function validate(x){
 if(![1,2].includes(x.version)||!Number.isFinite(x.frequency)||x.frequency<.05||x.frequency>2||!Array.isArray(x.poses)||x.poses.length!==4)throw Error('配置格式不正确');
 if(x.version===1){
  const converted=blank();converted.frequency=x.frequency;
  for(let pi=0;pi<4;pi++)for(const name of NAMES){
   const old=x.poses[pi].heads?.[name],p=rig.parts.find(p=>p.name===name),h=blankHead();
   if(!old||!Number.isFinite(old.angle))throw Error('旧配置缺少头部数据');
   for(const k of KEYS)if(!Array.isArray(old.nodes?.[k])||old.nodes[k].length!==2||!old.nodes[k].every(Number.isFinite))throw Error('旧节点数据不正确');
   h.root=old.nodes[0].slice();const targets=p.points.map((r,i)=>{let k=0;while(k<KEYS.length-2&&KEYS[k+1]<i)k++;const a=KEYS[k],b=KEYS[k+1],u=smooth((i-a)/(b-a));return r.map((v,axis)=>v+old.nodes[a][axis]+(old.nodes[b][axis]-old.nodes[a][axis])*u)});
   let parentAngle=0;
   for(let i=1;i<10;i++){const a=targets[i-1],b=targets[i],r=p.points[i-1],s=p.points[i],angle=Math.atan2(b[1]-a[1],b[0]-a[0])-Math.atan2(s[1]-r[1],s[0]-r[0]);h.rotations[i-1]=wrapDegrees((angle-parentAngle)*180/Math.PI);parentAngle=angle;}
   h.rotations[9]=wrapDegrees(old.angle-parentAngle*180/Math.PI);converted.poses[pi].heads[name]=h;
  }
  $('message').textContent='旧配置已转换为固定长度骨链，局部位置可能略有变化。';return converted;
 }
 for(const pose of x.poses)for(const name of NAMES){const h=pose.heads?.[name];if(!h||!Array.isArray(h.root)||h.root.length!==2||!h.root.every(v=>Number.isFinite(v)&&Math.abs(v)<=800)||!Array.isArray(h.rotations)||h.rotations.length!==10||!h.rotations.every(v=>Number.isFinite(v)&&Math.abs(v)<=720))throw Error('骨链数据不正确')}
 return x;
}
function wrapDegrees(v){return ((v+180)%360+360)%360-180}
try{const saved=localStorage.getItem(STORE)||localStorage.getItem('aloysius_serpent_bone_nodes_v1');if(saved)project=validate(JSON.parse(saved))}catch(e){$('message').textContent='未载入旧暂存：'+e.message}
function persist(){try{localStorage.setItem(STORE,JSON.stringify(project))}catch(e){$('message').textContent='浏览器未允许暂存，请使用“保存配置 JSON”。'}}
function checkpoint(){history.push(clone(project));if(history.length>60)history.shift()}
function current(){return project.poses[poseIndex].heads[head]}
function stop(){playing=false;scrubbing=false;$('play').textContent='播放循环';$('playStatus').textContent='编辑：'+POSES[poseIndex]}
function selectedValue(){const p=rig.parts.find(p=>p.name===head),b=chain(p,current())[nodeIndex];return [b.x-p.points[nodeIndex][0],b.y-p.points[nodeIndex][1]]}
function updateUI(){
 $('frequency').value=project.frequency;$('period').textContent=(1/project.frequency).toFixed(2)+' 秒/来回';
 $('nodeX').value=selectedValue()[0].toFixed(1);$('nodeY').value=selectedValue()[1].toFixed(1);
 $('angle').value=$('angleNumber').value=current().rotations[nodeIndex].toFixed(1);
 document.querySelectorAll('[data-head]').forEach(b=>b.classList.toggle('active',b.dataset.head===head));
 document.querySelectorAll('[data-pose]').forEach(b=>b.classList.toggle('active',+b.dataset.pose===poseIndex));
 document.querySelectorAll('[data-node]').forEach(b=>b.classList.toggle('active',+b.dataset.node===nodeIndex));
 if(!playing&&!scrubbing)$('playStatus').textContent='编辑：'+POSES[poseIndex];
}
function blendedHeads(){
 if(!playing&&!scrubbing)return project.poses[poseIndex].heads;
 let u=((cycle%1)+1)%1,i=0,start=0;
 while(i<3&&u>=start+DURATIONS[i]){start+=DURATIONS[i];i++}
 let k=(u-start)/DURATIONS[i];const hold=(i===1||i===3)?.48:0;
 k=smooth(Math.max(0,(k-hold)/(1-hold)));const next=(i+1)%4,result={};
 for(const name of NAMES){const a=project.poses[i].heads[name],b=project.poses[next].heads[name];result[name]={root:a.root.map((v,axis)=>v+(b.root[axis]-v)*k),rotations:a.rotations.map((v,j)=>v+wrapDegrees(b.rotations[j]-v)*k)}}
 $('playStatus').textContent='预览：'+POSES[i]+' → '+POSES[next];return result;
}
function chain(p,h){
 const output=[];
 for(let i=0;i<p.points.length;i++){
  const r=p.points[i];let x,y,a;
  if(i===0){x=r[0]+h.root[0];y=r[1]+h.root[1];a=h.rotations[0]*Math.PI/180;}
  else{const parent=output[i-1],previous=p.points[i-1],dx=r[0]-previous[0],dy=r[1]-previous[1],c=Math.cos(parent.angle),s=Math.sin(parent.angle);x=parent.x+c*dx-s*dy;y=parent.y+s*dx+c*dy;a=parent.angle+h.rotations[i]*Math.PI/180;}
  output.push({x,y,angle:a});
 }
 return output;
}
function solve(p,h,target,effector){
 if(effector===0){h.root=[clampOffset(target.x-p.points[0][0]),clampOffset(target.y-p.points[0][1])];return;}
 const preserve=effector===9&&$('keepFacing').checked,oldAngle=chain(p,h)[9].angle;
 for(let pass=0;pass<28;pass++){
  for(let j=effector-1;j>=0;j--){
   const points=chain(p,h),pivot=points[j],tip=points[effector],ax=tip.x-pivot.x,ay=tip.y-pivot.y,bx=target.x-pivot.x,by=target.y-pivot.y;
   if(Math.hypot(ax,ay)<.001||Math.hypot(bx,by)<.001)continue;
   let turn=Math.atan2(ax*by-ay*bx,ax*bx+ay*by)*180/Math.PI*.65;turn=Math.max(-7,Math.min(7,turn));
   const limit=j===0?18:j===1?28:80;h.rotations[j]=Math.max(-limit,Math.min(limit,h.rotations[j]+turn));
  }
  const tip=chain(p,h)[effector];if(Math.hypot(tip.x-target.x,tip.y-target.y)<.6)break;
 }
 if(preserve){const points=chain(p,h);h.rotations[9]=wrapDegrees((oldAngle-points[8].angle)*180/Math.PI);}
 const tip=chain(p,h)[effector];$('message').textContent=Math.hypot(tip.x-target.x,tip.y-target.y)>12?'已保持每节长度；当前目标超出受限骨链的可达范围。':'骨链联动：各节长度固定，后续骨节自动跟随。';
}
function pose(){
 const globals=[],matrices=[],locals=[],heads=blendedHeads();
 for(const p of rig.parts){
  const h=heads[p.name],n=p.points.length,points=chain(p,h);
  for(let i=0;i<n;i++){
   const r=p.points[i],{x,y,angle}=points[i];
   const id=p.jointIds[i],c=Math.cos(angle),s=Math.sin(angle);globals[id]={x,y,angle};matrices[id]=[c,s,-s,c,x-c*r[0]+s*r[1],y-s*r[0]-c*r[1]];
   const par=i?globals[p.jointIds[i-1]]:{x:0,y:0,angle:0};const pc=Math.cos(par.angle),ps=Math.sin(par.angle);locals[id]={x:pc*(x-par.x)+ps*(y-par.y),y:-ps*(x-par.x)+pc*(y-par.y),angle:angle-par.angle};
  }
 }
 for(const id of rig.bodyJointIds){const r=rig.joints[id].rest;globals[id]={x:r[0],y:r[1],angle:0};matrices[id]=[1,0,0,1,0,0];}
 return {globals,matrices,locals};
}
window.poseAt=pose;
function handles(state){
 const p=rig.parts.find(p=>p.name===head);ctx.save();ctx.strokeStyle=COLORS[head];ctx.fillStyle=COLORS[head];ctx.lineWidth=3;ctx.font='19px Microsoft YaHei';
 ctx.beginPath();p.jointIds.forEach((id,i)=>{const b=state.globals[id];i?ctx.lineTo(b.x,b.y):ctx.moveTo(b.x,b.y)});ctx.stroke();
 p.jointIds.forEach(id=>{const b=state.globals[id];ctx.beginPath();ctx.arc(b.x,b.y,4,0,Math.PI*2);ctx.fill()});
 KEYS.forEach((k,i)=>{const b=state.globals[p.jointIds[k]],locked=k===0&&$('lockRoot').checked;ctx.beginPath();ctx.arc(b.x,b.y,nodeIndex===k?12:9,0,Math.PI*2);ctx.fillStyle=locked?'#8f8a91':COLORS[head];ctx.fill();ctx.strokeStyle='#16101c';ctx.lineWidth=3;ctx.stroke();ctx.lineWidth=3;ctx.fillStyle='#fff3dc';ctx.strokeText(LABELS[i],b.x+17,b.y-12);ctx.fillText(LABELS[i],b.x+17,b.y-12)});ctx.restore();
}
function draw(){
 if(!ready)return;settings.scene=true;rig.parts.forEach(p=>p.visible=!$('isolate').checked||p.name===head);
 const state=necks(30);ctx.fillStyle='#24121e';ctx.fillRect(0,0,1536,1024);ctx.drawImage(images['EmptyBed.png'],0,0);ctx.drawImage(surface,0,0);ctx.drawImage(images['BedForeground.png'],0,0);
 if($('showBones').checked&&!playing&&!scrubbing)handles(state);
}
window.drawAt=draw;
NAMES.forEach(name=>{const b=document.createElement('button');b.dataset.head=name;b.textContent=CN[name];b.onclick=()=>{head=name;stop();updateUI();draw()};$('heads').appendChild(b)});
POSES.forEach((name,i)=>{const b=document.createElement('button');b.dataset.pose=i;b.textContent=name;b.onclick=()=>{poseIndex=i;stop();updateUI();draw()};$('poses').appendChild(b)});
KEYS.forEach((key,i)=>{const b=document.createElement('button');b.dataset.node=key;b.textContent=LABELS[i];b.onclick=()=>{nodeIndex=key;stop();updateUI();draw()};$('nodeButtons').appendChild(b)});
function xy(e){const r=canvas.getBoundingClientRect();return {x:(e.clientX-r.left)*1536/r.width,y:(e.clientY-r.top)*1024/r.height}}
canvas.onpointerdown=e=>{
 if(playing||scrubbing){stop();draw();return}if(!$('showBones').checked)return;
 const at=xy(e),state=pose(),p=rig.parts.find(p=>p.name===head);let best=null,distance=32;
 for(const k of KEYS){if(k===0&&$('lockRoot').checked)continue;const b=state.globals[p.jointIds[k]],d=Math.hypot(at.x-b.x,at.y-b.y);if(d<distance){best=k;distance=d}}
 if(best===null)return;checkpoint();nodeIndex=best;const b=state.globals[p.jointIds[best]];drag={at,start:clone(current()),point:{x:b.x,y:b.y}};canvas.setPointerCapture(e.pointerId);updateUI();draw();
};
canvas.onpointermove=e=>{if(!drag)return;const at=xy(e),p=rig.parts.find(p=>p.name===head);project.poses[poseIndex].heads[head]=clone(drag.start);solve(p,current(),{x:drag.point.x+at.x-drag.at.x,y:drag.point.y+at.y-drag.at.y},nodeIndex);updateUI();draw()};
canvas.onpointerup=canvas.onpointercancel=e=>{if(!drag)return;drag=null;if(canvas.hasPointerCapture(e.pointerId))canvas.releasePointerCapture(e.pointerId);persist()};
function clampOffset(v){return Math.max(-800,Math.min(800,v))}
function changeNode(axis,value){if(nodeIndex===0&&$('lockRoot').checked){$('message').textContent='颈根已固定；取消“固定颈根”后可调整。';updateUI();return}if(!Number.isFinite(value))return;checkpoint();stop();const p=rig.parts.find(p=>p.name===head),b=chain(p,current())[nodeIndex],target={x:b.x,y:b.y};target[axis?'y':'x']=p.points[nodeIndex][axis]+clampOffset(value);solve(p,current(),target,nodeIndex);persist();updateUI();draw()}
$('nodeX').onchange=e=>changeNode(0,+e.target.value);$('nodeY').onchange=e=>changeNode(1,+e.target.value);
for(const id of ['angle','angleNumber']){$(id).onfocus=()=>checkpoint();$(id).oninput=e=>{const v=+e.target.value;if(!Number.isFinite(v))return;stop();current().rotations[nodeIndex]=Math.max(-180,Math.min(180,v));updateUI();persist();draw()}}
$('frequency').onchange=e=>{const v=+e.target.value;if(!Number.isFinite(v)||v<=0){updateUI();return}checkpoint();project.frequency=Math.max(.05,Math.min(2,v));persist();updateUI()};
$('play').onclick=()=>{playing=!playing;scrubbing=false;if(playing){cycle=0;$('play').textContent='停止并继续编辑'}else{stop();draw()}};
$('scrub').oninput=e=>{playing=false;scrubbing=true;cycle=+e.target.value;$('play').textContent='播放循环';draw()};
$('undo').onclick=()=>{if(!history.length)return;project=history.pop();stop();persist();updateUI();draw()};
$('copy').onclick=()=>{checkpoint();project.poses[(poseIndex+1)%4].heads[head]=clone(current());persist();$('message').textContent=CN[head]+'已复制到「'+POSES[(poseIndex+1)%4]+'」。'};
$('resetNode').onclick=()=>{checkpoint();stop();current().rotations[nodeIndex]=0;if(nodeIndex===0)current().root=[0,0];persist();updateUI();draw()};
$('resetHead').onclick=()=>{checkpoint();stop();project.poses[poseIndex].heads[head]=blankHead();persist();updateUI();draw()};
for(const id of ['isolate','lockRoot','showBones','keepFacing'])$(id).onchange=()=>draw();
function download(name,data,type){const a=document.createElement('a'),url=URL.createObjectURL(new Blob([data],{type}));a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)}
$('save').onclick=()=>{persist();download('四头蛇_关键节点.json',JSON.stringify(project,null,2),'application/json')};
$('load').onclick=()=>$('file').click();$('file').onchange=async e=>{const f=e.target.files[0];if(!f)return;try{const data=validate(JSON.parse(await f.text()));checkpoint();project=data;stop();persist();updateUI();draw();$('message').textContent='已载入配置。'}catch(err){$('message').textContent='载入失败：'+err.message}e.target.value=''};
$('png').onclick=()=>{const b=$('showBones').checked;$('showBones').checked=false;draw();const a=document.createElement('a');a.download=CN[head]+'_'+POSES[poseIndex]+'.png';a.href=canvas.toDataURL('image/png');a.click();$('showBones').checked=b;draw()};
window.addEventListener('keydown',e=>{if(/INPUT|SELECT|TEXTAREA/.test(e.target.tagName))return;if(e.code==='Space'){e.preventDefault();$('play').click()}if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='z'){e.preventDefault();$('undo').click()}});
function tick(now){if(last&&playing){cycle+=(now-last)/1000*project.frequency;$('scrub').value=cycle%1;draw()}last=now;requestAnimationFrame(tick)}
Promise.all(Object.entries(window.ASSETS).map(([name,url])=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images[name]=im;textures[name]=upload(im);resolve()};im.onerror=reject;im.src=url}))).then(()=>{ready=true;window.ready=true;updateUI();draw();requestAnimationFrame(tick)}).catch(e=>{$('message').textContent='素材载入失败：'+String(e)});

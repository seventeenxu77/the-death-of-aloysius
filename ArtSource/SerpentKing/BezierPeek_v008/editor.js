const NAMES=['Stone','Venom','Fang','Time'],CN={Stone:'巨石',Venom:'毒液',Fang:'突刺',Time:'时间'},COLORS={Stone:'#ffad5c',Venom:'#60dace',Fang:'#ff7da2',Time:'#e8d579'};
const LABELS=['颈根','下段弧度','弯曲中心','上段弧度','头部'],POSES=['左低','中高','右低'],STORE='aloysius_serpent_bezier_peek_v8';
const $=id=>document.getElementById(id),clone=x=>JSON.parse(JSON.stringify(x));
let head='Stone',poseIndex=1,nodeIndex=4,playing=false,scrubbing=false,cycle=0,last=0,drag=null,history=[],ready=false;
function preset(p,index){
 const r=p.points[0],e=p.points.at(-1),end=[e[0],Math.max(r[1]-470,e[1]-p.artTop+25)],height=r[1]-end[1],side=index-1;
 const lower=[r[0]+Math.cos(p.referenceStartAngle)*100,r[1]+Math.sin(p.referenceStartAngle)*100];
 let nodes;
 if(side===0)nodes=[[...r],lower,[(r[0]+end[0])*.5,r[1]-height*.51],[0,0],end];
 else nodes=[[...r],lower,[r[0]+side*180,r[1]-height*.40],[0,0],[end[0]+side*135,end[1]+115]];
 const length=Math.max(85,height*.24);nodes[3]=[nodes[4][0]-Math.cos(p.referenceEndAngle)*length,nodes[4][1]-Math.sin(p.referenceEndAngle)*length];
 if(p.name==='Venom'){
  const before=Math.atan2(nodes[4][1]-nodes[3][1],nodes[4][0]-nodes[3][0]);
  const scale=(nodes[0][1]-nodes[4][1]+95)/Math.max(1,nodes[0][1]-nodes[4][1]);
  for(let i=1;i<5;i++)nodes[i][1]=nodes[0][1]+(nodes[i][1]-nodes[0][1])*scale;
  const after=Math.atan2(nodes[4][1]-nodes[3][1],nodes[4][0]-nodes[3][0]);
  return {nodes,angle:(before-after)*180/Math.PI};
 }
 return {nodes,angle:0};
}
function blank(){return {version:8,frequency:.36,poses:POSES.map((name,i)=>({name,heads:Object.fromEntries(rig.parts.map(p=>[p.name,preset(p,i)]))}))}}
let project=blank();
function validate(x){
 if(![3,7,8].includes(x.version)||!Number.isFinite(x.frequency)||x.frequency<.05||x.frequency>2||!Array.isArray(x.poses)||x.poses.length!==3)throw Error('请选择本版贝塞尔编辑器保存的配置');
 for(const p of x.poses)for(const name of NAMES){const h=p.heads?.[name];if(!h||!Array.isArray(h.nodes)||h.nodes.length!==5||!Number.isFinite(h.angle)||Math.abs(h.angle)>45)throw Error('姿态数据不完整');for(const n of h.nodes)if(!Array.isArray(n)||n.length!==2||!n.every(v=>Number.isFinite(v)&&Math.abs(v)<5000))throw Error('控制点不正确')}
 return x;
}
try{const saved=localStorage.getItem(STORE);if(saved)project=validate(JSON.parse(saved))}catch(e){$('message').textContent='暂存读取失败：'+e.message}
function persist(){try{localStorage.setItem(STORE,JSON.stringify(project))}catch(e){$('message').textContent='浏览器未允许暂存，请保存 JSON。'}}
function checkpoint(){history.push(clone(project));if(history.length>60)history.shift()}
function current(){return project.poses[poseIndex].heads[head]}
function stop(){playing=false;scrubbing=false;$('play').textContent='播放循环';$('status').textContent='编辑：'+POSES[poseIndex]}
function ui(){
 $('x').value=current().nodes[nodeIndex][0].toFixed(1);$('y').value=current().nodes[nodeIndex][1].toFixed(1);$('angle').value=current().angle;
 $('frequency').value=project.frequency;$('period').textContent=(1/project.frequency).toFixed(2)+' 秒/来回';
 for(const [key,value] of [['head',head],['pose',String(poseIndex)],['node',String(nodeIndex)]])document.querySelectorAll('[data-'+key+']').forEach(b=>b.classList.toggle('active',b.dataset[key]===value));
 if(!playing&&!scrubbing)$('status').textContent='编辑：'+POSES[poseIndex];
}
function controls(h){
 const [a,b,m,d,e]=h.nodes;let tx=d[0]-b[0],ty=d[1]-b[1],len=Math.hypot(tx,ty)||1;tx/=len;ty/=len;
 const arm=Math.min(Math.hypot(m[0]-a[0],m[1]-a[1]),Math.hypot(e[0]-m[0],e[1]-m[1]))*.32;
 return [[a,b,[m[0]-tx*arm,m[1]-ty*arm],m],[m,[m[0]+tx*arm,m[1]+ty*arm],d,e]];
}
function cubic(c,t){const s=1-t;return [s*s*s*c[0][0]+3*s*s*t*c[1][0]+3*s*t*t*c[2][0]+t*t*t*c[3][0],s*s*s*c[0][1]+3*s*s*t*c[1][1]+3*s*t*t*c[2][1]+t*t*t*c[3][1]]}
function arc(h){
 const cs=controls(h),raw=[];for(let k=0;k<2;k++)for(let i=0;i<129;i++){if(k&&i===0)continue;raw.push(cubic(cs[k],i/128))}
 const lengths=[0];for(let i=1;i<raw.length;i++)lengths.push(lengths[i-1]+Math.hypot(raw[i][0]-raw[i-1][0],raw[i][1]-raw[i-1][1]));
 const total=lengths.at(-1)||1,points=[];let k=0;
 for(let i=0;i<193;i++){const distance=total*i/192;while(k<raw.length-2&&lengths[k+1]<distance)k++;const t=(distance-lengths[k])/Math.max(.0001,lengths[k+1]-lengths[k]);points.push([raw[k][0]+(raw[k+1][0]-raw[k][0])*t,raw[k][1]+(raw[k+1][1]-raw[k][1])*t,0])}
 for(let i=0;i<points.length;i++){const a=points[Math.max(0,i-1)],b=points[Math.min(points.length-1,i+1)];points[i][2]=Math.atan2(b[1]-a[1],b[0]-a[0])}
 return {points,length:total,h};
}
function sample(a,u){const z=Math.max(0,Math.min(1,u))*192,k=Math.min(191,Math.floor(z)),t=z-k,p=a.points[k],q=a.points[k+1],diff=Math.atan2(Math.sin(q[2]-p[2]),Math.cos(q[2]-p[2]));return [p[0]+(q[0]-p[0])*t,p[1]+(q[1]-p[1])*t,p[2]+diff*t]}
function activeHeads(){
 if(!playing&&!scrubbing)return project.poses[poseIndex].heads;
 const z=((cycle%1)+1)%1*4,segment=Math.min(3,Math.floor(z)),sequence=[0,1,2,1],i=sequence[segment],j=sequence[(segment+1)%4],hold=i===1?0:.42;
 const t=smooth(Math.max(0,(z-segment-hold)/(1-hold))),result={};
 for(const name of NAMES){const a=project.poses[i].heads[name],b=project.poses[j].heads[name];result[name]={angle:a.angle+(b.angle-a.angle)*t,nodes:a.nodes.map((n,k)=>n.map((v,axis)=>v+(b.nodes[k][axis]-v)*t))}}
 $('status').textContent='预览：'+POSES[i]+' → '+POSES[j];return result;
}
function pose(){const heads=activeHeads(),curves={};for(const p of rig.parts)curves[p.name]=arc(heads[p.name]);return {curves}}
window.poseAt=pose;
function deformCurve(m,state){
 if(m.name==='Coils'){for(let i=0;i<m.vertices.length;i++){m.positions[i*2]=m.vertices[i][0]/768-1;m.positions[i*2+1]=1-(m.vertices[i][1]+128)/576}return;}
 const p=rig.parts.find(p=>p.name===m.name),a=state.curves[m.name],end=a.points.at(-1),origin=p.points.at(-1),angle=end[2]-p.referenceEndAngle+a.h.angle*Math.PI/180,c=Math.cos(angle),s=Math.sin(angle);
 for(let i=0;i<m.vertices.length;i++){
  const j=i*5,u=m.binding[j],dx=m.binding[j+1],dy=m.binding[j+2],refAngle=m.binding[j+3],headWeight=m.binding[j+4],v=m.vertices[i];let x,y;
  const hx=end[0]+c*(v[0]-origin[0])-s*(v[1]-origin[1]),hy=end[1]+s*(v[0]-origin[0])+c*(v[1]-origin[1]);
  if(headWeight>.999){x=hx;y=hy}else{
   const q=sample(a,u);
   // Bind normalized silhouette cross sections, not the source's wide coiled
   // foot or an unrelated branch of its S bend. Preserve a smooth tube radius.
   const radius=p.neckRadius,nx=q[0]-Math.sin(q[2])*dx*radius,ny=q[1]+Math.cos(q[2])*dx*radius+dy;
   x=nx+(hx-nx)*headWeight;y=ny+(hy-ny)*headWeight;
  }
  m.positions[i*2]=x/768-1;m.positions[i*2+1]=1-(y+128)/576;
 }
}
function overlay(state){
 const p=rig.parts.find(p=>p.name===head),a=state.curves[head],h=a.h;ctx.save();ctx.translate(0,128);ctx.strokeStyle=COLORS[head];ctx.lineWidth=3;ctx.beginPath();a.points.forEach((q,i)=>i?ctx.lineTo(q[0],q[1]):ctx.moveTo(q[0],q[1]));ctx.stroke();
 for(let i=0;i<=24;i++){const q=sample(a,i/24),nx=-Math.sin(q[2]),ny=Math.cos(q[2]);ctx.strokeStyle='#8ce0d1';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(q[0]-nx*8,q[1]-ny*8);ctx.lineTo(q[0]+nx*8,q[1]+ny*8);ctx.stroke();}
 ctx.setLineDash([6,6]);ctx.strokeStyle='#a88e6a';ctx.beginPath();ctx.moveTo(...h.nodes[0]);ctx.lineTo(...h.nodes[1]);ctx.moveTo(...h.nodes[3]);ctx.lineTo(...h.nodes[4]);ctx.stroke();ctx.setLineDash([]);ctx.font='18px Microsoft YaHei';
 h.nodes.forEach((n,i)=>{ctx.beginPath();ctx.arc(n[0],n[1],nodeIndex===i?12:9,0,Math.PI*2);ctx.fillStyle=(i===1||i===3)?'#231a24':COLORS[head];ctx.fill();ctx.strokeStyle=COLORS[head];ctx.lineWidth=3;ctx.stroke();ctx.strokeStyle='#120e16';ctx.fillStyle='#fff1d7';ctx.strokeText(LABELS[i],n[0]+17,n[1]-12);ctx.fillText(LABELS[i],n[0]+17,n[1]-12)});ctx.restore();
}
function draw(){
 if(!ready)return;settings.scene=true;rig.parts.forEach(p=>p.visible=!$('isolate').checked||p.name===head);
 const state=necks(30);ctx.fillStyle='#110811';ctx.fillRect(0,0,1536,1152);if($('bed').checked)ctx.drawImage(images['EmptyBed.png'],0,128);ctx.drawImage(surface,0,0);if($('bed').checked)ctx.drawImage(images['BedForeground.png'],0,128);
 if($('showCurve').checked&&!playing&&!scrubbing)overlay(state);
 const p=rig.parts.find(p=>p.name===head);$('length').textContent='当前弧长 '+state.curves[head].length.toFixed(0)+'；原始 '+p.referenceLength.toFixed(0)+'。横截面宽度保持，头部不拉伸。';
}
window.drawAt=draw;window.selectPose=i=>{poseIndex=i;stop();ui();draw()};
function buttons(id,values,key,fn){values.forEach((name,i)=>{const b=document.createElement('button');b.textContent=key==='head'?CN[name]:name;b.dataset[key]=key==='head'?name:i;b.onclick=()=>fn(key==='head'?name:i);$(id).appendChild(b)})}
buttons('heads',NAMES,'head',n=>{head=n;stop();ui();draw()});buttons('poses',POSES,'pose',window.selectPose);buttons('nodes',LABELS,'node',i=>{nodeIndex=i;stop();ui();draw()});
function position(e){const r=canvas.getBoundingClientRect();return {x:(e.clientX-r.left)*1536/r.width,y:(e.clientY-r.top)*1152/r.height-128}}
function move(index,dx,dy,start){
 const h=clone(start),add=(k,x,y)=>{h.nodes[k][0]+=x;h.nodes[k][1]+=y};
 if(index===0){if($('lockRoot').checked)return;for(let k=0;k<5;k++)add(k,dx,dy)}
 else if(index===4){add(4,dx,dy);add(3,dx,dy);add(2,dx*.18,dy*.18);add(1,dx*.05,dy*.05)}
 else if(index===2){add(2,dx,dy);add(1,dx*.2,dy*.2);if($('linked').checked){add(3,dx*.75,dy*.75);add(4,dx*.75,dy*.75)}}
 else add(index,dx,dy);
 project.poses[poseIndex].heads[head]=h;
}
canvas.onpointerdown=e=>{
 if(playing||scrubbing){stop();draw();return}if(!$('showCurve').checked)return;const at=position(e);let best=-1,distance=32;
 current().nodes.forEach((n,i)=>{if(i===0&&$('lockRoot').checked)return;const d=Math.hypot(at.x-n[0],at.y-n[1]);if(d<distance){distance=d;best=i}});
 if(best<0)return;checkpoint();nodeIndex=best;drag={at,start:clone(current())};canvas.setPointerCapture(e.pointerId);ui();draw();
};
canvas.onpointermove=e=>{if(!drag)return;const at=position(e);move(nodeIndex,at.x-drag.at.x,at.y-drag.at.y,drag.start);ui();draw()};
canvas.onpointerup=canvas.onpointercancel=e=>{if(!drag)return;drag=null;if(canvas.hasPointerCapture(e.pointerId))canvas.releasePointerCapture(e.pointerId);persist()};
for(const [id,axis] of [['x',0],['y',1]])$(id).onchange=e=>{const v=+e.target.value;if(!Number.isFinite(v))return;if(nodeIndex===0&&$('lockRoot').checked){ui();return}checkpoint();stop();move(nodeIndex,axis===0?v-current().nodes[nodeIndex][0]:0,axis===1?v-current().nodes[nodeIndex][1]:0,current());persist();ui();draw()};
$('angle').onchange=e=>{const v=+e.target.value;if(!Number.isFinite(v))return;checkpoint();stop();current().angle=Math.max(-45,Math.min(45,v));persist();ui();draw()};
$('frequency').onchange=e=>{const v=+e.target.value;if(!Number.isFinite(v)||v<=0){ui();return}checkpoint();project.frequency=Math.max(.05,Math.min(2,v));persist();ui()};
$('play').onclick=()=>{playing=!playing;scrubbing=false;if(playing){cycle=0;$('play').textContent='停止并编辑'}else{stop();draw()}};
$('scrub').oninput=e=>{playing=false;scrubbing=true;cycle=+e.target.value;$('play').textContent='播放循环';draw()};
$('undo').onclick=()=>{if(!history.length)return;project=history.pop();stop();persist();ui();draw()};
$('copy').onclick=()=>{checkpoint();project.poses[(poseIndex+1)%3].heads[head]=clone(current());persist();$('message').textContent='已复制到 '+POSES[(poseIndex+1)%3]};
$('reset').onclick=()=>{checkpoint();stop();project.poses[poseIndex].heads[head]=preset(rig.parts.find(p=>p.name===head),poseIndex);persist();ui();draw()};
for(const id of ['isolate','bed','lockRoot','linked','showCurve'])$(id).onchange=draw;
function download(name,data,type){const a=document.createElement('a'),url=URL.createObjectURL(new Blob([data],{type}));a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)}
$('save').onclick=()=>{persist();download('四头蛇_贝塞尔姿态.json',JSON.stringify(project,null,2),'application/json')};
$('load').onclick=()=>$('file').click();$('file').onchange=async e=>{const f=e.target.files[0];if(!f)return;try{const data=validate(JSON.parse(await f.text()));checkpoint();project=data;stop();persist();ui();draw();$('message').textContent='已载入配置。'}catch(err){$('message').textContent=err.message}e.target.value=''};
$('png').onclick=()=>{const visible=$('showCurve').checked;$('showCurve').checked=false;draw();const a=document.createElement('a');a.download=CN[head]+'_'+POSES[poseIndex]+'.png';a.href=canvas.toDataURL('image/png');a.click();$('showCurve').checked=visible;draw()};
window.addEventListener('keydown',e=>{if(/INPUT|SELECT|TEXTAREA/.test(e.target.tagName))return;if(e.code==='Space'){e.preventDefault();$('play').click()}if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='z'){e.preventDefault();$('undo').click()}});
function tick(now){if(last&&playing){cycle+=(now-last)/1000*project.frequency;$('scrub').value=cycle%1;draw()}last=now;requestAnimationFrame(tick)}
Promise.all(Object.entries(window.ASSETS).map(([name,url])=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images[name]=im;textures[name]=upload(im);resolve()};im.onerror=reject;im.src=url}))).then(()=>{ready=true;window.ready=true;ui();draw();if(!location.search.includes('render=1'))requestAnimationFrame(tick)}).catch(e=>{$('message').textContent='素材加载失败：'+String(e)});

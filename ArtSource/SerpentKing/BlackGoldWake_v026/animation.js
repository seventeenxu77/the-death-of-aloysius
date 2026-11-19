// Hierarchical bone transforms + inverse bind matrices. No full-image motion.
const rig=window.RIG,canvas=document.getElementById('stage'),ctx=canvas.getContext('2d');
const surface=document.createElement('canvas');surface.width=1536;surface.height=1024;
const gl=surface.getContext('webgl',{alpha:true,antialias:true,preserveDrawingBuffer:true});
if(!gl)throw Error('浏览器未启用 WebGL');
const settings={bones:false,idle:true,scene:true,speed:1,paused:false};window.rigSettings=settings;
const images={},textures={};const clamp=x=>Math.max(0,Math.min(1,x));const smooth=x=>{x=clamp(x);return x*x*x*(x*(x*6-15)+10)};
function shader(type,source){let s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s}
const program=gl.createProgram();gl.attachShader(program,shader(gl.VERTEX_SHADER,'attribute vec2 aPosition;attribute vec2 aUV;varying vec2 vUV;void main(){gl_Position=vec4(aPosition,0.,1.);vUV=aUV;}'));
gl.attachShader(program,shader(gl.FRAGMENT_SHADER,`precision mediump float;varying vec2 vUV;uniform sampler2D uOpen;uniform sampler2D uClosed;uniform float closedEye;uniform vec4 eye0;uniform vec4 eye1;uniform float roots;uniform float front;
float eye(vec4 e,vec2 p){if(e.z<1.)return 0.;vec2 d=(p-e.xy)/e.zw;return 1.-smoothstep(.80,1.,length(d));}
void main(){vec2 p=vUV*vec2(1536.,1024.);vec4 a=texture2D(uOpen,vUV),b=texture2D(uClosed,vUV);float mask=max(eye(eye0,p),eye(eye1,p))*closedEye;
vec4 color=vec4(mix(a.rgb,b.rgb,mask),a.a);color.a*=1.-roots*smoothstep(845.,865.,p.y);
float edge=785.-15.*smoothstep(850.,1150.,p.x)+65.*(1.-smoothstep(240.,400.,p.x));if(front>.5&&p.y<edge)discard;gl_FragColor=color;}`));
gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program));gl.useProgram(program);
const posLoc=gl.getAttribLocation(program,'aPosition'),uvLoc=gl.getAttribLocation(program,'aUV');gl.enableVertexAttribArray(posLoc);gl.enableVertexAttribArray(uvLoc);
gl.uniform1i(gl.getUniformLocation(program,'uOpen'),0);gl.uniform1i(gl.getUniformLocation(program,'uClosed'),1);
gl.enable(gl.BLEND);gl.blendFuncSeparate(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA,gl.ONE,gl.ONE_MINUS_SRC_ALPHA);
function buf(data,target){let b=gl.createBuffer();gl.bindBuffer(target,b);gl.bufferData(target,data,gl.STATIC_DRAW);return b}
for(const m of rig.meshes){m.positions=new Float32Array(m.vertices.length*2);m.pb=buf(m.positions,gl.ARRAY_BUFFER);m.ub=buf(new Float32Array(m.uv.flat()),gl.ARRAY_BUFFER);m.ib=buf(new Uint16Array(m.indices),gl.ELEMENT_ARRAY_BUFFER)}
function upload(image){const tex=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,tex);for(const p of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T])gl.texParameteri(gl.TEXTURE_2D,p,gl.CLAMP_TO_EDGE);for(const p of [gl.TEXTURE_MIN_FILTER,gl.TEXTURE_MAG_FILTER])gl.texParameteri(gl.TEXTURE_2D,p,gl.LINEAR);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,image);return tex}
function pose(t){
 const globals=[],matrices=[],locals=[];
 for(let pi=0;pi<rig.parts.length;pi++){
  const p=rig.parts[pi],age=t-p.start,progress=smooth(age/p.duration),rest=p.points,n=rest.length;
  // The lower roots stay inside the common coils. Only upper joints rise.
  const targets=rest.map(([x,y],i)=>{
   // Unfold from the coils. Compression lives below the foreground coil,
   // keeping the visible S-neck's width and gold motifs intact.
   const lift=[0,.12,.5,.85,1,1,1,1,1,1][i];
   const anticip=age>0&&age<.4?Math.sin(age/.4*Math.PI)*6:0;
   const settle=smooth((age-p.duration)/.7),idle=(settings.idle?1:0)*p.idle*settle;
   const phase=t*.94+pi*1.83,dx=(Math.sin(phase)*6+Math.sin(phase*1.71+.7)*2)*idle;
   const dy=Math.sin(phase*.77+pi)*3*idle;
   return [x+(p.drop[0]*(1-progress)+dx)*lift+Math.sin(i*.75)*12*Math.sin(progress*Math.PI)*lift,y+(p.drop[1]*(1-progress)+anticip+dy)*lift];
  });
  // The skull is one rigid bone: no face squash, no pseudo-turn by stretching.
  const headDelta=[targets[n-1][0]-rest[n-1][0],targets[n-1][1]-rest[n-1][1]];
  for(let i=0;i<n;i++)if(rest[i][1]<=p.rigidY)targets[i]=[rest[i][0]+headDelta[0],rest[i][1]+headDelta[1]];
  for(let i=0;i<n;i++){
   const id=p.jointIds[i],r=rest[i],q=targets[i];let angle=0;
   if(i>0&&i<n-2&&r[1]>p.rigidY){
    const a=rest[Math.max(0,i-1)],b=rest[Math.min(n-1,i+1)],c=targets[Math.max(0,i-1)],d=targets[Math.min(n-1,i+1)];
    // Controlled translation/stretch avoids inverted quads on the wide S bends.
    // These remain parent-local bone keys, not an image-space displacement field.
    angle=0;
   }
   const parent=i?globals[p.jointIds[i-1]]:{x:0,y:0,angle:0};const ca=Math.cos(parent.angle),sa=Math.sin(parent.angle),tx=q[0]-parent.x,ty=q[1]-parent.y;
   const local={x:ca*tx+sa*ty,y:-sa*tx+ca*ty,angle:angle-parent.angle};locals[id]=local;
   const g={x:parent.x+ca*local.x-sa*local.y,y:parent.y+sa*local.x+ca*local.y,angle:parent.angle+local.angle};globals[id]=g;
   const co=Math.cos(g.angle),si=Math.sin(g.angle);matrices[id]=[co,si,-si,co,g.x-co*r[0]+si*r[1],g.y-si*r[0]-co*r[1]];
  }
 }
 for(let k=0;k<rig.bodyJointIds.length;k++){
  const id=rig.bodyJointIds[k],j=rig.joints[id],x=j.rest[0],w=Math.sin(Math.PI*x/1536);
  const dy=settings.idle?Math.sin(t*.7+x*.004)*1.3*w:0;
  const g={x,y:830+dy,angle:0},parent=k?globals[rig.bodyJointIds[k-1]]:{x:0,y:0,angle:0};
  globals[id]=g;locals[id]={x:g.x-parent.x,y:g.y-parent.y,angle:0};matrices[id]=[1,0,0,1,0,dy];
 }
 return {globals,matrices,locals};
}
window.poseAt=pose;
function drawSkin(m,state,t,front=false){
  const p=rig.parts.find(p=>p.name===m.name),isBody=m.name==='Coils';
  if(!isBody&&!p.visible)return;
  gl.activeTexture(gl.TEXTURE0);gl.bindTexture(gl.TEXTURE_2D,textures[isBody?'Coils.png':p.name+'_Open.png']);gl.activeTexture(gl.TEXTURE1);gl.bindTexture(gl.TEXTURE_2D,textures[isBody?'Coils.png':p.name+'_Closed.png']);
  const closed=isBody?0:1-smooth((t-p.start-p.duration-p.eyeDelay)/p.eyeDuration);
  gl.uniform1f(gl.getUniformLocation(program,'front'),front?1:0);
  gl.uniform1f(gl.getUniformLocation(program,'closedEye'),closed);gl.uniform4fv(gl.getUniformLocation(program,'eye0'),isBody?[0,0,0,0]:p.eyes[0]);gl.uniform4fv(gl.getUniformLocation(program,'eye1'),isBody?[0,0,0,0]:p.eyes[1]||[0,0,0,0]);gl.uniform1f(gl.getUniformLocation(program,'roots'),settings.scene&&!isBody?1:0);
  for(let i=0;i<m.vertices.length;i++){let x=m.vertices[i][0],y=m.vertices[i][1],dx=0,dy=0;for(let k=0;k<2;k++){const a=state.matrices[m.joints[i][k]],w=m.weights[i][k];dx+=w*(a[0]*x+a[2]*y+a[4]);dy+=w*(a[1]*x+a[3]*y+a[5])}m.positions[2*i]=dx/768-1;m.positions[2*i+1]=1-dy/512}
  gl.bindBuffer(gl.ARRAY_BUFFER,m.pb);gl.bufferData(gl.ARRAY_BUFFER,m.positions,gl.DYNAMIC_DRAW);gl.vertexAttribPointer(posLoc,2,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ARRAY_BUFFER,m.ub);gl.vertexAttribPointer(uvLoc,2,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,m.ib);gl.drawElements(gl.TRIANGLES,m.indices.length,gl.UNSIGNED_SHORT,0);
}
function necks(t){
 const state=pose(t);gl.viewport(0,0,1536,1024);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT);
 const body=rig.meshes.find(m=>m.name==='Coils');if(settings.scene)drawSkin(body,state,t);
 for(const m of rig.meshes)if(m.name!=='Coils')drawSkin(m,state,t);
 if(settings.scene)drawSkin(body,state,t,true);return state;
}
function bones(state){ctx.save();ctx.lineWidth=3;ctx.font='18px Microsoft YaHei';rig.parts.forEach((p,k)=>{if(!p.visible)return;ctx.strokeStyle=ctx.fillStyle=['#72c9cf','#f1b469','#ee839b','#dedb8c'][k];const points=p.jointIds.map(i=>state.globals[i]);ctx.beginPath();points.forEach((q,i)=>i?ctx.lineTo(q.x,q.y):ctx.moveTo(q.x,q.y));ctx.stroke();for(const q of points){ctx.beginPath();ctx.arc(q.x,q.y,5,0,Math.PI*2);ctx.fill()}ctx.fillText(p.label,points.at(-1).x+35,points.at(-1).y)});ctx.restore()}
window.drawAt=function(t){const state=necks(t);ctx.clearRect(0,0,1536,1024);ctx.fillStyle='#24121e';ctx.fillRect(0,0,1536,1024);
 if(settings.scene){ctx.drawImage(images['EmptyBed.png'],0,0)}else{ctx.fillStyle='#342837';for(let y=0;y<1024;y+=40)for(let x=0;x<1536;x+=40)if((x/40+y/40)%2===0)ctx.fillRect(x,y,40,40)}
 ctx.drawImage(surface,0,0);if(settings.scene){ctx.drawImage(images['BedForeground.png'],0,0)}if(settings.bones)bones(state);
 document.getElementById('clock').textContent=t.toFixed(2)+' s';document.getElementById('seek').value=t;
 document.getElementById('status').textContent=rig.parts.slice().sort((a,b)=>a.start-b.start).map(p=>p.label+': '+(t<p.start?'等待':t<p.start+p.duration?'抬起':t<p.start+p.duration+p.eyeDelay+p.eyeDuration?'睁眼':'保持睁眼')).join('　');
};
let playTime=0,last=0;window.seek=t=>{playTime=Math.max(0,t);settings.paused=true;document.getElementById('play').textContent='播放';window.drawAt(playTime)};
function timelineEnd(){return Math.max(25,...rig.parts.map(p=>p.start+p.duration+p.eyeDelay+p.eyeDuration+5))}
function tick(now){if(last&&!settings.paused)playTime+=(now-last)/1000*settings.speed;last=now;playTime=Math.min(playTime,timelineEnd());document.getElementById('seek').max=timelineEnd();window.drawAt(playTime);requestAnimationFrame(tick)}
const panel=document.getElementById('parts');
for(const p of rig.parts.slice().sort((a,b)=>a.start-b.start)){const f=document.createElement('fieldset');f.dataset.name=p.name;f.innerHTML=`<legend><label><input type="checkbox" data-key="visible" checked> ${p.label}</label></legend><label>开始（秒） <input type="number" min="0" step="0.1" data-key="start" value="${p.start}"></label><label>抬起时长 <input type="number" min="0.3" step="0.1" data-key="duration" value="${p.duration}"></label><label>到位后睁眼延迟 <input type="number" min="0" step="0.05" data-key="eyeDelay" value="${p.eyeDelay}"></label><label>微动幅度 <input type="number" min="0" max="3" step="0.1" data-key="idle" value="1"></label>`;for(const el of f.querySelectorAll('input'))el.oninput=()=>{p[el.dataset.key]=el.type==='checkbox'?el.checked:Math.max(el.dataset.key==='duration'?.3:0,+el.value);document.getElementById('seek').max=timelineEnd();window.drawAt(playTime)};panel.appendChild(f)}
document.getElementById('play').onclick=e=>{settings.paused=!settings.paused;e.target.textContent=settings.paused?'播放':'暂停'};
document.getElementById('replay').onclick=()=>{playTime=0;settings.paused=false;document.getElementById('play').textContent='暂停'};
document.getElementById('end').onclick=()=>window.seek(Math.max(...rig.parts.map(p=>p.start+p.duration+p.eyeDelay+p.eyeDuration)));
document.getElementById('seek').oninput=e=>window.seek(+e.target.value);
for(const k of ['bones','scene','idle'])document.getElementById(k).onchange=e=>{settings[k]=e.target.checked;window.drawAt(playTime)};
document.getElementById('speed').onchange=e=>settings.speed=+e.target.value;
function config(){return {version:26,heads:rig.parts.map(({name,start,duration,eyeDelay,eyeDuration,idle})=>({name,start,duration,eyeDelay,eyeDuration,idle}))}}
window.timingConfig=config;
document.getElementById('export').onclick=()=>{const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(config(),null,2)],{type:'application/json'}));a.download='WakeTiming.json';a.click();URL.revokeObjectURL(a.href)};
document.getElementById('load').onclick=()=>document.getElementById('file').click();
document.getElementById('file').onchange=async e=>{try{const c=JSON.parse(await e.target.files[0].text());for(const h of c.heads){const p=rig.parts.find(p=>p.name===h.name);if(p)for(const k of ['start','duration','eyeDelay','eyeDuration','idle'])if(Number.isFinite(h[k]))p[k]=Math.max(k==='duration'?.3:0,h[k])}let fields=panel.querySelectorAll('fieldset');fields.forEach(f=>{const p=rig.parts.find(p=>p.name===f.dataset.name);for(const el of f.querySelectorAll('input[type=number]'))el.value=p[el.dataset.key]});document.getElementById('seek').max=timelineEnd();window.seek(0)}catch(err){alert('配置读取失败：'+err.message)}};
Promise.all(Object.entries(window.ASSETS).map(([name,url])=>new Promise((res,rej)=>{let im=new Image();im.onload=()=>{images[name]=im;textures[name]=upload(im);res()};im.onerror=rej;im.src=url}))).then(()=>{window.drawAt(0);window.ready=true;if(!window.OFFLINE_RENDER)requestAnimationFrame(tick)});

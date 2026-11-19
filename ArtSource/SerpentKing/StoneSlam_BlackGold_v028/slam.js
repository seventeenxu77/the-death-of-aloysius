// Real 2D linear-blend skinning driven by parented bones and inverse bind poses.
// No background pixels or full-frame displacement field participate in the skin.
const rig=window.RIG,canvas=document.getElementById('stage'),ctx=canvas.getContext('2d');
const surface=document.createElement('canvas');surface.width=canvas.width;surface.height=canvas.height;
const gl=surface.getContext('webgl',{alpha:true,antialias:true,preserveDrawingBuffer:true});
const settings={amplitude:1,speed:1,bones:false,still:false,paused:false};
const colors=['#b89ee9','#e9ad6a','#ec8195','#f2df8c'];
const image=new Image(),closedImage=new Image();let openTexture,closedTexture;
function shader(type,text){const s=gl.createShader(type);gl.shaderSource(s,text);gl.compileShader(s);return s;}
const program=gl.createProgram();
gl.attachShader(program,shader(gl.VERTEX_SHADER,'attribute vec2 aPosition;attribute vec2 aUV;varying vec2 vUV;void main(){gl_Position=vec4(aPosition,0.,1.);vUV=aUV;}'));
gl.attachShader(program,shader(gl.FRAGMENT_SHADER,'precision mediump float;varying vec2 vUV;uniform sampler2D uTexture;uniform sampler2D uClosed;uniform float closedEye;void main(){vec4 a=texture2D(uTexture,vUV),b=texture2D(uClosed,vUV);vec2 p=vUV*vec2(1024.,1536.);float mask=smoothstep(332.,340.,p.x)*(1.-smoothstep(412.,420.,p.x))*smoothstep(1000.,1008.,p.y)*(1.-smoothstep(1082.,1090.,p.y));gl_FragColor=mix(a,b,mask*closedEye);}'));
gl.linkProgram(program);gl.useProgram(program);gl.uniform1i(gl.getUniformLocation(program,"uTexture"),0);gl.uniform1i(gl.getUniformLocation(program,"uClosed"),1);
const pa=gl.getAttribLocation(program,'aPosition'),ua=gl.getAttribLocation(program,'aUV');
gl.enableVertexAttribArray(pa);gl.enableVertexAttribArray(ua);
gl.enable(gl.BLEND);gl.blendFuncSeparate(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA,gl.ONE,gl.ONE_MINUS_SRC_ALPHA);
function buffer(data,target){const b=gl.createBuffer();gl.bindBuffer(target,b);gl.bufferData(target,data,gl.STATIC_DRAW);return b;}
for(const m of rig.meshes){
 m.positions=new Float32Array(m.vertices.length*2);
 m.pb=buffer(m.positions,gl.ARRAY_BUFFER);
 m.ub=buffer(new Float32Array(m.uv.flat()),gl.ARRAY_BUFFER);
 m.ib=buffer(new Uint16Array(m.indices),gl.ELEMENT_ARRAY_BUFFER);
}
function pose(t){
 const f=((t%rig.duration+rig.duration)%rig.duration)*rig.fps,a=Math.floor(f),b=a+1,u=f-a;
 const globals=[],matrices=[];
 for(let i=0;i<rig.joints.length;i++){
  const j=rig.joints[i],p=j.parent<0?{x:0,y:0,angle:0}:globals[j.parent];
  const local=settings.still?0:(rig.frames[a][i]*(1-u)+rig.frames[b][i]*u)*settings.amplitude;
  const c=Math.cos(p.angle),s=Math.sin(p.angle);
  const localPos=settings.still?j.local:rig.localFrames[a][i].map((v,k)=>v*(1-u)+rig.localFrames[b][i][k]*u);
  const g={x:p.x+c*localPos[0]-s*localPos[1],y:p.y+s*localPos[0]+c*localPos[1],angle:p.angle+local};
  const co=Math.cos(g.angle),si=Math.sin(g.angle);
  globals.push(g);matrices.push([co,si,-si,co,g.x-co*j.rest[0]+si*j.rest[1],g.y-si*j.rest[0]-co*j.rest[1]]);
 }
 return {globals,matrices};
}
function drawBones(globals){
 ctx.save();ctx.lineWidth=3;ctx.font='18px Microsoft YaHei';
 rig.parts.forEach((p,partIndex)=>{
  const color=colors[partIndex],points=p.jointIds.map(i=>globals[i]);
  ctx.strokeStyle=color;ctx.fillStyle=color;
  for(let i=0;i<points.length;i++){
   const a=points[i],b=points[i+1]||{x:a.x-Math.sin(a.angle)*180,y:a.y+Math.cos(a.angle)*180};
   const dx=b.x-a.x,dy=b.y-a.y,n=Math.hypot(dx,dy),nx=-dy/n*7,ny=dx/n*7;
   ctx.beginPath();ctx.moveTo(a.x,a.y);ctx.lineTo(a.x+dx*.24+nx,a.y+dy*.24+ny);ctx.lineTo(b.x,b.y);ctx.lineTo(a.x+dx*.24-nx,a.y+dy*.24-ny);ctx.closePath();
   ctx.globalAlpha=.65;ctx.fill();ctx.globalAlpha=1;ctx.stroke();
   ctx.beginPath();ctx.arc(a.x,a.y,i===0?7:4,0,Math.PI*2);ctx.fill();
  }
  const first=points[0];ctx.fillText(p.label,first.x-40,first.y+34+partIndex*4);
 });ctx.restore();
}
window.drawAt=function(t){
 const {globals,matrices}=pose(t);
 const eyeTime=((t%rig.duration)+rig.duration)%rig.duration;
 const closed=(eyeTime>.13&&eyeTime<.25)||(eyeTime>1.08&&eyeTime<1.42)||(eyeTime>=2.5&&eyeTime<2.72);
 gl.uniform1f(gl.getUniformLocation(program,'closedEye'),closed?1:0);
 gl.viewport(0,0,surface.width,surface.height);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT);
 for(const m of rig.meshes){
  for(let i=0;i<m.vertices.length;i++){
   const [x,y]=m.vertices[i];let dx=0,dy=0;
   for(let k=0;k<2;k++){
    const q=matrices[m.joints[i][k]],w=m.weights[i][k];
    dx+=w*(q[0]*x+q[2]*y+q[4]);dy+=w*(q[1]*x+q[3]*y+q[5]);
   }
   m.positions[i*2]=2*dx/rig.width-1;m.positions[i*2+1]=1-2*dy/rig.height;
  }
  gl.bindBuffer(gl.ARRAY_BUFFER,m.pb);gl.bufferData(gl.ARRAY_BUFFER,m.positions,gl.DYNAMIC_DRAW);gl.vertexAttribPointer(pa,2,gl.FLOAT,false,0,0);
  gl.bindBuffer(gl.ARRAY_BUFFER,m.ub);gl.vertexAttribPointer(ua,2,gl.FLOAT,false,0,0);
  gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,m.ib);gl.drawElements(gl.TRIANGLES,m.indices.length,gl.UNSIGNED_SHORT,0);
 }
 ctx.setTransform(canvas.width/rig.width,0,0,canvas.height/rig.height,0,0);
 const phaseTime=((t%rig.duration)+rig.duration)%rig.duration;
 const last=matrices[matrices.length-1],cx=rig.contactX,cy=-190+1228*.72;
 const noseY=last[1]*cx+last[3]*cy+last[5];
 ground(phaseTime,noseY);

 ctx.drawImage(surface,0,0,rig.width,rig.height);


 const phases=[[.45,'向下探头'],[1.25,'回缩蓄力'],[2.28,'蓄力抖动'],[2.50,'加速下砸'],[2.60,'撞击停顿'],[3.10,'小幅回弹'],[3.35,'落点停留'],[4.95,'缓慢回收'],[6,'恢复待机']];
 document.getElementById('phase').textContent=phases.find(p=>phaseTime<p[0])[1];
 if(settings.bones)drawBones(globals);
};
let playingTime=0,last=0;
function tick(now){if(last&&!settings.paused)playingTime+=(now-last)/1000*settings.speed;last=now;window.drawAt(playingTime);requestAnimationFrame(tick);}
function upload(im,slot){const tex=gl.createTexture();gl.activeTexture(gl.TEXTURE0+slot);gl.bindTexture(gl.TEXTURE_2D,tex);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,im);return tex;}
Promise.all([[image,window.ATLAS],[closedImage,window.CLOSED]].map(([im,url])=>new Promise((resolve,reject)=>{im.onload=resolve;im.onerror=reject;im.src=url}))).then(()=>{openTexture=upload(image,0);closedTexture=upload(closedImage,1);window.drawAt(0);window.ready=true;if(!window.OFFLINE_RENDER)requestAnimationFrame(tick)});
window.rigSettings=settings;
document.getElementById('play').onclick=e=>{settings.paused=!settings.paused;e.target.textContent=settings.paused?'播放':'暂停';};
for(const id of ['speed'])document.getElementById(id).oninput=e=>{settings[id]=+e.target.value;document.getElementById(id+'Value').textContent=(+e.target.value).toFixed(2)+'×';};
for(const id of ['bones','still'])document.getElementById(id).onchange=e=>settings[id]=e.target.checked;
document.getElementById('reset').onclick=()=>{playingTime=0;settings.paused=false;document.getElementById('play').textContent='暂停';settings.amplitude=settings.speed=1;settings.still=false;for(const id of ['speed']){document.getElementById(id).value=1;document.getElementById(id+'Value').textContent='1.00×';}document.getElementById('still').checked=false;};

// All scenery is fixed. Only authored impact particles move, never the camera.
function ground(t,noseY){
 const f=rig.floor,x=rig.contactX;
 const g=ctx.createLinearGradient(0,0,0,rig.height);g.addColorStop(0,'#190f1b');g.addColorStop(.78,'#2c1a2b');g.addColorStop(1,'#100d14');
 ctx.fillStyle=g;ctx.fillRect(0,0,rig.width,rig.height);
 ctx.fillStyle='#29212a';ctx.fillRect(0,f,rig.width,rig.height-f);
 ctx.fillStyle='#3c3039';ctx.fillRect(0,f,rig.width,7);
 ctx.strokeStyle='#58434a';ctx.lineWidth=2;ctx.beginPath();ctx.moveTo(0,f+1);ctx.lineTo(rig.width,f+1);ctx.stroke();
 ctx.strokeStyle='#17141d';ctx.lineWidth=3;
 for(let i=-1;i<9;i++){ctx.beginPath();ctx.moveTo(i*180,f+8);ctx.lineTo(i*205-80,rig.height);ctx.stroke();}
 ctx.beginPath();ctx.moveTo(0,f+110);ctx.lineTo(rig.width,f+110);ctx.stroke();
 const close=Math.max(0,Math.min(1,1-(f-noseY)/500));ctx.fillStyle=`rgba(6,4,9,${.22+.5*close})`;
 ctx.beginPath();ctx.ellipse(x,f+5,65+90*close,8+7*close,0,0,Math.PI*2);ctx.fill();
}
window.seek=(t)=>{playingTime=t;settings.paused=true;document.getElementById('play').textContent='播放';window.drawAt(t);};
document.getElementById('seek').oninput=e=>window.seek(+e.target.value);

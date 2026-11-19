const rig=window.RIG,canvas=document.getElementById('stage'),ctx=canvas.getContext('2d');
const surface=document.createElement('canvas');surface.width=1536;surface.height=1024;
const gl=surface.getContext('webgl',{alpha:true,antialias:true,preserveDrawingBuffer:true});
const settings={bones:false,paused:false,speed:1,angle:null,idle:true};window.settings=settings;
const metadata={Time:{phase:0,row:1,cut:540,width:270,cutCenter:709,extra:0},Stone:{phase:-Math.PI/2,row:0,cut:390,width:207,cutCenter:588.5,extra:0},Fang:{phase:Math.PI/2,row:2,cut:510,width:156,cutCenter:922,extra:0},Venom:{phase:Math.PI,row:3,cut:280,width:122,cutCenter:719,extra:-35}};
const images={},textures={},clamp=x=>Math.max(0,Math.min(1,x));
const ease=x=>{x=clamp(x);return x*x*x*(x*(x*6-15)+10)};
function shader(type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s}
const program=gl.createProgram();gl.attachShader(program,shader(gl.VERTEX_SHADER,'attribute vec2 aPosition;attribute vec2 aUV;varying vec2 vUV;void main(){gl_Position=vec4(aPosition,0.,1.);vUV=aUV;}'));
gl.attachShader(program,shader(gl.FRAGMENT_SHADER,`precision mediump float;varying vec2 vUV;uniform sampler2D tex;uniform float cut;uniform float front;uniform float shade;uniform float mode;uniform vec4 frame;
void main(){if(mode>1.5){float x=vUV.x;float stripe=abs(fract(vUV.y*7.+.18*(1.-pow(abs(x*2.-1.),2.)))-.5);float ink=max(1.-smoothstep(.025,.037,stripe),1.-smoothstep(.018,.032,min(x,1.-x)));gl_FragColor=vec4(mix(vec3(.012,.008,.008),vec3(1.,.79,.34),ink)*shade,1.);return;}vec2 uv=vUV;if(mode>.5 && mode<1.5 && vUV.y>.925)discard;if(mode>.5)uv=frame.xy+vUV*frame.zw;vec4 c=texture2D(tex,uv);if(mode<.5){vec2 p=vUV*vec2(1536.,1024.);if(cut>0.&&p.y<cut)discard;float edge=785.-15.*smoothstep(850.,1150.,p.x)+65.*(1.-smoothstep(240.,400.,p.x));if(front>.5&&p.y<edge)discard;}c.rgb*=shade;gl_FragColor=c;}`));
gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program));gl.useProgram(program);
const pLoc=gl.getAttribLocation(program,'aPosition'),uLoc=gl.getAttribLocation(program,'aUV');gl.enableVertexAttribArray(pLoc);gl.enableVertexAttribArray(uLoc);
gl.enable(gl.BLEND);gl.blendFuncSeparate(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA,gl.ONE,gl.ONE_MINUS_SRC_ALPHA);
function buffer(data,target){const b=gl.createBuffer();gl.bindBuffer(target,b);gl.bufferData(target,data,gl.STATIC_DRAW);return b}
for(const m of rig.meshes){m.positions=new Float32Array(m.vertices.length*2);m.pb=buffer(m.positions,gl.ARRAY_BUFFER);m.ub=buffer(new Float32Array(m.uv.flat()),gl.ARRAY_BUFFER);m.ib=buffer(new Uint16Array(m.indices),gl.ELEMENT_ARRAY_BUFFER)}
const quad={positions:new Float32Array(8),uv:[[0,0],[1,0],[0,1],[1,1]],indices:[0,1,2,1,3,2]};
quad.pb=buffer(quad.positions,gl.ARRAY_BUFFER);quad.ub=buffer(new Float32Array(quad.uv.flat()),gl.ARRAY_BUFFER);quad.ib=buffer(new Uint16Array(quad.indices),gl.ELEMENT_ARRAY_BUFFER);
function upload(im){const tex=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,tex);for(const k of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T])gl.texParameteri(gl.TEXTURE_2D,k,gl.CLAMP_TO_EDGE);for(const k of [gl.TEXTURE_MIN_FILTER,gl.TEXTURE_MAG_FILTER])gl.texParameteri(gl.TEXTURE_2D,k,gl.LINEAR);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,im);return tex}
function yawAt(t){return Math.PI/2*ease((t-1)/3)}
function pose(t,theta){
 const joints=[],heads=[];
 for(const p of rig.parts){
  const meta=metadata[p.name],phi=meta.phase+theta,depth=Math.cos(phi),perspective=.9+.35*depth;
  const start=p.points[0],end=p.points.at(-1),cap=500/512*100/meta.width;
  const points=[];
  for(let i=0;i<p.points.length;i++){
   const q=i/(p.points.length-1),radius=60+q*270,twist=meta.phase+theta*ease(q);
   let x=768+Math.sin(twist)*radius+Math.sin(q*Math.PI*2+meta.phase)*35*Math.sin(q*Math.PI);
   let y=850-q*440+Math.cos(twist)*(20+q*50)+meta.extra*q;
   if(settings.idle){x+=Math.sin(t*.95+meta.phase)*5*q;y+=Math.sin(t*.71+meta.phase)*3*q;}
   const depthHere=Math.cos(twist),scale=.9+.35*depthHere;
   const taper=1+(cap-1)*ease((q-.7)/.3);
   const bone={x,y,sx:scale*taper,sy:scale,rest:p.points[i]};joints[p.jointIds[i]]=bone;points.push(bone);
  }
  const skull=points.at(-1);
  heads.push({name:p.name,depth,scale:perspective,points,meta,p,attachX:skull.x+(meta.cutCenter-end[0])*skull.sx,attachY:skull.y+(meta.cut-end[1])*skull.sy});
 }
 for(const id of rig.bodyJointIds){const r=rig.joints[id].rest,u=r[0]/1536;const shift=Math.sin(theta)*14*Math.sin(u*Math.PI);joints[id]={x:r[0]+shift,y:r[1]+Math.sin(theta*2+u*3)*4*Math.sin(u*Math.PI),sx:1,sy:1,rest:r};}
 return {joints,heads:heads.sort((a,b)=>a.depth-b.depth)};
}
function drawMesh(m,texture,options={}){
 gl.bindTexture(gl.TEXTURE_2D,textures[texture]);gl.uniform1f(gl.getUniformLocation(program,'cut'),options.cut||-1);gl.uniform1f(gl.getUniformLocation(program,'front'),options.front?1:0);gl.uniform1f(gl.getUniformLocation(program,'shade'),options.shade||1);gl.uniform1f(gl.getUniformLocation(program,'mode'),options.ribbon?2:(options.frame?1:0));gl.uniform4fv(gl.getUniformLocation(program,'frame'),options.frame||[0,0,1,1]);
 gl.bindBuffer(gl.ARRAY_BUFFER,m.pb);gl.bufferData(gl.ARRAY_BUFFER,m.positions,gl.DYNAMIC_DRAW);gl.vertexAttribPointer(pLoc,2,gl.FLOAT,false,0,0);
 gl.bindBuffer(gl.ARRAY_BUFFER,m.ub);gl.vertexAttribPointer(uLoc,2,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,m.ib);gl.drawElements(gl.TRIANGLES,m.indices.length,gl.UNSIGNED_SHORT,0);
}
const ribbons={};
for(const p of rig.parts){
 const count=81,m={positions:new Float32Array(count*4),uv:[],indices:[]};
 for(let i=0;i<count;i++){m.uv.push([0,i/(count-1)],[1,i/(count-1)]);if(i<count-1){let a=i*2;m.indices.push(a,a+1,a+2,a+1,a+3,a+2)}}
 m.pb=buffer(m.positions,gl.ARRAY_BUFFER);m.ub=buffer(new Float32Array(m.uv.flat()),gl.ARRAY_BUFFER);m.ib=buffer(new Uint16Array(m.indices),gl.ELEMENT_ARRAY_BUFFER);ribbons[p.name]=m;
}
function curve(points,q){
 const u=clamp(q)*(points.length-1),k=Math.min(points.length-2,Math.floor(u)),t=u-k;
 const p0=points[Math.max(0,k-1)],p1=points[k],p2=points[k+1],p3=points[Math.min(points.length-1,k+2)];
 const f=key=>.5*((2*p1[key])+(-p0[key]+p2[key])*t+(2*p0[key]-5*p1[key]+4*p2[key]-p3[key])*t*t+(-p0[key]+3*p1[key]-3*p2[key]+p3[key])*t*t*t);
 return {x:f('x'),y:f('y')};
}
function ribbon(h){
 const m=ribbons[h.name],points=h.points.map(p=>({x:p.x,y:p.y}));points[points.length-2]={x:h.attachX,y:h.attachY+42*h.scale};points[points.length-1]={x:h.attachX,y:h.attachY-15*h.scale};
 for(let i=0;i<81;i++){
  const q=i/80,p=curve(points,q),a=curve(points,Math.max(0,q-.005)),b=curve(points,Math.min(1,q+.005));
  let nx=-(b.y-a.y),ny=b.x-a.x,len=Math.hypot(nx,ny)||1;nx/=len;ny/=len;
  const width=(130*(1-q)+80*h.scale*q)*.5;
  for(let side=0;side<2;side++){const sign=side?1:-1,j=(i*2+side)*2;m.positions[j]=(p.x+nx*width*sign)/768-1;m.positions[j+1]=1-(p.y+ny*width*sign)/512;}
 }
 return m;
}
function deform(m,state){
 for(let i=0;i<m.vertices.length;i++){
  const [x,y]=m.vertices[i];let px=0,py=0;
  for(let k=0;k<2;k++){const b=state.joints[m.joints[i][k]],w=m.weights[i][k];px+=w*(b.x+(x-b.rest[0])*b.sx);py+=w*(b.y+(y-b.rest[1])*b.sy);}
  m.positions[i*2]=px/768-1;m.positions[i*2+1]=1-py/512;
 }
}
window.poseAt=pose;
window.drawAt=function(t){
 const theta=settings.angle===null?yawAt(t):settings.angle*Math.PI/180,state=pose(t,theta);
 gl.viewport(0,0,1536,1024);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT);
 const body=rig.meshes.find(m=>m.name==='Coils');deform(body,state);drawMesh(body,'Coils.png');
 for(const h of state.heads){
  drawMesh(ribbon(h),'Coils.png',{ribbon:true,shade:.85+.15*(h.depth+1)/2});
  // Each head uses a true front/three-quarter/profile or rear drawing, never an X-scale flip.
  const frame=Math.min(2,Math.floor(theta/(Math.PI/2)*2+.5));
  const bases={Stone:[300,262,256],Time:[257,260,230],Fang:[226,258,260],Venom:[260,258,257]};
  const w=500*h.scale,hh=250*h.scale,x=h.attachX-w*bases[h.name][frame]/512,y=h.attachY-hh+10*h.scale;
  const pts=[[x,y],[x+w,y],[x,y+hh],[x+w,y+hh]];
  for(let i=0;i<4;i++){quad.positions[i*2]=pts[i][0]/768-1;quad.positions[i*2+1]=1-pts[i][1]/512;}
  drawMesh(quad,'HeadTurnAtlas.png',{frame:[frame/3,h.meta.row/4,1/3,1/4],shade:.85+.15*(h.depth+1)/2});
 }
 drawMesh(body,'Coils.png',{front:true});
 ctx.fillStyle='#211421';ctx.fillRect(0,0,1536,1024);ctx.fillStyle='#100b12';ctx.beginPath();ctx.ellipse(768,980,480,25,0,0,Math.PI*2);ctx.fill();ctx.drawImage(surface,0,0);
 if(settings.bones){ctx.lineWidth=2;ctx.strokeStyle='#5dd1c6';for(const h of state.heads){ctx.beginPath();h.points.forEach((p,i)=>i?ctx.lineTo(p.x,p.y):ctx.moveTo(p.x,p.y));ctx.stroke();for(const p of h.points){ctx.beginPath();ctx.arc(p.x,p.y,4,0,Math.PI*2);ctx.fillStyle='#5dd1c6';ctx.fill();}}}
 const front=state.heads.at(-1);document.getElementById('front').textContent='当前最前：'+{Stone:'巨石',Time:'时间',Venom:'毒液',Fang:'突刺'}[front.name];
 document.getElementById('angle').value=theta*180/Math.PI;document.getElementById('angleValue').textContent=(theta*180/Math.PI).toFixed(1)+'°';document.getElementById('seek').value=t;document.getElementById('clock').textContent=t.toFixed(2)+' s';
};
let time=0,last=0;function tick(now){if(last&&!settings.paused)time=Math.min(8,time+(now-last)/1000*settings.speed);last=now;drawAt(time);requestAnimationFrame(tick)}
window.seek=t=>{time=t;settings.angle=null;settings.paused=true;drawAt(t);document.getElementById('play').textContent='播放'};
document.getElementById('play').onclick=e=>{settings.paused=!settings.paused;e.target.textContent=settings.paused?'播放':'暂停'};
document.getElementById('replay').onclick=()=>{settings.angle=null;time=0;settings.paused=false;document.getElementById('play').textContent='暂停'};
document.getElementById('stone').onclick=()=>window.seek(5);
document.getElementById('time').onclick=()=>window.seek(0);
document.getElementById('seek').oninput=e=>window.seek(+e.target.value);
document.getElementById('angle').oninput=e=>{settings.paused=true;settings.angle=+e.target.value;drawAt(time)};
document.getElementById('bones').onchange=e=>{settings.bones=e.target.checked;drawAt(time)};
document.getElementById('speed').onchange=e=>settings.speed=+e.target.value;
Promise.all(Object.entries(window.ASSETS).map(([name,url])=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images[name]=im;textures[name]=upload(im);resolve()};im.onerror=reject;im.src=url}))).then(()=>{window.ready=true;drawAt(0);if(!location.search.includes('render=1'))requestAnimationFrame(tick)});

const canvas=document.getElementById('canvas'),ctx=canvas.getContext('2d'),rig=window.RIG;
const surface=document.createElement('canvas');surface.width=canvas.width;surface.height=canvas.height;
const gl=surface.getContext('webgl',{alpha:true,premultipliedAlpha:false,preserveDrawingBuffer:true});
const vs=`attribute vec2 pos;attribute vec2 uv;varying vec2 tex;void main(){gl_Position=vec4(pos,0.,1.);tex=uv;}`;
const fs=`precision mediump float;varying vec2 tex;uniform sampler2D art;void main(){vec4 c=texture2D(art,tex);c.a=smoothstep(.86,.965,c.a);if(c.a<.01)discard;gl_FragColor=c;}`;
function shader(type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s}
const program=gl.createProgram();gl.attachShader(program,shader(gl.VERTEX_SHADER,vs));gl.attachShader(program,shader(gl.FRAGMENT_SHADER,fs));gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program));gl.useProgram(program);
const pb=gl.createBuffer(),ub=gl.createBuffer(),ib=gl.createBuffer();
gl.bindBuffer(gl.ARRAY_BUFFER,ub);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(rig.vertices.flatMap(v=>[v[0]/rig.width,v[1]/rig.height])),gl.STATIC_DRAW);
const up=gl.getAttribLocation(program,'uv');gl.vertexAttribPointer(up,2,gl.FLOAT,false,0,0);gl.enableVertexAttribArray(up);
gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(rig.indices),gl.STATIC_DRAW);
const positions=new Float32Array(rig.vertices.length*2),pp=gl.getAttribLocation(program,'pos');
const rest=rig.chain,N=rest.length,lengths=[],angles=[],jointAngles=[];
for(let i=0;i<N-1;i++){const a=rest[i],b=rest[i+1];lengths.push(Math.hypot(b[0]-a[0],b[1]-a[1]));angles.push(Math.atan2(b[1]-a[1],b[0]-a[0]))}
function angularMean(a,b){return a+Math.atan2(Math.sin(b-a),Math.cos(b-a))*.5}
for(let i=0;i<N;i++)jointAngles.push(i===0?angles[0]:i===N-1?angles.at(-1):angularMean(angles[i-1],angles[i]));
let frequency=.78,amplitude=1,time=0,playing=true,previous=0,ready=false;
const scale=.745,origin=[canvas.width*.5,canvas.height-105],root=rest[0];
const view=p=>[origin[0]+(p[0]-root[0])*scale,origin[1]+(p[1]-root[1])*scale];
const ease=x=>{x=Math.max(0,Math.min(1,x));return x*x*(3-2*x)};
function solve(t){
 const points=[rest[0].slice()],phi=[],c=[],s=[];
 for(let i=0;i<N-1;i++){
  const u=(i+.5)/(N-1),phase=2*Math.PI*frequency*(t-u*.39/frequency),wave=Math.sin(phase)+.24*Math.sin(2*phase+.7)+.11*Math.sin(phase*.617+u*3.1);
  const gain=ease(u/.28)*(.17+.62*u),tip=ease((u-.6)/.4)*.13*Math.pow(Math.sin(phase-.3),3);
  const a=angles[i]+amplitude*(gain*wave+tip);phi.push(a);
  points.push([points[i][0]+Math.cos(a)*lengths[i],points[i][1]+Math.sin(a)*lengths[i]]);
 }
 for(let i=0;i<N;i++){const a=i===0?phi[0]:i===N-1?phi.at(-1):angularMean(phi[i-1],phi[i]),delta=a-jointAngles[i];c.push(Math.cos(delta));s.push(Math.sin(delta))}
 return {points,c,s,phi};
}
function transform(point,state,bone){const x=point[0]-rest[bone][0],y=point[1]-rest[bone][1];return [state.points[bone][0]+state.c[bone]*x-state.s[bone]*y,state.points[bone][1]+state.s[bone]*x+state.c[bone]*y]}
function mesh(t,state){
 for(let i=0;i<rig.vertices.length;i++){
  const [k,w]=rig.binding[i],v=rig.vertices[i],a=transform(v,state,k),b=transform(v,state,k+1),p=view([a[0]+(b[0]-a[0])*w,a[1]+(b[1]-a[1])*w]);
  positions[i*2]=p[0]/canvas.width*2-1;positions[i*2+1]=1-p[1]/canvas.height*2;
 }
 gl.viewport(0,0,surface.width,surface.height);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT);gl.disable(gl.BLEND);
 gl.bindBuffer(gl.ARRAY_BUFFER,pb);gl.bufferData(gl.ARRAY_BUFFER,positions,gl.DYNAMIC_DRAW);gl.vertexAttribPointer(pp,2,gl.FLOAT,false,0,0);gl.enableVertexAttribArray(pp);
 gl.drawElements(gl.TRIANGLES,rig.indices.length,gl.UNSIGNED_SHORT,0);
 ctx.drawImage(surface,0,0);
}
function random(n){const x=Math.sin(n*127.1+311.7)*43758.5453;return x-Math.floor(x)}
function emission(t,offset){
 const state=solve(t),v=[rig.wound[0]+rig.cutAxis[0]*offset,rig.wound[1]+rig.cutAxis[1]*offset];
 return {point:transform(v,state,N-1),direction:[Math.cos(state.phi.at(-1)),Math.sin(state.phi.at(-1))]};
}
function particles(t){
 if(!document.getElementById('blood').checked)return;
 const rate=25,first=Math.floor((t-2.25)*rate),last=Math.floor(t*rate);
 ctx.save();ctx.lineCap='round';
 for(let i=first;i<=last;i++){
  const born=i/rate,age=t-born,life=.85+random(i+47)*1.35;if(age<0||age>life)continue;
  const off=(random(i+19)-.5)*rig.cutWidth*.67,a=emission(born,off),b=emission(born-.008,off);
  const vx=(a.point[0]-b.point[0])/.008*.68+a.direction[0]*(80+random(i+67)*180)+(random(i+23)-.5)*145;
  const vy=(a.point[1]-b.point[1])/.008*.68+a.direction[1]*(80+random(i+67)*180)+(random(i+83)-.5)*105;
  const drag=(1-Math.exp(-age*.6))/.6,p=view([a.point[0]+vx*drag,a.point[1]+vy*drag+230*age*age]);
  const velocity=[vx*Math.exp(-age*.6),vy*Math.exp(-age*.6)+460*age],speed=Math.hypot(...velocity),angle=Math.atan2(velocity[1],velocity[0]);
  const radius=(2+random(i+53)*4.4)*scale,stretch=Math.min(3.4,1+speed/240);
  ctx.globalAlpha=1-ease((age/life-.72)/.28);ctx.fillStyle=i%4===0?'#b72735':'#8e1424';
  ctx.save();ctx.translate(...p);ctx.rotate(angle);ctx.beginPath();ctx.ellipse(0,0,radius*stretch,radius,0,0,Math.PI*2);ctx.fill();ctx.restore();
 }
 // Short fluid threads are emitted from the moving cut, not painted on the background.
 for(let j=0;j<3;j++){
  const off=(j-1)*rig.cutWidth*.19;ctx.strokeStyle=j===1?'#b51d2c':'#8d1423';ctx.lineWidth=(3.5-j*.6)*scale;ctx.globalAlpha=.8;ctx.beginPath();
  for(let n=0;n<=10;n++){
   const age=n*.016,a=emission(t-age,off),b=emission(t-age-.008,off),vx=(a.point[0]-b.point[0])/.008*.6+a.direction[0]*(115+j*36),vy=(a.point[1]-b.point[1])/.008*.6+a.direction[1]*(115+j*36);
   const p=view([a.point[0]+vx*age,a.point[1]+vy*age+230*age*age]);n?ctx.lineTo(...p):ctx.moveTo(...p);
  }ctx.stroke();
 }
 ctx.restore();
}
function bones(state){
 if(!document.getElementById('bones').checked)return;
 ctx.save();ctx.strokeStyle='#55d6cf';ctx.lineWidth=2;ctx.beginPath();state.points.forEach((p,i)=>i?ctx.lineTo(...view(p)):ctx.moveTo(...view(p)));ctx.stroke();
 state.points.forEach((p,i)=>{if(i%4)return;const q=view(p);ctx.fillStyle=i===0?'#fff1b6':'#55d6cf';ctx.beginPath();ctx.arc(...q,i===0?7:3.5,0,Math.PI*2);ctx.fill()});
 ctx.fillStyle='#fff1b6';ctx.font='18px system-ui';ctx.fillText('固定根部',...view([root[0]+24,root[1]+50]));ctx.restore();
}
function draw(t){if(!ready)return;const state=solve(t);ctx.fillStyle='#18070d';ctx.fillRect(0,0,canvas.width,canvas.height);mesh(t,state);particles(t);bones(state)}
window.renderAt=t=>{time=t;draw(t)};
window.skeletonAt=t=>solve(t).points;
const im=new Image();im.onload=()=>{const texture=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,texture);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,im);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);ready=window.ready=true;draw(0);if(!location.search.includes('render=1'))requestAnimationFrame(tick)};im.src=window.IMAGE;
function tick(now){if(previous&&playing)time+=(now-previous)/1000;previous=now;draw(time);requestAnimationFrame(tick)}
document.getElementById('pause').onclick=e=>{playing=!playing;e.target.textContent=playing?'暂停':'播放'};
document.getElementById('frequency').oninput=e=>{frequency=+e.target.value;document.getElementById('freqValue').value=frequency.toFixed(2)+' Hz';draw(time)};
document.getElementById('amplitude').oninput=e=>{amplitude=+e.target.value;document.getElementById('ampValue').value=Math.round(amplitude*100)+'%';draw(time)};
for(const id of ['blood','bones'])document.getElementById(id).onchange=()=>draw(time);

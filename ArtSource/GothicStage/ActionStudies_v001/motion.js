const canvas=document.getElementById('stage'),ctx=canvas.getContext('2d'),W=1440,H=900,DURATION=4.5;
const names=Object.fromEntries(RIG.bones.map((b,i)=>[b.name,i]));
const clamp=(a,l=0,h=1)=>Math.max(l,Math.min(h,a)),mix=(a,b,t)=>a+(b-a)*t,smooth=t=>{t=clamp(t);return t*t*(3-2*t)},easeOut=t=>1-(1-clamp(t))**2;
const range=(t,a,b)=>smooth((t-a)/(b-a));
function fresh(){return {x:RIG.bones.map(b=>b.x),y:RIG.bones.map(b=>b.y),angle:RIG.bones.map(b=>b.rotation||0)}}
function worlds(p){const ws=[];RIG.bones.forEach((b,i)=>{let q=b.parent?ws[names[b.parent]]:{x:0,y:0,a:0};let c=Math.cos(q.a),s=Math.sin(q.a);ws.push({x:q.x+p.x[i]*c-p.y[i]*s,y:q.y+p.x[i]*s+p.y[i]*c,a:q.a+p.angle[i]});});return ws;}
function angle(p,n,a){p.angle[names[n]]=a}
function twoBone(p,first,second,last,target,sign=-1,footAngle=0){
 let w=worlds(p),i=names[first],j=names[second],k=names[last],par=names[RIG.bones[i].parent];
 let dx=target.x-w[i].x,dy=target.y-w[i].y,l1=Math.hypot(p.x[j],p.y[j]),l2=Math.hypot(p.x[k],p.y[k]),d=clamp(Math.hypot(dx,dy),Math.abs(l1-l2)+.01,l1+l2-.01);
 let a=Math.atan2(dy,dx)+sign*Math.acos(clamp((l1*l1+d*d-l2*l2)/(2*l1*d),-1,1));
 let ex=w[i].x+Math.cos(a)*l1,ey=w[i].y+Math.sin(a)*l1,b=Math.atan2(target.y-ey,target.x-ex);
 p.angle[i]=a-w[par].a-Math.atan2(p.y[j],p.x[j]);
 p.angle[j]=b-a+Math.atan2(p.y[j],p.x[j])-Math.atan2(p.y[k],p.x[k]);
 w=worlds(p);p.angle[k]=footAngle-w[j].a;
}
function legs(p,far=-20,near=24){twoBone(p,'far_thigh','far_shin','far_foot',{x:far,y:605},-1);twoBone(p,'near_thigh','near_shin','near_foot',{x:near,y:605},-1);}
function settleCloth(p,t,impact,lean){
 const damp=t<impact?0:Math.exp(-(t-impact)*3.8)*Math.sin((t-impact)*11);
 angle(p,'hair_root',-lean*.65+damp*.035);angle(p,'hair_mid',-damp*.055);angle(p,'hair_tip',damp*.045);
 angle(p,'veil_root',-lean*.45-damp*.045);angle(p,'veil_1',damp*.035);angle(p,'veil_2',-damp*.025);angle(p,'veil_tip',damp*.03);
 angle(p,'skirt_front_root',lean*.32);angle(p,'skirt_front_mid',-lean*.4);angle(p,'skirt_front_tip',-lean*.15);
 angle(p,'skirt_back_root',-lean*.18);angle(p,'skirt_back_mid',damp*.03);angle(p,'skirt_back_tip',-damp*.04);
 angle(p,'scroll_root',-lean*.55+damp*.08);angle(p,'scroll_tip',-damp*.06);
}
function landing(t){
 let p=fresh(),u=clamp(t/.62),drop=t<.62?-210*(1-u*u):0;
 let compression=t<.62?mix(10,4,u):t<.79?mix(4,92,easeOut((t-.62)/.17)):t<1.06?92+3*Math.sin((t-.79)/.27*Math.PI):t<2.3?mix(92,8,range(t,1.06,2.3)):8;
 let c=compression/92,lean=mix(.04,.38,c);p.x[names.pelvis]=-27*c;p.y[names.pelvis]=345+compression;
 angle(p,'torso',lean);angle(p,'neck',-lean*.48);angle(p,'head',-lean*.25);
 legs(p,-22,25);
 twoBone(p,'near_upperarm','near_forearm','near_hand',{x:mix(24,90,c),y:mix(345,422,c)},1,-.25*c);
 twoBone(p,'far_upperarm','far_forearm','far_hand',{x:mix(30,120,c),y:mix(340,400,c)},1,-.2*c);
 settleCloth(p,t,.7,lean);p.y[0]=drop;
 return {pose:p,phase:t<.62?'下落 · 准备接地':t<.79?'接地 · 屈膝缓冲':t<1.06?'半蹲 · 稳住重心':t<2.3?'蹬腿 · 缓缓起身':'站稳 · 衣料回落',grip:false};
}
function lever(t){
 let p=fresh(),reach=range(t,.35,1.12),push=range(t,1.3,1.96),release=range(t,2.26,2.95),active=reach*(1-release);
 let theta=mix(-130,-50,push)*Math.PI/180,pivot={x:115,y:381},tip={x:pivot.x+Math.cos(theta)*78,y:pivot.y+Math.sin(theta)*78};
 let lean=(.07+.18*push)*active;p.x[names.pelvis]=20*push*(1-release);p.y[names.pelvis]=353+7*active;
 angle(p,'torso',lean);angle(p,'neck',-lean*.3);angle(p,'head',.08*active-lean*.2);legs(p,-20,26);
 let rest={x:24,y:351},hand={x:tip.x,y:tip.y-19};
 let target={x:mix(rest.x,hand.x,active),y:mix(rest.y,hand.y,active)};
 twoBone(p,'near_upperarm','near_forearm','near_hand',target,1,0);
 twoBone(p,'far_upperarm','far_forearm','far_hand',{x:mix(36,-9,active),y:mix(349,349,active)},1,.08);
 settleCloth(p,t,1.96,lean);
 return {pose:p,phase:t<.35?'站定 · 看向开关':t<1.12?'伸手 · 找到把手':t<1.3?'握住 · 停顿':t<1.96?'身体前倾 · 拨动':t<2.26?'到位 · 开关开启':t<2.95?'松手 · 收回':'恢复站姿',grip:t>=1.12&&t<=2.26,tip,pivot,theta,on:t>=1.96};
}
const surface=document.createElement('canvas');surface.width=W;surface.height=H;
const gl=surface.getContext('webgl',{alpha:true,antialias:true,preserveDrawingBuffer:true});
function shader(type,text){let s=gl.createShader(type);gl.shaderSource(s,text);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;}
let prog=gl.createProgram();gl.attachShader(prog,shader(gl.VERTEX_SHADER,'attribute vec2 pos;attribute vec2 uv;varying vec2 tex;void main(){gl_Position=vec4(pos.x/720.0-1.0,1.0-pos.y/450.0,0.,1.);tex=uv;}'));gl.attachShader(prog,shader(gl.FRAGMENT_SHADER,'precision mediump float;varying vec2 tex;uniform sampler2D map;void main(){gl_FragColor=texture2D(map,tex);}'));gl.linkProgram(prog);gl.useProgram(prog);
gl.enable(gl.BLEND);gl.blendFuncSeparate(gl.SRC_ALPHA,gl.ONE_MINUS_SRC_ALPHA,gl.ONE,gl.ONE_MINUS_SRC_ALPHA);let posLoc=gl.getAttribLocation(prog,'pos'),uvLoc=gl.getAttribLocation(prog,'uv');
let resources=[];
async function init(){for(let part of RIG.parts){let im=new Image();im.src=part.data;await im.decode();let tex=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,tex);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,im);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);let vb=gl.createBuffer(),ub=gl.createBuffer(),ib=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,ub);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(part.uv.flat()),gl.STATIC_DRAW);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,ib);gl.bufferData(gl.ELEMENT_ARRAY_BUFFER,new Uint16Array(part.triangles),gl.STATIC_DRAW);resources.push({part,tex,vb,ub,ib});}window.ready=true;drawAt(0);if(!location.search.includes('render=1'))requestAnimationFrame(tick);}
function skin(p,ox,scale=1.02){let w=worlds(p);for(let r of resources){let verts=[];r.part.vertices.forEach((v,i)=>{let x=0,y=0;for(let [name,weight] of r.part.weights[i]){let b=w[names[name]],rest=RIG.bind[name],dx=v[0]-rest[0],dy=v[1]-rest[1],c=Math.cos(b.a),s=Math.sin(b.a);x+=(b.x+c*dx-s*dy)*weight;y+=(b.y+s*dx+c*dy)*weight;}if(['veil','skirt_back'].includes(r.part.name)){let pin=smooth((v[1]-510)/120)*clamp((p.y[names.pelvis]-353)/80);x=mix(x,v[0],pin);y=mix(y,v[1]+p.y[0],pin);}if(['veil','skirt_back','skirt_front','scroll'].includes(r.part.name))y=Math.min(y,632+p.y[0]);verts.push(ox+x*scale,780-630*scale+y*scale);});gl.bindBuffer(gl.ARRAY_BUFFER,r.vb);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(verts),gl.DYNAMIC_DRAW);gl.enableVertexAttribArray(posLoc);gl.vertexAttribPointer(posLoc,2,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ARRAY_BUFFER,r.ub);gl.enableVertexAttribArray(uvLoc);gl.vertexAttribPointer(uvLoc,2,gl.FLOAT,false,0,0);gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER,r.ib);gl.bindTexture(gl.TEXTURE_2D,r.tex);gl.drawElements(gl.TRIANGLES,r.part.triangles.length,gl.UNSIGNED_SHORT,0);}return w;}
function drawLever(a){ctx.save();ctx.translate(1160,780-630*1.02);ctx.scale(1.02,1.02);let p=a.pivot;ctx.fillStyle='#100e13';ctx.strokeStyle='#9e7b42';ctx.lineWidth=3;ctx.beginPath();ctx.moveTo(p.x-28,630);ctx.lineTo(p.x-15,p.y+20);ctx.lineTo(p.x+15,p.y+20);ctx.lineTo(p.x+28,630);ctx.closePath();ctx.fill();ctx.stroke();ctx.fillRect(p.x-52,617,104,15);ctx.strokeRect(p.x-52,617,104,15);ctx.fillStyle='#46301d';ctx.beginPath();ctx.arc(p.x,p.y,20,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.lineWidth=13;ctx.strokeStyle='#100d13';ctx.beginPath();ctx.moveTo(p.x,p.y);ctx.lineTo(a.tip.x,a.tip.y);ctx.stroke();ctx.lineWidth=3;ctx.strokeStyle='#c59e56';ctx.stroke();ctx.fillStyle=a.on?'#dfbe70':'#752e34';ctx.beginPath();ctx.arc(a.tip.x,a.tip.y,10,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.fillStyle=a.on?'#d3ae63':'#342a28';ctx.fillRect(p.x-6,p.y+42,12,25);ctx.restore();}
function bones(w,ox){ctx.strokeStyle='#78dbc3';ctx.fillStyle='#e4f8a7';ctx.lineWidth=1.5;RIG.bones.forEach((b,i)=>{if(b.kind==='cloth')return;let q=w[i],par=b.parent?w[names[b.parent]]:null,x=ox+q.x*1.02,y=780-630*1.02+q.y*1.02;if(par){ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(ox+par.x*1.02,780-630*1.02+par.y*1.02);ctx.stroke();}ctx.beginPath();ctx.arc(x,y,3,0,7);ctx.fill();});}
window.drawAt=function(t){t=clamp(t,0,DURATION);let a=landing(t),b=lever(t);ctx.fillStyle='#241d27';ctx.fillRect(0,0,W,H);for(let x of [0,720]){let grad=ctx.createLinearGradient(x,0,x+720,900);grad.addColorStop(0,'#302632');grad.addColorStop(1,'#594638');ctx.fillStyle=grad;ctx.fillRect(x+8,8,704,884);ctx.fillStyle='#17141b';ctx.fillRect(x+8,782,704,110);ctx.fillStyle='#937549';ctx.fillRect(x+8,780,704,2);}ctx.fillStyle='#d8b77e';ctx.font='28px Microsoft YaHei';ctx.fillText('01  落地缓冲 → 站起',40,61);ctx.fillText('02  拨动开关',760,61);ctx.fillStyle='#c5b3a0';ctx.font='19px Microsoft YaHei';ctx.fillText(a.phase,40,99);ctx.fillText(b.phase,760,99);drawLever(b);gl.viewport(0,0,W,H);gl.clearColor(0,0,0,0);gl.clear(gl.COLOR_BUFFER_BIT);let wa=skin(a.pose,480),wb=skin(b.pose,1160);ctx.drawImage(surface,0,0);if(document.getElementById('bones').checked){bones(wa,480);bones(wb,1160);}ctx.fillStyle='#bfa987';ctx.font='16px Microsoft YaHei';ctx.fillText('双脚落稳 / 半蹲停顿 / 上身与衣料延迟回稳',40,838);ctx.fillText(b.on?'开关状态：开启':'开关状态：关闭',760,838);document.getElementById('scrub').value=t;return {landing:a,lever:b};};
window.exportClips=()=>({format:'aloysius-action-study-1',fps:60,duration:DURATION,bones:RIG.bones.map(b=>b.name),landing:Array.from({length:271},(_,i)=>landing(i/60).pose),lever:Array.from({length:271},(_,i)=>lever(i/60).pose),leverAngles:Array.from({length:271},(_,i)=>lever(i/60).theta),contactSeconds:.62,gripSeconds:[1.12,2.26],switchOnSeconds:1.96});
let playing=true,time=0,last=0;function tick(now){if(last&&playing)time=(time+(now-last)/1000*Number(document.getElementById('speed').value))%DURATION;last=now;drawAt(time);requestAnimationFrame(tick);}document.getElementById('play').onclick=()=>{playing=!playing;document.getElementById('play').textContent=playing?'暂停':'播放';};document.getElementById('replay').onclick=()=>{time=0;playing=true;document.getElementById('play').textContent='暂停';drawAt(time);};document.getElementById('scrub').oninput=e=>{time=Number(e.target.value);playing=false;document.getElementById('play').textContent='播放';drawAt(time);};init();

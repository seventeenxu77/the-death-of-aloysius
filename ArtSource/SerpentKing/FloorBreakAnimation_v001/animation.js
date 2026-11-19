const canvas=document.getElementById('stage'),ctx=canvas.getContext('2d');
const before=new Image(),after=new Image();
const scene=document.createElement('canvas');scene.width=1536;scene.height=1024;
const g=scene.getContext('2d');
const settings={paused:false,speed:1,detail:true,loop:true};window.settings=settings;
const IMPACT=1,DURATION=5.5,cx=263,cy=837;
const clamp=x=>Math.max(0,Math.min(1,x)),ease=x=>{x=clamp(x);return x*x*(3-2*x)};
const rnd=i=>{let x=Math.sin(i*127.1+311.7)*43758.5453;return x-Math.floor(x)};
const opening=[[136,856],[161,843],[147,834],[175,827],[177,808],[205,810],[218,820],[239,815],[256,823],[276,817],[290,805],[319,815],[336,813],[355,827],[385,814],[363,835],[372,849],[348,851],[343,872],[320,859],[295,865],[273,859],[252,871],[228,867],[212,859],[191,866],[179,856]];
function path(points){g.beginPath();points.forEach((p,i)=>i?g.lineTo(...p):g.moveTo(...p));g.closePath()}
const stoneColors=['#613336','#884e39','#4e2230','#b48341','#381b25'];
function rock(x,y,size,angle,seed,alpha){
 g.save();g.translate(x,y);g.rotate(angle);g.globalAlpha=alpha;
 const points=[[-1,-.28],[-.42,-.75],[.58,-.52],[1,.08],[.32,.59],[-.6,.48]].map(([x,y],i)=>[x*size*(.8+rnd(seed+i)*.3),y*size]);
 path(points);g.fillStyle=stoneColors[seed%stoneColors.length];g.fill();g.lineWidth=1;g.strokeStyle='#a2753d';g.stroke();
 g.beginPath();g.moveTo(points[0][0],points[0][1]);g.lineTo(0,0);g.lineTo(points[2][0],points[2][1]);g.strokeStyle='#c2954e';g.globalAlpha*=.65;g.stroke();g.restore();
}
// Textured pieces of the original tile travel as independent fragments.
const shards=Array.from({length:13},(_,i)=>{
 let x=167+rnd(i+20)*181,y=813+rnd(i+80)*47,w=20+rnd(i+220)*21,h=8+rnd(i+90)*14;
 return {x,y,points:[[x-w,y-h],[x+w*.8,y-h*.55],[x+w,y+h*.4],[x-w*.6,y+h]],vx:(x-cx)*2.6,vy:-90-rnd(i+9)*135,spin:(rnd(i+18)-.5)*7};
});
function tileShards(age){
 if(age<0||age>1)return;
 for(let i=0;i<shards.length;i++){
  const s=shards[i],a=age-Math.min(.04,i*.003);if(a<0)continue;
  const dx=s.vx*a,dy=s.vy*a+380*a*a;
  g.save();
  // Falling pieces disappear below the floor inside the opening.
  if(dy>12){g.beginPath();g.rect(128,775,275,105);g.clip();}
  g.translate(s.x+dx,s.y+dy);g.rotate(s.spin*a);g.translate(-s.x,-s.y);
  g.globalAlpha=1-ease((a-.45)/.45);path(s.points);g.clip();g.drawImage(before,0,0);g.restore();
 }
}
function debris(age){
 if(age<0)return;
 for(let i=0;i<39;i++){
  const delay=rnd(i+600)*.07,a=age-delay;if(a<0)continue;
  const originX=cx+(rnd(i+87)-.5)*160,originY=cy+(rnd(i+888)-.5)*23;
  const vx=(rnd(i+1)-.5)*410,vy=-75-rnd(i+5)*195,gravity=490;
  const flight=-2*vy/gravity,restX=originX+vx*flight;
  let x,y,ang,alpha=1;
  if(a<flight){x=originX+vx*a;y=originY+vy*a+gravity*a*a*.5;ang=a*(rnd(i+77)-.5)*16;}
  else {
   // Debris whose landing point is over the void continues down into darkness.
   if(restX>175&&restX<345)continue;
   const land=a-flight;
   x=restX+vx*.13*(1-Math.exp(-land*7));
   y=originY-Math.abs(Math.sin(land*15))*7*Math.exp(-land*10);
   ang=flight*(rnd(i+77)-.5)*16;alpha=1-ease((age-3.8)/.7);
  }
  if(y>878)continue;
  rock(x,y,2+rnd(i+40)*7,ang,i,alpha);
 }
 // Smaller fragments falling through the dark opening, clipped to its interior.
 if(age<1.1){g.save();path(opening);g.clip();for(let i=0;i<15;i++){
  const a=age-rnd(i+113)*.2;if(a<0)continue;
  const x=183+rnd(i+118)*157+(rnd(i+21)-.5)*45*a,y=816+40*a+260*a*a;
  rock(x,y,3+rnd(i+811)*6,a*4,i+44,1-clamp(a));
 }g.restore();}
}
function dust(age){
 if(age<0||age>3.5)return;
 // Muted, layered dust silhouettes; motion and opacity have separate attack and decay.
 for(let i=0;i<33;i++){
  const delay=rnd(i+400)*.12,a=age-delay;if(a<0)continue;
  const life=1.65+rnd(i+300)*1.25;if(a>life)continue;
  const side=i%2?1:-1,vx=side*(18+rnd(i+110)*55),rise=20+rnd(i+320)*52;
  const x=cx+(rnd(i+211)-.5)*115+vx*(1-Math.exp(-a*2));
  const y=cy+4-rise*(1-Math.exp(-a*2.7))-(i%4===0?a*17:0);
  const r=(8+rnd(i+78)*16)*(1+.7*a),alpha=clamp(a/.075)*Math.pow(Math.max(0,1-a/life),1.6)*.30;
  g.save();g.globalAlpha=alpha;g.fillStyle=['#a78354','#756047','#634d41','#aa8051'][i%4];
  g.beginPath();
  for(let k=0;k<12;k++){const theta=k*Math.PI*2/12,rr=r*(.85+rnd(i*20+k)*.23);let px=x+Math.cos(theta)*rr*1.5,py=y+Math.sin(theta)*rr*.68;k?g.lineTo(px,py):g.moveTo(px,py)}
  g.closePath();g.fill();g.restore();
 }
}
function fracture(t){
 const u=ease((t-.77)/.23);if(u<=0||t>=IMPACT)return;
 const branches=[[[286,826],[265,819],[247,823],[229,816]],[[286,826],[307,829],[329,819],[345,821]],[[286,826],[279,839],[263,846],[245,841]],[[286,826],[300,837],[319,846]]];
 g.strokeStyle='#c2914c';g.lineWidth=1.1;g.globalAlpha=u;
 for(let b of branches){g.beginPath();g.moveTo(...b[0]);for(let i=1;i<b.length;i++){const p=b[i-1],q=b[i],f=clamp(u*(b.length-1)-(i-1));g.lineTo(p[0]+(q[0]-p[0])*f,p[1]+(q[1]-p[1])*f)}g.stroke()}g.globalAlpha=1;
}
window.drawAt=function(t){
 t=Math.max(0,Math.min(DURATION,t));g.clearRect(0,0,1536,1024);g.drawImage(before,0,0);
 const age=t-IMPACT;
 if(age>=0){
  // Reveal only the impact patch; the rest of the room always uses the same image.
  const reveal=ease(age/.105),s=.08+.92*reveal;
  g.save();g.translate(cx,cy);g.scale(s,s);g.translate(-cx,-cy);path([[82,783],[327,781],[443,815],[446,877],[77,877]]);g.restore();
  // Expand a fixed screen-space mask, never scale the floor texture itself.
  g.save();path([[82,783],[327,781],[443,815],[446,877],[77,877]].map(([x,y])=>[cx+(x-cx)*s,cy+(y-cy)*s]));g.clip();g.drawImage(after,0,0);g.restore();
 }
 fracture(t);tileShards(age);debris(age);dust(age);
 ctx.clearRect(0,0,canvas.width,canvas.height);
 if(settings.detail)ctx.drawImage(scene,48,642,700,394,0,0,canvas.width,canvas.height);
 else ctx.drawImage(scene,0,0,canvas.width,canvas.height);
 document.getElementById('seek').value=t;document.getElementById('clock').textContent=t.toFixed(2)+' s';
 document.getElementById('phase').textContent=t<.77?'裂缝地砖':t<1?'裂纹扩展':t<1.18?'地砖撞碎':t<2?'碎石飞散、塌落':t<4.2?'灰尘散去':'洞口显露';
};
let playTime=0,last=0;
window.seek=t=>{playTime=t;settings.paused=true;window.drawAt(t);document.getElementById('play').textContent='播放'};
function tick(now){if(last&&!settings.paused){playTime+=(now-last)/1000*settings.speed;if(playTime>DURATION)playTime=settings.loop?0:DURATION;}last=now;window.drawAt(playTime);requestAnimationFrame(tick)}
document.getElementById('play').onclick=e=>{settings.paused=!settings.paused;e.target.textContent=settings.paused?'播放':'暂停'};
document.getElementById('replay').onclick=()=>{playTime=0;settings.paused=false;document.getElementById('play').textContent='暂停'};
document.getElementById('seek').oninput=e=>window.seek(+e.target.value);
document.getElementById('detail').onchange=e=>{settings.detail=e.target.checked;window.drawAt(playTime)};
document.getElementById('speed').oninput=e=>settings.speed=+e.target.value;
Promise.all([[before,window.BEFORE],[after,window.AFTER]].map(([im,url])=>new Promise((resolve,reject)=>{im.onload=resolve;im.onerror=reject;im.src=url}))).then(()=>{window.ready=true;window.drawAt(0);if(!location.search.includes('render=1'))requestAnimationFrame(tick)});

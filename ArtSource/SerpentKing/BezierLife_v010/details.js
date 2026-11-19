// Independent facial and skeletal accents. Main three-pose sway is unchanged.
let detailTime=0;
const DETAIL_PERIOD=25/3;
function envelope(t,start,rise,hold,fall){
 const a=t-start;if(a<0||a>=rise+hold+fall)return 0;
 if(a<rise)return smooth(a/rise);
 if(a<rise+hold)return 1;
 return 1-smooth((a-rise-hold)/fall);
}
function accents(t,starts,rise,hold,fall){return Math.max(0,...starts.map(s=>envelope(t,s,rise,hold,fall)))}
function keyedAccent(t,start,keys){
 const a=t-start;if(a<keys[0][0]||a>keys.at(-1)[0])return 0;
 for(let i=0;i<keys.length-1;i++)if(a<=keys[i+1][0]){const k=smooth((a-keys[i][0])/(keys[i+1][0]-keys[i][0]));return keys[i][1]+(keys[i+1][1]-keys[i][1])*k}
 return 0;
}
function stillDetail(){return {blink:0,angle:0,x:0,y:0,sacL:0,sacR:0,shake:0,tongue:0}}
function detailState(name){
 const m=stillDetail();if(!project.detailsEnabled||(!playing&&!scrubbing))return m;
 const t=((detailTime%DETAIL_PERIOD)+DETAIL_PERIOD)%DETAIL_PERIOD,k=project.detailsStrength;
 if(k<.001)return m;
 if(name==='Stone'){
  m.blink=accents(t,[1.04,6.18],.055,.09,.105);
  const keys=[[0,0],[.15,-.18],[.29,1],[.51,1],[1.03,0]];
  const nod=keyedAccent(t,.85,keys)+keyedAccent(t,5.99,keys);
  m.angle=-8.5*nod*k;m.y=11*nod*k;
 }else if(name==='Fang'){
  m.blink=accents(t,[2.65,2.83,6.54],.025,.025,.055);
  m.tongue=accents(t,[1.55,1.83,2.14,4.62,4.92,7.10,7.39],.055,.045,.11)*1.12*Math.min(1.15,k);
  const twitch=accents(t,[1.34,4.40,6.86],.05,.04,.18);
  m.angle=2.8*twitch*k;m.y=3*twitch*k;m.x=1.5*Math.sin(t*39)*twitch*k;
 }else if(name==='Venom'){
  m.blink=accents(t,[3.32,7.58],.04,.025,.09);
  const starts=[2.03,2.39,2.77,5.57,5.95,6.34];
  m.sacL=accents(t,starts,.11,.025,.19)*k;
  m.sacR=accents(t,starts.map(s=>s+.07),.09,.025,.21)*.87*k;
  const beat=Math.max(m.sacL,m.sacR);
  m.shake=Math.sin(t*67)*2.1*beat;m.angle=Math.sin(t*27)*1.2*beat;m.y=2*beat;
 }else if(name==='Time'){
  m.blink=accents(t,[4.35],.13,.09,.24);
  const nod=keyedAccent(t,4.56,[[0,0],[.40,1],[.64,1],[1.30,0]]);
  m.angle=1.4*nod*k;m.y=7*nod*k;
 }
 return m;
}
function decoratedPose(heads){
 const curves={},micro={};
 for(const p of rig.parts){
  const m=detailState(p.name),h=clone(heads[p.name]);micro[p.name]=m;
  h.angle+=m.angle;
  h.nodes[4][0]+=m.x;h.nodes[3][0]+=m.x;
  h.nodes[4][1]+=m.y;h.nodes[3][1]+=m.y;h.nodes[2][1]+=m.y*.25;
  curves[p.name]=arc(h);
 }
 return {curves,micro};
}
function facialPoint(name,x,y,m){
 if(name!=='Venom'||y>235||(!m.sacL&&!m.sacR))return [x,y];
 let dx=0,dy=0;
 const sacs=[[837,164,37,34,m.sacL],[973,163,29,33,m.sacR]];
 for(const [cx,cy,rx,ry,beat] of sacs){
  if(!beat)continue;
  const r=Math.hypot((x-cx)/rx,(y-cy)/ry),weight=1-smooth((r-.65)/.65);
  const eyeDistance=Math.min(Math.hypot((x-875)/25,(y-139)/20),Math.hypot((x-951)/23,(y-139)/20));
  const w=weight*smooth((eyeDistance-.8)/.5);
  dx+=(x-cx)*.29*beat*w+Math.sign(cx-910)*m.shake*w;
  dy+=(y-cy)*.24*beat*w+Math.sin(detailTime*51+cx)*1.6*beat*w;
 }
 return [x+dx,y+dy];
}
function drawTongue(state){
 const p=rig.parts.find(p=>p.name==='Fang'),m=state.micro.Fang;
 if(!p.visible||m.tongue<.03||!project.detailsEnabled)return;
 const a=state.curves.Fang,end=a.points.at(-1),origin=p.points.at(-1),angle=end[2]-p.referenceEndAngle+a.h.angle*Math.PI/180;
 const q=m.tongue,ax=1138,ay=441,flutter=Math.sin(detailTime*57)*3*q;
 const point=(x,y)=>[ax+(x-ax)*q,ay+(y-ay)*q];
 ctx.save();ctx.translate(end[0],end[1]+192);ctx.rotate(angle);ctx.translate(-origin[0],-origin[1]);
 ctx.lineCap='round';ctx.lineJoin='round';
 const stroke=(color,width,forkWidth)=>{
  ctx.strokeStyle=color;ctx.lineWidth=width;ctx.beginPath();ctx.moveTo(ax,ay);
  ctx.bezierCurveTo(...point(1149,446),...point(1155,450),...point(1158,463));ctx.stroke();
  ctx.lineWidth=forkWidth;ctx.beginPath();ctx.moveTo(...point(1158,463));
  ctx.bezierCurveTo(...point(1152,476),...point(1146+flutter,490),...point(1144+flutter,500));
  ctx.moveTo(...point(1158,463));ctx.bezierCurveTo(...point(1164,471),...point(1169+flutter,485),...point(1170+flutter,499));ctx.stroke();
 };
 stroke('#030202',8,6);stroke(window.TONGUE_GOLD||'#ffca65',4.5,2.7);ctx.restore();
}

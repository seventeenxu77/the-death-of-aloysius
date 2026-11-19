from pathlib import Path
import json,base64
R=Path(__file__).resolve().parent
S=R.parent/'BlackGoldWake_v026'
s=(S/'animation.js').read_text('utf8')
s=s.replace('speed:1,paused:false','speed:1,frequency:.36,paused:false')
wave='''
function swing(t,pi){
 const cycle=t*Math.max(.01,settings.frequency)*2+pi*.37,step=Math.floor(cycle),u=cycle-step,sign=step%2===0?1:-1;
 let x,y;
 if(u<.18){x=-sign;y=34}
 else if(u<.32){const k=smooth((u-.18)/.14);x=(-1+.04*k)*sign;y=34-46*k}
 else if(u<.72){const k=smooth((u-.32)/.4);x=(-.96+1.86*k)*sign;y=-12+12*k}
 else if(u<.84){const k=Math.pow((u-.72)/.12,3);x=(.9+.16*k)*sign;y=42*k}
 else if(u<.93){const k=smooth((u-.84)/.09);x=(1.06-.06*k)*sign;y=42-8*k}
 else{x=sign;y=34}
 return {x,y};
}
'''
s=s.replace('function pose(t){',wave+'function pose(t){')
start=s.index('   const phase=t*.94');end=s.index('\n   return [',start)
s=s[:start]+'''   const w=swing(t,pi),width={Fang:70,Stone:62,Venom:54,Time:56}[p.name];
   const dx=w.x*width*idle,dy=w.y*idle;'''+s[end:]
s=s.replace('  // The skull is one rigid bone:',"  const roll=swing(t,pi).x*.065*(settings.idle?p.idle:0)*smooth((age-p.duration)/.7);\n  // The skull is one rigid bone:")
s=s.replace('for(let i=0;i<n;i++)if(rest[i][1]<=p.rigidY)targets[i]=[rest[i][0]+headDelta[0],rest[i][1]+headDelta[1]];', 'for(let i=0;i<n;i++)if(rest[i][1]<=p.rigidY){const ox=rest[i][0]-rest[n-1][0],oy=rest[i][1]-rest[n-1][1];targets[i]=[rest[n-1][0]+headDelta[0]+Math.cos(roll)*ox-Math.sin(roll)*oy,rest[n-1][1]+headDelta[1]+Math.sin(roll)*ox+Math.cos(roll)*oy]}')
s=s.replace('q=targets[i];let angle=0;', 'q=targets[i];let angle=rest[i][1]<=p.rigidY?roll:roll*.18*[0,.12,.5,.85,1,1,1,1,1,1][i];')
s=s.replace('    angle=0;','    angle=roll*.18*[0,.12,.5,.85,1,1,1,1,1,1][i];')
s=s.replace('let playTime=0,last=0;', 'let playTime=25,last=0;')
s=s.replace('playTime=0;settings.paused=false','playTime=25;settings.paused=false')
s=s.replace('playTime=Math.min(playTime,timelineEnd());','if(playTime>33)playTime=25;')
s=s.replace("document.getElementById('seek').max=timelineEnd();window.drawAt(playTime);requestAnimationFrame(tick)","document.getElementById('seek').max=33;window.drawAt(playTime);requestAnimationFrame(tick)")
s=s.replace('window.drawAt(0);window.ready=true;', 'window.drawAt(25);window.ready=true;')
s=s.replace('version:26,heads:rig.parts.map', 'version:26,idleFrequencyHz:settings.frequency,heads:rig.parts.map')
s=s.replace("const c=JSON.parse(await e.target.files[0].text());", "const c=JSON.parse(await e.target.files[0].text());if(Number.isFinite(c.idleFrequencyHz)){settings.frequency=Math.max(.08,c.idleFrequencyHz);for(const k of ['frequency','frequencyNumber'])document.getElementById(k).value=settings.frequency;document.getElementById('period').textContent=(1/settings.frequency).toFixed(2)+' 秒 / 左右往返';}")
s += "\nfor(const id of ['frequency','frequencyNumber'])document.getElementById(id).oninput=e=>{settings.frequency=Math.max(.08,+e.target.value);for(const k of ['frequency','frequencyNumber'])document.getElementById(k).value=settings.frequency;document.getElementById('period').textContent=(1/settings.frequency).toFixed(2)+' 秒 / 左右往返';drawAt(playTime)};\n"
(R/'animation.js').write_text(s,'utf8')
html=(S/'preview.html.in').read_text('utf8')
html=html.replace('黑金四头蛇 · 独立骨骼苏醒','四头蛇 · 大幅摆头与下沉停顿').replace('抬起后独立微动','停顿式大幅摆头')
html=html.replace('<div id="parts"></div>', '<label>摆动频率 <input id="frequencyNumber" type="number" min=".08" max="1.5" step=".01" value=".36"> Hz<input id="frequency" type="range" min=".08" max="1.5" step=".01" value=".36"><span id="period">2.78 秒 / 左右往返</span></label><div id="parts" hidden></div>')
html=html.replace('从头播放','重新播放').replace('min="0" max="25"','min="25" max="33"')
html=html.replace('导出时机 JSON','保存配置 JSON')
html=html.replace('默认 1 / 5.5 / 12 / 17 秒开始；20 秒完成。20–25 秒展示睁眼待机。调整时间不会自动保存，请导出 JSON。','低位停顿 → 抬起释放 → 快摆 → 末端加速下沉 → 小幅回弹、压住。四头独立错拍，保持睁眼。频率只在当前预览生效；可将数值填入 Unity 的 idleFrequencyHz 属性。')
assets={f.name:'data:image/png;base64,'+base64.b64encode(f.read_bytes()).decode() for f in (S/'Layers').glob('*.png')}
html=html.replace('__RIG__',(S/'WakeRig.json').read_text('utf8')).replace('__ASSETS__',json.dumps(assets)).replace('__JS__',s)
(R/'SwayDirector.html').write_text(html,'utf8')
old=(R.parent/'FixedPositionIdlePreview_v001'/'render.py').read_text('utf8')
old=old.replace("source=R.parent/'BlackGoldWake_v026'/'WakeDirector.html'", "source=R/'SwayDirector.html'").replace('FixedPositionIdle.mp4','SunflowerSway.mp4').replace('Exported existing fixed-position idle: 8 seconds, 60 fps, amplitude unchanged.','Exported accented sway: 8 seconds, 60 fps, 0.36 Hz.')
(R/'render.py').write_text(old,'utf8')
print('Packaged paused-accent skeletal sway preview.',flush=True)

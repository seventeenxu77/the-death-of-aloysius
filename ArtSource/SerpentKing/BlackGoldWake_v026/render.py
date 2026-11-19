"""Render the authored animation and its review stills. No Unity Play-mode operations."""
from pathlib import Path
import base64,json,subprocess,shutil,sys
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parent
with sync_playwright() as p:
 browser=p.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True,args=['--use-angle=swiftshader','--enable-webgl'])
 page=browser.new_page(viewport={'width':1800,'height':1300},device_scale_factor=1)
 errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 page.goto((R/'WakeDirector.html').as_uri()+'?render=1');page.wait_for_function('window.ready===true',timeout=60000)
 def still(name,t):
  data=page.evaluate('(t)=>{window.drawAt(t);return document.getElementById("stage").toDataURL().split(",")[1]}',t)
  (R/name).write_bytes(base64.b64decode(data))
 for name,t in [('Start.png',0),('FangRaised.png',4),('VenomRaised.png',9),('StoneRaised.png',15.5),('End.png',20)]:still(name,t)
 page.evaluate('rigSettings.bones=true');still('Binding.png',20);page.evaluate('rigSettings.bones=false')
 (R/'WakeTiming.json').write_text(json.dumps(page.evaluate('timingConfig()'),ensure_ascii=False,indent=2),'utf-8')
 # Export actual evaluated parent-local transforms: usable without this browser.
 samples=page.evaluate('()=>Array.from({length:1201},(_,i)=>({time:i/60,bones:poseAt(i/60).locals}))')
 (R/'WakeBoneKeys.json').write_text(json.dumps({'fps':60,'duration':20,'frames':samples},separators=(',',':')),'utf-8')
 if errors:raise RuntimeError(errors)
 if '--stills' not in sys.argv:
  fps=60;duration=25
  with (R/'encode.log').open('w') as log:
   enc=subprocess.Popen([shutil.which('ffmpeg'),'-y','-f','image2pipe','-vcodec','png','-framerate',str(fps),'-i','-','-an','-c:v','libx264','-crf','18','-preset','fast','-pix_fmt','yuv420p','-movflags','+faststart',str(R/'BlackGoldWake.mp4')],stdin=subprocess.PIPE,stdout=log,stderr=log)
   for i in range(fps*duration):
    data=page.evaluate('(t)=>{drawAt(t);return document.getElementById("stage").toDataURL().split(",")[1]}',i/fps);enc.stdin.write(base64.b64decode(data))
    if i%120==0:print(f'Rendered {i}/{fps*duration}',flush=True)
   enc.stdin.close()
   if enc.wait():raise RuntimeError('ffmpeg encode failed')
 browser.close()
 print('Render completed; browser errors:',errors)

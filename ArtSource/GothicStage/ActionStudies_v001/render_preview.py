from pathlib import Path
import subprocess,shutil,base64,json
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parent
with sync_playwright() as pw:
 browser=pw.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True,args=['--enable-webgl','--use-gl=angle','--use-angle=swiftshader'])
 page=browser.new_page(viewport={'width':1500,'height':1100})
 page.goto((root/'MotionPreview.html').as_uri()+'?render=1');page.wait_for_function('window.ready === true')
 clips=page.evaluate('exportClips()');(root/'MotionClips.json').write_text(json.dumps(clips,separators=(',',':')),encoding='utf-8')
 for t,name in [(0,'Start'),(.79,'LandingCompression'),(1.65,'LeverContact'),(3.25,'End')]:
  raw=page.evaluate('(t)=>{drawAt(t);return document.getElementById("stage").toDataURL().split(",")[1]}',t)
  (root/(name+'.png')).write_bytes(base64.b64decode(raw))
 if '--stills' not in __import__('sys').argv:
  ff=shutil.which('ffmpeg');log=(root/'encode.log').open('w');proc=subprocess.Popen([ff,'-y','-f','image2pipe','-vcodec','png','-framerate','60','-i','-','-an','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(root/'Actions.mp4')],stdin=subprocess.PIPE,stdout=log,stderr=log)
  for i in range(270):
   raw=page.evaluate('(t)=>{drawAt(t);return document.getElementById("stage").toDataURL().split(",")[1]}',i/60)
   proc.stdin.write(base64.b64decode(raw))
   if i%60==0: print('Rendered',i,'/270',flush=True)
  proc.stdin.close();code=proc.wait();log.close()
  if code:raise RuntimeError('encode failed')
  for name,x in [('Landing',0),('Lever',720)]:
   subprocess.run([ff,'-y','-i',str(root/'Actions.mp4'),'-vf',f'crop=720:900:{x}:0','-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(root/(name+'.mp4'))],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,check=True)
 browser.close()
print('Preview exported',flush=True)

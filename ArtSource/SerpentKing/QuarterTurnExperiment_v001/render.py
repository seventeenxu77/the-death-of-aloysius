from pathlib import Path
import base64,subprocess,shutil
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parent
with sync_playwright() as p:
 b=p.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True,args=['--use-angle=swiftshader','--enable-webgl'])
 page=b.new_page(viewport={'width':1750,'height':1200})
 page.goto((R/'QuarterTurn.html').as_uri()+'?render=1');page.wait_for_function('window.ready===true')
 for name,t in [('TimeFront.png',0),('HalfTurn.png',2.5),('StoneFront.png',5)]:
  data=page.evaluate('(t)=>{drawAt(t);return document.getElementById("stage").toDataURL().split(",")[1]}',t);(R/name).write_bytes(base64.b64decode(data))
 with (R/'encode.log').open('w') as log:
  enc=subprocess.Popen([shutil.which('ffmpeg'),'-y','-f','image2pipe','-vcodec','png','-framerate','60','-i','-','-an','-c:v','libx264','-crf','18','-preset','fast','-pix_fmt','yuv420p','-movflags','+faststart',str(R/'QuarterTurn.mp4')],stdin=subprocess.PIPE,stdout=log,stderr=log)
  for i in range(480):
   data=page.evaluate('(t)=>{drawAt(t);return document.getElementById("stage").toDataURL().split(",")[1]}',i/60);enc.stdin.write(base64.b64decode(data))
   if i%120==0:print(i,'/480',flush=True)
  enc.stdin.close()
  if enc.wait():raise RuntimeError('Video encode failed')
 b.close()
print('Quarter turn video and endpoint views saved.')

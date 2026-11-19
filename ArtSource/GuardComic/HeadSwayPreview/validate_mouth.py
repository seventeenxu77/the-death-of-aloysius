from pathlib import Path
import base64,json,subprocess,shutil
from playwright.sync_api import sync_playwright
p=Path('ArtSource/GuardComic/HeadSwayPreview').resolve()
with sync_playwright() as pw:
 browser=pw.chromium.launch(executable_path='C:/Program Files/Google/Chrome/Application/chrome.exe',headless=True)
 page=browser.new_page(viewport={'width':1200,'height':1000},device_scale_factor=1)
 errors=[]
 page.on('pageerror',lambda e: errors.append(str(e)))
 page.goto((p/'Preview.html').as_uri());page.evaluate('window.guardReady')
 shots=[]
 for name,t in [('center',0),('left',.6),('right',1.8)]:
  page.evaluate('(t)=>window.renderGuardAt(t)',t)
  data=page.evaluate("document.querySelector('canvas').toDataURL('image/png').split(',')[1]")
  (p/(name+'.png')).write_bytes(base64.b64decode(data))
  shots.append(page.evaluate('window.guardPose'))
 assert not errors, errors
 assert shots[1]['angle'] < -2.7 and shots[2]['angle'] > 2.1,shots
 from PIL import Image,ImageChops
 a=Image.open(p/'left.png').convert('RGB');b=Image.open(p/'right.png').convert('RGB')
 assert ImageChops.difference(a.crop((50,990,700,1050)),b.crop((50,990,700,1050))).getbbox() is None,'Shoulder moved'
 assert ImageChops.difference(a.crop((1300,100,1400,900)),b.crop((1300,100,1400,900))).getbbox() is None,'Background moved'
 assert ImageChops.difference(a.crop((250,220,1100,700)).convert('RGB'),b.crop((250,220,1100,700)).convert('RGB')).getbbox() is not None,'Head stayed still'
 # Compare mouth frames with identical head pose so unintended image drift is detectable.
 page.evaluate("document.querySelector('#amplitude').value=0;document.querySelector('#mouthEnabled').checked=true;window.renderGuardAt(.6)")
 data=page.evaluate("document.querySelector('canvas').toDataURL('image/png').split(',')[1]")
 (p/'mouth-open-check.png').write_bytes(base64.b64decode(data))
 assert page.evaluate('window.guardPose.mouthOpen')==1
 page.evaluate("document.querySelector('#mouthEnabled').checked=false;window.renderGuardAt(.6)")
 data=page.evaluate("document.querySelector('canvas').toDataURL('image/png').split(',')[1]")
 (p/'ClosedMouth.png').write_bytes(base64.b64decode(data))
 assert page.evaluate('window.guardPose.mouthOpen')==0
 a=Image.open(p/'mouth-open-check.png').convert('RGB');b=Image.open(p/'ClosedMouth.png').convert('RGB')
 diff=ImageChops.difference(a,b);bbox=diff.getbbox();assert bbox is not None,'Mouth did not animate'
 assert bbox[0]>=240 and bbox[1]>=550 and bbox[2]<=390 and bbox[3]<=650,('Changes outside mouth',bbox)
 page.evaluate("document.querySelector('#amplitude').value=2.8;document.querySelector('#mouthEnabled').checked=true")
 (p/'Validation-mouth.json').write_text(json.dumps({'passed':True,'browserErrors':errors,'headMoves':True,'shoulderFixed':True,'backgroundFixed':True,'poses':shots,'mouthAlternates':True,'mouthOnlyDifferenceBounds':bbox,'noEyeOrHelmetDrift':True},indent=2),encoding='utf-8')
 ffmpeg=shutil.which('ffmpeg')
 proc=subprocess.Popen([ffmpeg,'-hide_banner','-loglevel','error','-y','-f','image2pipe','-vcodec','mjpeg','-r','30','-i','-','-vf','scale=1080:810','-c:v','libx264','-preset','fast','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart','-an',str(p/'HeadSwayMouth.mp4')],stdin=subprocess.PIPE)
 for f in range(156):
  page.evaluate('(t)=>window.renderGuardAt(t)',f/30)
  data=page.evaluate("document.querySelector('canvas').toDataURL('image/jpeg',.94).split(',')[1]")
  proc.stdin.write(base64.b64decode(data))
 proc.stdin.close();assert proc.wait()==0
 browser.close()
 print('PASS: animated mouth and head; no eye/helmet drift; fixed shoulder/background; video exported.')

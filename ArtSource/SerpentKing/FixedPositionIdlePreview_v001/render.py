from pathlib import Path
import base64,subprocess,shutil
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parent
source=R.parent/'BlackGoldWake_v026'/'WakeDirector.html'
with sync_playwright() as p:
    b=p.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True,args=['--use-angle=swiftshader','--enable-webgl'])
    page=b.new_page(viewport={'width':1800,'height':1300})
    page.goto(source.as_uri()+'?render=1')
    page.wait_for_function('window.ready===true',timeout=60000)
    page.evaluate('rigSettings.paused=true;rigSettings.idle=true;rigSettings.bones=false;rigSettings.scene=true')
    def frame(t):
        return base64.b64decode(page.evaluate('(t)=>{drawAt(t);return document.getElementById("stage").toDataURL().split(",")[1]}',t))
    (R/'Preview.png').write_bytes(frame(25))
    with (R/'encode.log').open('w') as log:
        enc=subprocess.Popen([shutil.which('ffmpeg'),'-y','-f','image2pipe','-vcodec','png','-framerate','60','-i','-','-an','-c:v','libx264','-crf','18','-preset','fast','-pix_fmt','yuv420p','-movflags','+faststart',str(R/'FixedPositionIdle.mp4')],stdin=subprocess.PIPE,stdout=log,stderr=log)
        for i in range(480):
            enc.stdin.write(frame(25+i/60))
            if i%120==0:print(f'{i}/480',flush=True)
        enc.stdin.close()
        if enc.wait():raise RuntimeError('Export failed')
    b.close()
print('Exported existing fixed-position idle: 8 seconds, 60 fps, amplitude unchanged.',flush=True)

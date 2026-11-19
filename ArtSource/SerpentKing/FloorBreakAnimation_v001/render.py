from pathlib import Path
import base64,subprocess,shutil
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parent
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True)
    page=browser.new_page(viewport={'width':1400,'height':1100},device_scale_factor=1)
    page.goto((R/'FloorBreak.html').as_uri()+'?render=1');page.wait_for_function('window.ready===true')
    for detail,name in [(True,'FloorBreak_Closeup.mp4'),(False,'FloorBreak_Room.mp4')]:
        page.evaluate('(v)=>window.settings.detail=v',detail)
        with (R/(name+'.log')).open('w',encoding='utf-8') as log:
            enc=subprocess.Popen([shutil.which('ffmpeg'),'-y','-f','image2pipe','-vcodec','png','-framerate','60','-i','-','-an','-c:v','libx264','-crf','18','-preset','medium','-pix_fmt','yuv420p','-movflags','+faststart',str(R/name)],stdin=subprocess.PIPE,stdout=log,stderr=log)
            for i in range(330):
                data=page.evaluate('(t)=>{window.drawAt(t);return document.getElementById("stage").toDataURL().split(",")[1]}',i/60)
                enc.stdin.write(base64.b64decode(data))
                if detail and i in [0,68,80,110,180,300]:(R/f'Frame_{i:03}.png').write_bytes(base64.b64decode(data))
                if i%120==0:print(name,i,'/330',flush=True)
            enc.stdin.close()
            if enc.wait():raise RuntimeError('Encoding failed: '+name)
    browser.close()
print('Saved close-up and full-room animation videos.',flush=True)

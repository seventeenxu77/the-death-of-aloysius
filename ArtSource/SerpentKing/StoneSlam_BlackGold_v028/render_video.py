"""Production render of the authored bone animation; no game/editor tests."""
from pathlib import Path
import base64,subprocess,shutil
from playwright.sync_api import sync_playwright
root=Path(__file__).resolve().parent
fps,duration=60,6
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True,args=['--use-angle=swiftshader','--enable-webgl'])
    page=browser.new_page(viewport={'width':1400,'height':1300},device_scale_factor=1)
    page.goto((root/'BreakerSlam.html').as_uri()+'?render=1')
    page.wait_for_function('window.ready === true')
    # Binding diagram is a deliverable, not a visual QA screenshot.
    data=page.evaluate('()=>{window.rigSettings.bones=true;window.rigSettings.still=true;window.drawAt(0);return document.getElementById("stage").toDataURL().split(",")[1]}')
    (root/'BreakerSlam_Binding.png').write_bytes(base64.b64decode(data))
    page.evaluate('()=>{window.rigSettings.still=false;window.rigSettings.bones=false;}')
    for name,t in [('Idle',0),('Windup',1.2),('Shake',2.12),('Impact',2.5)]:
        data=page.evaluate('(t)=>{window.drawAt(t);return document.getElementById("stage").toDataURL().split(",")[1]}',t)
        (root/(name+'.png')).write_bytes(base64.b64decode(data))
    with (root/'encode.log').open('w',encoding='utf-8') as log:
        encoder=subprocess.Popen([shutil.which('ffmpeg'),'-y','-f','image2pipe','-vcodec','png','-framerate',str(fps),'-i','-',
            '-an','-c:v','libx264','-crf','18','-preset','medium','-pix_fmt','yuv420p','-movflags','+faststart',str(root/'BreakerSlam.mp4')],stdin=subprocess.PIPE,stdout=log,stderr=log)
        for i in range(fps*duration):
            data=page.evaluate('(t)=>{window.drawAt(t);return document.getElementById("stage").toDataURL().split(",")[1]}',i/fps)
            encoder.stdin.write(base64.b64decode(data))
            if i%60==0:print(f'Rendering skeletal animation {i}/{fps*duration}',flush=True)
        encoder.stdin.close();result=encoder.wait()
    browser.close()
    if result:raise RuntimeError('Video encoding failed; see encode.log')
print('Saved skeletal MP4 and binding diagram.',flush=True)

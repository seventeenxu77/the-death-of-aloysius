from pathlib import Path
import base64,subprocess,shutil
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parent
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True,args=['--use-angle=swiftshader','--enable-webgl'])
    page=browser.new_page(viewport={'width':1800,'height':1300})
    page.goto((R/'四头蛇_贝塞尔编辑器.html').as_uri()+'?render=1')
    page.wait_for_function('window.ready===true',timeout=60000)
    page.evaluate("project=blank();$('isolate').checked=false;$('bed').checked=true;$('showCurve').checked=false;playing=true;scrubbing=false;")
    with (R/'video_encode.log').open('w') as log:
        enc=subprocess.Popen([shutil.which('ffmpeg'),'-y','-f','image2pipe','-vcodec','png','-framerate','60','-i','-','-an','-c:v','libx264','-crf','18','-preset','fast','-pix_fmt','yuv420p','-movflags','+faststart',str(R/'BezierSway_Together.mp4')],stdin=subprocess.PIPE,stdout=log,stderr=log)
        for i in range(500):
            data=page.evaluate("t=>{cycle=t*project.frequency;draw();return canvas.toDataURL('image/png').split(',')[1]}",i/60)
            png=base64.b64decode(data)
            enc.stdin.write(png)
            if i==0:(R/'Poster.png').write_bytes(png)
            if i%120==0:print(f'Exported {i}/500 frames',flush=True)
        enc.stdin.close()
        if enc.wait():raise RuntimeError('Video encoding failed')
    browser.close()
print('Saved BezierSway_Together.mp4: 3 cycles, 60 fps, current Bezier presets.',flush=True)

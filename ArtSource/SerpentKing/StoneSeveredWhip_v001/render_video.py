from pathlib import Path
import base64, json, shutil, subprocess
from playwright.sync_api import sync_playwright

R=Path(__file__).resolve().parent
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True,args=['--use-angle=swiftshader','--enable-webgl'])
    page=browser.new_page(viewport={'width':1320,'height':1440})
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.goto((R/'断口鞭状摆动预览.html').as_uri()+'?render=1')
    page.wait_for_function('window.ready===true',timeout=60000)
    bounds=page.evaluate('''()=>{let bounds=[Infinity,Infinity,-Infinity,-Infinity];for(let i=0;i<480;i++){const s=solve(i/60);for(let k=0;k<rig.vertices.length;k++){const [b,w]=rig.binding[k],v=rig.vertices[k],a=transform(v,s,b),c=transform(v,s,b+1),p=view([a[0]+(c[0]-a[0])*w,a[1]+(c[1]-a[1])*w]);bounds[0]=Math.min(bounds[0],p[0]);bounds[1]=Math.min(bounds[1],p[1]);bounds[2]=Math.max(bounds[2],p[0]);bounds[3]=Math.max(bounds[3],p[1])}}return bounds}''')
    print('Animation canvas bounds:',bounds,flush=True)
    (R/'MotionBounds.json').write_text(json.dumps({'canvas':[1152,1400],'bodyBounds':bounds,'fixedRoot':[576,1295],'joints':65},indent=2),'utf8')
    with (R/'encode.log').open('w') as log:
        encoder=subprocess.Popen([shutil.which('ffmpeg'),'-y','-f','image2pipe','-vcodec','png','-framerate','60','-i','-','-an','-c:v','libx264','-crf','18','-preset','fast','-pix_fmt','yuv420p','-movflags','+faststart',str(R/'巨石断口_鞭状摆动.mp4')],stdin=subprocess.PIPE,stdout=log,stderr=log)
        try:
            for i in range(480):
                frame=page.evaluate("t=>{renderAt(t);return canvas.toDataURL('image/png').split(',')[1]}",i/60)
                data=base64.b64decode(frame);encoder.stdin.write(data)
                if i in (0,45,90,135,180,240):(R/f'Frame_{i:03}.png').write_bytes(data)
                if i%120==0:print(f'Exported {i}/480 frames',flush=True)
        finally:
            encoder.stdin.close();code=encoder.wait()
        if code:raise RuntimeError('Video export failed')
    page.evaluate("document.getElementById('bones').checked=true;renderAt(.75)")
    (R/'骨骼链预览.png').write_bytes(base64.b64decode(page.evaluate("canvas.toDataURL('image/png').split(',')[1]")))
    browser.close()
    if errors:raise RuntimeError(errors)
print('Saved native skin animation preview: 8 s, 60 fps. Unity untouched.',flush=True)

from pathlib import Path
import base64,subprocess,shutil
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parent
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True,args=['--use-angle=swiftshader','--enable-webgl'])
    page=browser.new_page(viewport={'width':1800,'height':1400})
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.goto((R/'四头蛇_贝塞尔编辑器.html').as_uri()+'?render=1')
    page.wait_for_function('window.ready===true',timeout=60000)
    page.evaluate("project=blank();$('isolate').checked=false;$('bed').checked=true;$('showCurve').checked=false;playing=true;scrubbing=false;window.detailCanvas=document.createElement('canvas');detailCanvas.width=1536;detailCanvas.height=1024;window.detailCtx=detailCanvas.getContext('2d');")
    evaluate=r'''t=>{
      cycle=t*project.frequency;detailTime=t;draw();
      const main=canvas.toDataURL('image/png').split(',')[1];
      detailCtx.fillStyle='#150f18';detailCtx.fillRect(0,0,1536,1024);
      const names=['Stone','Fang','Venom','Time'],labels=['巨石 · 眨眼 / 沉重点头','突刺 · 快速吐信 / 短促颤动','毒液 · 毒囊交错鼓动','时间 · 慢眨眼 / 轻微颔首'];
      const rois={Stone:[330,170,340,300],Fang:[1000,270,255,280],Venom:[780,60,255,230],Time:[620,260,340,300]},base=activeHeads();
      names.forEach((name,index)=>{
        const p=rig.parts.find(p=>p.name===name),a=arc(base[name]),end=a.points.at(-1),origin=p.points.at(-1),angle=end[2]-p.referenceEndAngle+a.h.angle*Math.PI/180,c=Math.cos(angle),s=Math.sin(angle),r=rois[name];
        const corners=[[r[0],r[1]],[r[0]+r[2],r[1]],[r[0],r[1]+r[3]],[r[0]+r[2],r[1]+r[3]]].map(v=>[end[0]+c*(v[0]-origin[0])-s*(v[1]-origin[1]),end[1]+s*(v[0]-origin[0])+c*(v[1]-origin[1])+192]);
        const sx=Math.min(...corners.map(v=>v[0]))-12,sy=Math.min(...corners.map(v=>v[1]))-12,sw=Math.max(...corners.map(v=>v[0]))-sx+12,sh=Math.max(...corners.map(v=>v[1]))-sy+12;
        const col=index%2,row=Math.floor(index/2),scale=Math.min(768/sw,474/sh),dw=sw*scale,dh=sh*scale;
        detailCtx.drawImage(canvas,sx,sy,sw,sh,col*768+(768-dw)/2,row*512+32+(476-dh)/2,dw,dh);
        detailCtx.fillStyle='#d7b16d';detailCtx.font='22px Microsoft YaHei';detailCtx.fillText(labels[index],col*768+20,row*512+26);
      });
      return {main,close:detailCanvas.toDataURL('image/png').split(',')[1]};
    }'''
    with (R/'video_encode.log').open('w') as log:
        def encoder(name):
            return subprocess.Popen([shutil.which('ffmpeg'),'-y','-f','image2pipe','-vcodec','png','-framerate','60','-i','-','-an','-c:v','libx264','-crf','18','-preset','fast','-pix_fmt','yuv420p','-movflags','+faststart',str(R/name)],stdin=subprocess.PIPE,stdout=log,stderr=log)
        main=encoder('SerpentLife_1p35Hz.mp4');close=encoder('SerpentLife_Closeup_1p35Hz.mp4')
        try:
            for i in range(500):
                frame=page.evaluate(evaluate,i/60)
                main_png=base64.b64decode(frame['main']);close_png=base64.b64decode(frame['close'])
                main.stdin.write(main_png);close.stdin.write(close_png)
                if i in (69,98,151,274):(R/('Detail_'+str(i).zfill(3)+'.png')).write_bytes(close_png)
                if i==0:(R/'Poster.png').write_bytes(main_png)
                if i%120==0:print(f'Exported {i}/500 frames (main + closeups)',flush=True)
        finally:
            main.stdin.close();close.stdin.close()
            codes=[main.wait(),close.wait()]
        if any(codes):raise RuntimeError('Video encode failed')
    browser.close()
    if errors:raise RuntimeError(errors)
print('Saved full-scene and closeup videos: 8.33 s, 60 fps, four distinct detail performances.',flush=True)

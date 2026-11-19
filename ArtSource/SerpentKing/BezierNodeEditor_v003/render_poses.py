from pathlib import Path
import base64
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parent
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True,args=['--use-angle=swiftshader','--enable-webgl'])
    page=browser.new_page(viewport={'width':1800,'height':1300})
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    page.goto((R/'四头蛇_贝塞尔编辑器.html').as_uri()+'?render=1')
    page.wait_for_function('window.ready===true',timeout=60000)
    page.evaluate("localStorage.removeItem(STORE);project=blank();$('bed').checked=false;$('showCurve').checked=false;window.review=document.createElement('canvas');review.width=1536;review.height=1440;window.reviewCtx=review.getContext('2d');reviewCtx.fillStyle='#24121e';reviewCtx.fillRect(0,0,1536,1440)")
    for row,name in enumerate(['Stone','Venom','Fang','Time']):
        for i in range(3):
            page.evaluate("([name,i,row])=>{head=name;selectPose(i);reviewCtx.drawImage(canvas,i*512,row*360,512,341.33);reviewCtx.fillStyle='#e4c58b';reviewCtx.font='16px sans-serif';reviewCtx.fillText(CN[name]+' / '+POSES[i],i*512+15,row*360+355)}",[name,i,row])
            if name=='Stone':
                data=page.evaluate("document.getElementById('stage').toDataURL().split(',')[1]")
                (R/('Stone_'+str(i)+'.png')).write_bytes(base64.b64decode(data))
    data=page.evaluate("review.toDataURL().split(',')[1]");(R/'SkinReview.png').write_bytes(base64.b64decode(data))
    browser.close()
    if errors:raise RuntimeError(errors)
print('Rendered the 3 authored poses for all 4 independent skins; no Unity actions.')

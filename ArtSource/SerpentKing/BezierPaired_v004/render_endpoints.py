from pathlib import Path
import base64
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parent
with sync_playwright() as p:
    b=p.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True,args=['--use-angle=swiftshader','--enable-webgl'])
    page=b.new_page(viewport={'width':1800,'height':1300})
    page.goto((R/'四头蛇_贝塞尔编辑器.html').as_uri()+'?render=1');page.wait_for_function('window.ready===true',timeout=60000)
    page.evaluate("project=blank();$('isolate').checked=false;$('bed').checked=true;$('showCurve').checked=false;playing=true;")
    for name,value in [('Endpoint_A.png',0),('Middle.png',.25),('Endpoint_B.png',.5)]:
        data=page.evaluate("v=>{cycle=v;draw();return canvas.toDataURL().split(',')[1]}",value)
        (R/name).write_bytes(base64.b64decode(data))
    b.close()
print('Rendered paired endpoint compositions.')

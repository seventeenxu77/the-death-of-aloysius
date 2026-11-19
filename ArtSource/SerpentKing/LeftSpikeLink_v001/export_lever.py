"""Export the existing approved lever motion, including the reverse switch stroke."""
from pathlib import Path
import json
from playwright.sync_api import sync_playwright
R=Path(__file__).resolve().parent
ROOT=R.parents[2]
SOURCE=ROOT/'ArtSource/GothicStage/ActionStudies_v001'
OUT=ROOT/'Assets/Art/SerpentKing/LeftSpikeLink_v001'
OUT.mkdir(parents=True,exist_ok=True)
with sync_playwright() as p:
    browser=p.chromium.launch(executable_path=r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',headless=True)
    page=browser.new_page()
    page.goto((SOURCE/'MotionPreview.html').as_uri()+'?render=1')
    result=page.evaluate('''()=>{
      const off=eval('('+lever.toString().replace('push=range(t,1.3,1.96)','push=1-range(t,1.3,1.96)')+')');
      const onFrames=[],offFrames=[],onAngles=[],offAngles=[];
      for(let i=0;i<=189;i++){const a=lever(i/60),b=off(i/60);onFrames.push(a.pose);offFrames.push(b.pose);onAngles.push(a.theta);offAngles.push(b.theta)}
      return {fps:60,duration:3.15,bones:RIG.bones.map(b=>b.name),onFrames,offFrames,onAngles,offAngles};
    }''')
    browser.close()
(OUT/'LeverMotion.json').write_text(json.dumps(result,separators=(',',':')),'utf8')
print('Exported existing lever action: on/off, 190 frames x',len(result['bones']),'bones. No Unity playback.')

from pathlib import Path
import json,base64
R=Path(__file__).resolve().parent
S=R.parent/'BlackGoldWake_v026'
source=(S/'animation.js').read_text('utf8')
prefix=source[:source.index('function pose(t){')]
draw=source[source.index('function drawSkin('):source.index('function bones(')]
draw=draw.replace('const closed=isBody?0:1-smooth((t-p.start-p.duration-p.eyeDelay)/p.eyeDuration);','const closed=0;')
assets={p.name:'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode() for p in (S/'Layers').glob('*.png')}
html=(R/'template.html').read_text('utf8')
js=prefix+'\n'+draw+'\n'+(R/'editor.js').read_text('utf8')
html=html.replace('__RIG__',(S/'WakeRig.json').read_text('utf8')).replace('__ASSETS__',json.dumps(assets)).replace('__JS__',js)
(R/'四头蛇_节点编辑器.html').write_text(html,'utf8')
print('Created self-contained bone-node HTML editor.')

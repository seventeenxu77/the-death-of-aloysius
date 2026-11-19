from pathlib import Path
import json,base64
root=Path(__file__).resolve().parent
source=root.parent/'Bride2DRig'
rig=json.loads((source/'rig.json').read_text(encoding='utf-8'))
for part in rig['parts']:
    part['data']='data:image/png;base64,'+base64.b64encode((source/part['image']).read_bytes()).decode()
html=(root/'preview.template.html').read_text(encoding='utf-8')
html=html.replace('/*RIG*/', 'const RIG='+json.dumps(rig,separators=(',',':'))+';')
html=html.replace('/*MOTION*/',(root/'motion.js').read_text(encoding='utf-8'))
(root/'MotionPreview.html').write_text(html,encoding='utf-8')
print('Packaged offline skeletal preview.')

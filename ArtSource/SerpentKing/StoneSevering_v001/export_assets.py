"""Package the approved continuous-chain preview for native Unity playback."""
from pathlib import Path
import json,shutil
R=Path(__file__).resolve().parent
ROOT=R.parents[2]
S=ROOT/'ArtSource/SerpentKing/StoneSeveredWhip_v001'
OUT=ROOT/'Assets/Art/SerpentKing/StoneSevering_v001';OUT.mkdir(parents=True,exist_ok=True)
d=json.loads((S/'WhipRig.json').read_text('utf8'))
for k in ['chain','vertices','binding']:d[k]=[v for pair in d[k] for v in pair]
(OUT/'WhipRig.json').write_text(json.dumps(d,separators=(',',':')),'utf8')
shutil.copy2(S/'SeveredNeck.png',OUT/'SeveredNeck.png')
print('Packaged approved 65-node whip, original texture unchanged:',len(d['chain'])//2,'nodes;',len(d['vertices'])//2,'vertices')

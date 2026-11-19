"""Export the existing approved lever motion, including the reverse switch stroke."""
from pathlib import Path
import json
R=Path(__file__).resolve().parent
ROOT=R.parents[2]
SOURCE=ROOT/'ArtSource/GothicStage/ActionStudies_v001'
OUT=ROOT/'Assets/Art/SerpentKing/LeftSpikeLink_v001'
OUT.mkdir(parents=True,exist_ok=True)
result=json.loads((R/'LeverMotion.json').read_text('utf8'))
(OUT/'LeverMotion.json').write_text(json.dumps(result,separators=(',',':')),'utf8')
print('Exported existing lever action: on/off, 190 frames x',len(result['bones']),'bones. No Unity playback.')

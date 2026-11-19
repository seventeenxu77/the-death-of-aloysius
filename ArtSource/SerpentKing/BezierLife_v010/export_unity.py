"""Export the preserved native source bindings and three poses without a webpage."""
from pathlib import Path
import json, math, re, hashlib

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OUT = ROOT / 'Assets/Art/SerpentKing/BezierLife_v010'
OUT.mkdir(parents=True, exist_ok=True)
source = (HERE / 'SourceRig.json').read_text('utf8')
snapshot = json.loads(source)
rig = snapshot['RIG']
legacy = json.loads((HERE.parent / 'BlackGoldWake_v026/WakeRig.json').read_text('utf8'))

def preset(p, index):
    r, e = p['points']
    end = [e[0], max(r[1]-470, e[1]-p['artTop']+25)]
    height, side = r[1]-end[1], index-1
    lower = [r[0]+math.cos(p['referenceStartAngle'])*100, r[1]+math.sin(p['referenceStartAngle'])*100]
    if side == 0:
        nodes = [r[:], lower, [(r[0]+end[0])*.5, r[1]-height*.51], [0,0], end]
    else:
        nodes = [r[:], lower, [r[0]+side*180, r[1]-height*.40], [0,0], [end[0]+side*135,end[1]+115]]
    length = max(85,height*.24)
    nodes[3] = [nodes[4][0]-math.cos(p['referenceEndAngle'])*length, nodes[4][1]-math.sin(p['referenceEndAngle'])*length]
    angle = 0
    if p['name'] == 'Venom':
        before = math.atan2(nodes[4][1]-nodes[3][1], nodes[4][0]-nodes[3][0])
        scale = (nodes[0][1]-nodes[4][1]+200)/max(1,nodes[0][1]-nodes[4][1])
        for n in nodes[1:]: n[1] = nodes[0][1]+(n[1]-nodes[0][1])*scale
        after = math.atan2(nodes[4][1]-nodes[3][1],nodes[4][0]-nodes[3][0])
        angle = (before-after)*180/math.pi
    return {'nodes':sum(nodes, []), 'angle':angle}

parts = []
for p in rig['parts']:
    m = next(m for m in rig['meshes'] if m['name']==p['name'])
    old = next(h for h in legacy['parts'] if h['name']==p['name'])
    pairs, weights = [], []
    for x,y in m['vertices']:
        if y < old['rigidY']:
            k, w = len(old['points'])-1, 0
            pairs.extend([k,k]);weights.extend([1,0])
        else:
            k = next((k for k,(a,b) in enumerate(zip(old['points'],old['points'][1:])) if y>=b[1]),len(old['points'])-2)
            a,b = old['points'][k:k+2]
            w = max(0,min(1,(a[1]-y)/(a[1]-b[1])))
            pairs.extend([k,k+1]);weights.extend([1-w,w])
    parts.append(dict(name=p['name'],origin=p['points'][-1],referenceEndAngle=p['referenceEndAngle'],
        neckRadius=p['neckRadius'],eyes=sum(p['eyes'],[]),vertices=sum(m['vertices'],[]),
        uv=sum(m['uv'],[]),indices=m['indices'],binding=m['binding'],
        poses=[preset(p,i) for i in range(3)],legacyPoints=sum(old['points'],[]),legacyJoints=pairs,legacyWeights=weights))
data=dict(version=10,frequency=1.35,parts=parts,tongueGold=snapshot['tongueGold'])
target=OUT/'BezierLife.json'
target.write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),'utf8')
print('Exported approved three-pose spline rig:',target)
print('Heads:',len(parts),'vertices:',sum(len(p['vertices'])//2 for p in parts))
print('Source data SHA256:',hashlib.sha256(source.encode('utf8')).hexdigest())

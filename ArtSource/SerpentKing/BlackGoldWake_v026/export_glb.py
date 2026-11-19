"""Portable editable 2D skinned glTF. Source PNGs are embedded unchanged.

46 articulated joints; rigid-skull eye overlays change only after arrival.
The browser is the reference renderer; glTF uses STEP open/closed eyes.
"""
from pathlib import Path
import json,struct,math
R=Path(__file__).resolve().parent
rig=json.loads((R/'WakeRig.json').read_text('utf-8'))
frames=json.loads((R/'WakeBoneKeys.json').read_text('utf-8'))['frames']
g=dict(asset=dict(version='2.0',generator='BlackGoldWake_v026'),scene=0,scenes=[dict(name='BlackGoldWake',nodes=[])],nodes=[],meshes=[],skins=[],accessors=[],bufferViews=[],buffers=[],animations=[],extensionsUsed=['KHR_materials_unlit'],materials=[],samplers=[dict(magFilter=9729,minFilter=9729,wrapS=33071,wrapT=33071)],textures=[],images=[])
blob=bytearray();materials={}
def view(data,target=None):
 while len(blob)%4:blob.append(0)
 v=dict(buffer=0,byteOffset=len(blob),byteLength=len(data));blob.extend(data)
 if target:v['target']=target
 g['bufferViews'].append(v);return len(g['bufferViews'])-1
def acc(data,n,kind='f',target=None):
 flat=[c for row in data for c in row] if n>1 else data
 a=dict(bufferView=view(struct.pack('<'+kind*len(flat),*flat),target),componentType=5126 if kind=='f' else 5123,count=len(data),type={1:'SCALAR',2:'VEC2',3:'VEC3',4:'VEC4',16:'MAT4'}[n])
 if n in (1,3):
  rows=data if n>1 else [[v] for v in data];a['min']=[min(row[k] for row in rows) for k in range(n)];a['max']=[max(row[k] for row in rows) for k in range(n)]
 g['accessors'].append(a);return len(g['accessors'])-1
def material(name):
 if name in materials:return materials[name]
 i=len(g['images']);g['images'].append(dict(name=name,bufferView=view((R/'Layers'/name).read_bytes()),mimeType='image/png'))
 g['textures'].append(dict(source=i,sampler=0));m=len(g['materials']);g['materials'].append(dict(name=name,doubleSided=True,alphaMode='MASK',alphaCutoff=.35,pbrMetallicRoughness=dict(baseColorTexture=dict(index=i),metallicFactor=0,roughnessFactor=1),extensions={'KHR_materials_unlit':{}}));materials[name]=m;return m
def node(name,mesh,skin=False,parent=None,translation=None):
 n=dict(name=name,mesh=mesh)
 if skin:n['skin']=0
 if translation:n['translation']=translation
 i=len(g['nodes']);g['nodes'].append(n)
 if parent is None:g['scenes'][0]['nodes'].append(i)
 else:g['nodes'][parent].setdefault('children',[]).append(i)
 return i
for i,j in enumerate(rig['joints']):
 g['nodes'].append(dict(name=j['name'],translation=[j['local'][0]/100,-j['local'][1]/100,0]))
 if j['parent']<0:g['scenes'][0]['nodes'].append(i)
 else:g['nodes'][j['parent']].setdefault('children',[]).append(i)
inverse=[]
for j in rig['joints']:
 x,y=j['rest'];inverse.append([1,0,0,0,0,1,0,0,0,0,1,0,-x/100,y/100,0,1])
g['skins'].append(dict(name='FourIndependentNecks_And_SharedCoils',joints=list(range(46)),inverseBindMatrices=acc(inverse,16)))
def mesh(name,pos,uv,idx,mat,joints=None,weights=None):
 attrs=dict(POSITION=acc(pos,3,target=34962),TEXCOORD_0=acc(uv,2,target=34962))
 if joints is not None:attrs.update(JOINTS_0=acc([j+[0,0] for j in joints],4,'H',34962),WEIGHTS_0=acc([w+[0.,0.] for w in weights],4,target=34962))
 g['meshes'].append(dict(name=name,primitives=[dict(attributes=attrs,indices=acc(idx,1,'H',34963),material=mat,mode=4)]));return len(g['meshes'])-1
def quad(name,z):
 mi=mesh(name,[[0,0,z],[15.36,0,z],[0,-10.24,z],[15.36,-10.24,z]],[[0,0],[1,0],[0,1],[1,1]],[0,1,2,1,3,2],material(name));node(name,mi)
quad('EmptyBed.png',-.1)
layer={'Coils':0,'Venom':.01,'Stone':.02,'Fang':.03,'Time':.04}
for m in rig['meshes']:
 idx=[]
 for i in range(0,len(m['indices']),3):
  tri=m['indices'][i:i+3]
  if m['name']!='Coils' and min(m['vertices'][v][1] for v in tri)>=848:continue
  idx.extend(tri)
 mi=mesh(m['name'],[[x/100,-y/100,layer[m['name']]] for x,y in m['vertices']],m['uv'],idx,material('Coils.png' if m['name']=='Coils' else m['name']+'_Open.png'),m['joints'],m['weights']);node(m['name'],mi,True)
 # Front part of the SAME shared coil skin provides occlusion.
 if m['name']=='Coils':
  idx=[]
  for i in range(0,len(m['indices']),3):
   tri=m['indices'][i:i+3];x=sum(m['vertices'][v][0] for v in tri)/3;y=sum(m['vertices'][v][1] for v in tri)/3
   if y>=785:idx.extend(tri)
  mi=mesh('Coils_Foreground',[[x/100,-y/100,.08] for x,y in m['vertices']],m['uv'],idx,material('Coils.png'),m['joints'],m['weights']);node('Coils_Foreground',mi,True)
quad('BedForeground.png',.1)
animation=dict(name='Wake_20s_Fang_Venom_Stone_Time',samplers=[],channels=[])
times=acc([f['time'] for f in frames],1)
def channel(node,path,out,time=times,interp='LINEAR'):
 animation['samplers'].append(dict(input=time,output=out,interpolation=interp));animation['channels'].append(dict(sampler=len(animation['samplers'])-1,target=dict(node=node,path=path)))
for i,j in enumerate(rig['joints']):
 channel(i,'translation',acc([[f['bones'][i]['x']/100,-f['bones'][i]['y']/100,0] for f in frames],3))
 # Current pose keeps orientation fixed, but the animation preserves a rotation channel for editing.
 channel(i,'rotation',acc([[0,0,math.sin(-f['bones'][i]['angle']/2),math.cos(f['bones'][i]['angle']/2)] for f in frames],4))
for p in rig['parts']:
 skull=p['jointIds'][-1];rx,ry=rig['joints'][skull]['rest']
 for e,(x,y,w,h) in enumerate(p['eyes']):
  positions=[[0,0,0]];uv=[[x/1536,y/1024]];indices=[]
  for k in range(33):
   a=2*math.pi*k/32;dx=math.cos(a)*w;dy=math.sin(a)*h
   positions.append([dx/100,-dy/100,0]);uv.append([(x+dx)/1536,(y+dy)/1024])
   if k:indices.extend([0,k,k+1])
  mi=mesh(p['name']+'_ClosedEye_'+str(e),positions,uv,indices,material(p['name']+'_Closed.png'))
  ni=node(p['name']+'_EyeFrame_'+str(e),mi,parent=skull,translation=[(x-rx)/100,-(y-ry)/100,layer[p['name']]+.002])
  opened=p['start']+p['duration']+p['eyeDelay']+p['eyeDuration']
  channel(ni,'scale',acc([[1,1,1],[0,0,0],[0,0,0]],3),acc([0,opened,20],1),'STEP')
g['animations'].append(animation);g['buffers']=[dict(byteLength=len(blob))]
raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*((-len(raw))%4);blob+=b'\0'*((-len(blob))%4)
out=struct.pack('<4sII',b'glTF',2,12+8+len(raw)+8+len(blob))+struct.pack('<I4s',len(raw),b'JSON')+raw+struct.pack('<I4s',len(blob),b'BIN\0')+blob
(R/'BlackGoldWake_Skeletal.glb').write_bytes(out)
print('Exported editable glTF: 46 bones, 5 skins plus front render pass, static bed, closed-eye attachments, 20-second animation.')

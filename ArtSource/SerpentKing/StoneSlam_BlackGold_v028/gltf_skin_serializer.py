# Standard glTF: 29 joint nodes, inverse bind matrices, JOINTS_0 / WEIGHTS_0,
# 9 independent meshes, PNG alpha, and one 12-second local-rotation animation.
g=dict(asset=dict(version='2.0',generator='SerpentKing Skeletal v011'),scene=0,
       scenes=[dict(name='SerpentKing_2D_Rig',nodes=[0])],nodes=[],meshes=[],skins=[],
       accessors=[],bufferViews=[],buffers=[],animations=[],
       extensionsUsed=['KHR_materials_unlit'],
       materials=[dict(name='DarkSilhouette_Transparent',doubleSided=True,alphaMode='BLEND',
        pbrMetallicRoughness=dict(baseColorTexture=dict(index=0),metallicFactor=0,roughnessFactor=1),
        extensions={'KHR_materials_unlit':{}})],
       samplers=[dict(magFilter=9729,minFilter=9729,wrapS=33071,wrapT=33071)],
       textures=[dict(source=0,sampler=0)],images=[])
blob=bytearray()
def add_view(data,target=None):
    while len(blob)%4:blob.append(0)
    start=len(blob);blob.extend(data)
    v=dict(buffer=0,byteOffset=start,byteLength=len(data))
    if target:v['target']=target
    g['bufferViews'].append(v);return len(g['bufferViews'])-1
def accessor(data,components,kind='f',type_=None,target=None,bounds=False):
    flat=[v for row in data for v in row] if components>1 else list(data)
    raw=struct.pack('<'+kind*len(flat),*flat)
    a=dict(bufferView=add_view(raw,target),componentType={'f':5126,'H':5123}[kind],count=len(data),
           type=type_ or {1:'SCALAR',2:'VEC2',3:'VEC3',4:'VEC4',16:'MAT4'}[components])
    if bounds:
        rows=data if components>1 else [[x] for x in data]
        a['min']=[min(row[i] for row in rows) for i in range(components)]
        a['max']=[max(row[i] for row in rows) for i in range(components)]
    g['accessors'].append(a);return len(g['accessors'])-1
def point(v,z=0):return [v[0]/100,-v[1]/100,z]

for i,j in enumerate(rig['joints']):
    g['nodes'].append(dict(name=j['name'],translation=point(j['local']),children=[]))
    if i:g['nodes'][j['parent']]['children'].append(i)
inverse=[]
for j in rig['joints']:
    x,y,z=point(j['rest']);inverse.append([1,0,0,0,0,1,0,0,0,0,1,0,-x,-y,-z,1])
g['skins'].append(dict(name='SerpentKing_Skeleton',joints=list(range(len(rig['joints']))),skeleton=0,
                      inverseBindMatrices=accessor(inverse,16)))
for m in rig['meshes']:
    pos=[point(v,m['layer']*.01) for v in m['vertices']]
    attr=dict(POSITION=accessor(pos,3,target=34962,bounds=True),
              TEXCOORD_0=accessor(m['uv'],2,target=34962),
              JOINTS_0=accessor([j+[0,0] for j in m['joints']],4,'H',target=34962),
              WEIGHTS_0=accessor([w+[0.,0.] for w in m['weights']],4,target=34962))
    g['meshes'].append(dict(name=m['name'],primitives=[dict(attributes=attr,indices=accessor(m['indices'],1,'H',target=34963),material=0,mode=4)]))
    node=len(g['nodes']);g['nodes'].append(dict(name=m['name'],mesh=len(g['meshes'])-1,skin=0))
    g['nodes'][0]['children'].append(node)
time=accessor([i/FPS for i in range(len(rig['frames']))],1,bounds=True)
animation=dict(name='Coil_Preload_Snap_Recover',samplers=[],channels=[])
for ji in range(1,len(rig['joints'])):
    quats=[[0.,0.,math.sin(-row[ji]/2),math.cos(row[ji]/2)] for row in rig['frames']]
    output=accessor(quats,4)
    animation['samplers'].append(dict(input=time,output=output,interpolation='LINEAR'))
    animation['channels'].append(dict(sampler=len(animation['samplers'])-1,target=dict(node=ji,path='rotation')))
g['animations'].append(animation)
g['images'].append(dict(bufferView=add_view((ROOT/'SerpentKing_Atlas.png').read_bytes()),mimeType='image/png'))
g['buffers']=[dict(byteLength=len(blob))]
raw=json.dumps(g,separators=(',',':')).encode()
raw+=b' '*((-len(raw))%4);blob+=b'\0'*((-len(blob))%4)
glb=struct.pack('<4sII',b'glTF',2,12+8+len(raw)+8+len(blob))+struct.pack('<I4s',len(raw),b'JSON')+raw+struct.pack('<I4s',len(blob),b'BIN\0')+blob
(ROOT/'SerpentKing_Skeletal.glb').write_bytes(glb)


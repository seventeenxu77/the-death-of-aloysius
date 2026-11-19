"""Author the downward breaker's skeletal attack, plus a standard skinned GLB."""
from pathlib import Path
import math,json,struct,base64
ROOT=Path(__file__).resolve().parent
W,H,FPS,DURATION=1280,1280,60,6
FLOOR,IMPACT=1040,2.50
SCALE=.72
SOURCE_LINE=[[580,0],[570,160],[540,320],[480,480],[435,650],[400,820]]
ROOT_POS=[800,-190]
CONTACT=[405,1228]
TARGET_X=ROOT_POS[0]+(CONTACT[0]-580)*SCALE
IDLE_Y=ROOT_POS[1]+CONTACT[1]*SCALE
def smooth(x):
    x=max(0,min(1,x));return x*x*(3-2*x)
def mix(a,b,t):return a+(b-a)*t
rig=dict(width=W,height=H,atlas='Stone_Open.png',duration=DURATION,fps=FPS,impact=IMPACT,floor=FLOOR,contactX=TARGET_X,
    joints=[dict(name='SceneRoot',parent=-1,rest=[0,0],local=[0,0])],meshes=[],frames=[],rootFrames=[],parts=[])
ids=[]
for i,(x,y) in enumerate(SOURCE_LINE):
    pos=[ROOT_POS[0]+(x-580)*SCALE,ROOT_POS[1]+y*SCALE]
    parent=ids[-1] if i else 0;prev=rig['joints'][parent]['rest']
    ids.append(len(rig['joints']))
    rig['joints'].append(dict(name='Breaker_Head' if i==5 else f'Breaker_Neck_{i+1:02}',parent=parent,
                            rest=pos,local=[pos[0]-prev[0],pos[1]-prev[1]]))
rig['parts']=[dict(name='Breaker',label='巨石蛇头',jointIds=ids)]
def weight(y,head):
    if head:return [ids[-1],ids[-1]],[1.,0.]
    for i in range(5):
        a,b=SOURCE_LINE[i][1],SOURCE_LINE[i+1][1]
        if a<=y<=b:
            w=smooth((y-a)/(b-a));return [ids[i],ids[i+1]],[1-w,w]
    return [ids[-1],ids[-1]],[1.,0.]
for name,lo,hi,head in [('Neck',0,820,False),('Head',820,1536,True)]:
    ys=sorted(set([lo,hi]+list(range(lo,hi,14))+[p[1] for p in SOURCE_LINE if lo<p[1]<hi]))
    m=dict(name='Breaker_'+name,layer=1,vertices=[],uv=[],joints=[],weights=[],indices=[])
    cols=30
    for y in ys:
        for c in range(cols+1):
            x=c/cols*1024
            m['vertices'].append([ROOT_POS[0]+(x-580)*SCALE,ROOT_POS[1]+y*SCALE]);m['uv'].append([x/1024,y/1536])
            j,w=weight(y,head);m['joints'].append(j);m['weights'].append(w)
    for r in range(len(ys)-1):
        for c in range(cols):
            a=r*(cols+1)+c;b=a+1;d=a+cols+1;e=d+1;m['indices'].extend([a,d,b,b,d,e])
    rig['meshes'].append(m)

def attack(t):
    t=t%DURATION;shake=0.;bend=0.;roll=0.
    if t<.45:y=IDLE_Y
    elif t<1.25:
        u=smooth((t-.45)/.80);y=IDLE_Y-95*u;bend=u
    elif t<2.28:
        u=(t-1.25)/1.03;env=math.sin(math.pi*u)**.5
        shake=math.sin(2*math.pi*19*(t-1.25))*6*env
        y=IDLE_Y-95+math.sin(2*math.pi*23*(t-1.25))*3*env;bend=1
        roll=.009*math.sin(2*math.pi*17*(t-1.25))*env
    elif t<IMPACT:
        u=(t-2.28)/.22;y=mix(IDLE_Y-95,FLOOR,u*u*u);bend=1-u
    elif t<2.60:y=FLOOR;bend=.7*smooth((t-IMPACT)/.10)
    elif t<2.80:
        u=(t-2.60)/.20;y=FLOOR-42*(1-(1-u)**3);bend=.7*(1-u)
    elif t<3.10:y=mix(FLOOR-42,FLOOR-22,smooth((t-2.8)/.30))
    elif t<3.35:y=FLOOR-22
    elif t<4.95:y=mix(FLOOR-22,IDLE_Y,smooth((t-3.35)/1.6))
    else:y=IDLE_Y
    angles=[0.]*7
    for i,v in enumerate([.035,-.09,.10,-.07,.025]):angles[ids[i]]=v*bend
    angles[ids[-1]]=-sum(angles)+roll
    # FK establishes the head pose; root translation places its actual nose on
    # the contact line exactly. Skull never scales, shears or goes through floor.
    gs=[]
    for i,j in enumerate(rig['joints']):
        px,py,pa=gs[j['parent']] if j['parent']>=0 else (0,0,0)
        c,s=math.cos(pa),math.sin(pa);lx,ly=j['local']
        gs.append((px+c*lx-s*ly,py+s*lx+c*ly,pa+angles[i]))
    hx,hy,a=gs[ids[-1]];bx,by=rig['joints'][ids[-1]]['rest']
    nx,ny=TARGET_X-bx,IDLE_Y-by
    nx,ny=hx+math.cos(a)*nx-math.sin(a)*ny,hy+math.sin(a)*nx+math.cos(a)*ny
    root=[ROOT_POS[0]+TARGET_X+shake-nx,ROOT_POS[1]+y-ny]
    return angles,root

rig['localFrames']=[]
for i in range(FPS*DURATION+1):
    a,r=attack(i/FPS);rig['frames'].append(a);rig['rootFrames'].append(r)
    # Keep the offscreen neck root fixed. Only the distal bones follow the slam.
    # Bake parent-relative translations so the GLB and preview use the same skin.
    gs=[];moved=[];locals=[]
    for k,j in enumerate(rig['joints']):
        px,py,pa=gs[j['parent']] if j['parent']>=0 else (0,0,0)
        c,si=math.cos(pa),math.sin(pa);lx,ly=j['local']
        g=(px+c*lx-si*ly,py+si*lx+c*ly,pa+a[k]);gs.append(g)
        factor=(max(0,k-1)/5)**1.25
        moved.append((g[0]+(r[0]-ROOT_POS[0])*factor,g[1]+(r[1]-ROOT_POS[1])*factor,g[2]))
        parent=moved[j['parent']] if j['parent']>=0 else (0,0,0)
        dx,dy=moved[k][0]-parent[0],moved[k][1]-parent[1]
        c,si=math.cos(parent[2]),math.sin(parent[2]);locals.append([c*dx+si*dy,-si*dx+c*dy])
    rig['localFrames'].append(locals)
(ROOT/'BreakerSlam_Rig.json').write_text(json.dumps(rig,separators=(',',':'),ensure_ascii=False),encoding='utf-8')

# Reuse the standard glTF skin serializer from v011, not its character or poses.
serializer=(ROOT/'gltf_skin_serializer.py').read_text(encoding='utf-8')
serializer=serializer.replace("(ROOT/'SerpentKing_Atlas.png')","(ROOT/'Stone_Open.png')")
serializer=serializer.replace("(ROOT/'SerpentKing_Skeletal.glb')","(ROOT/'BreakerSlam_Skeletal.glb')")
serializer=serializer.replace("g['animations'].append(animation)","""for ji in range(1,len(rig['joints'])):
    root_output=accessor([point(row[ji]) for row in rig['localFrames']],3)
    animation['samplers'].append(dict(input=time,output=root_output,interpolation='LINEAR'))
    animation['channels'].append(dict(sampler=len(animation['samplers'])-1,target=dict(node=ji,path='translation')))
animation['name']='Breaker_Windup_Shake_Slam_Recoil'
g['animations'].append(animation)""")
exec(serializer)
html=(ROOT/'preview.template.html').read_text(encoding='utf-8')
html=html.replace('__RIG__',json.dumps(rig,separators=(',',':'),ensure_ascii=False))
html=html.replace('__ATLAS__','data:image/png;base64,'+base64.b64encode((ROOT/'Stone_Open.png').read_bytes()).decode())
html=html.replace('__CLOSED__','data:image/png;base64,'+base64.b64encode((ROOT/'Stone_Closed.png').read_bytes()).decode())
html=html.replace('__SCRIPT__',(ROOT/'slam.js').read_text(encoding='utf-8'))
(ROOT/'BreakerSlam.html').write_text(html,encoding='utf-8')
print('Authored skeletal slam, nose contact, GLB and preview.',flush=True)

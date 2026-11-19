"""Package generated layers and author skinned meshes; does not repaint source images."""
from pathlib import Path
import json,base64,math
from PIL import Image
R=Path(__file__).resolve().parent
parts=[
 dict(name='Venom',label='毒液',start=5.5,duration=2.8,drop=[-65,350],rigidY=275,eyes=[[875,139,24,20],[951,139,22,20]],points=[[780,870],[810,770],[849,650],[836,540],[800,430],[745,330],[715,230],[745,130],[820,90],[920,155]]),
 dict(name='Stone',label='巨石',start=12,duration=2.8,drop=[-55,210],rigidY=385,eyes=[[410,277,22,15],[516,275,24,19]],points=[[650,870],[650,770],[570,690],[470,620],[456,540],[510,460],[597,399],[633,315],[600,229],[476,283]]),
 dict(name='Fang',label='突刺',start=1,duration=2.8,drop=[55,230],rigidY=435,eyes=[[1125,331,25,19]],points=[[790,870],[845,800],[785,735],[724,675],[772,611],[861,556],[934,480],[937,391],[976,302],[1095,350]]),
 dict(name='Time',label='时间',start=17,duration=2.6,drop=[0,165],rigidY=480,eyes=[[739,353,30,23],[833,353,30,23]],points=[[790,900],[829,820],[700,775],[599,735],[571,664],[608,599],[694,551],[776,500],[787,432],[785,353]])]
parts[0].update(drop=[240,220],rigidY=280,points=[[780,870],[810,800],[840,730],[850,660],[846,590],[829,520],[805,450],[766,380],[732,320],[720,280]])
parts[1].update(drop=[-45,175],rigidY=390,points=[[650,870],[648,810],[621,750],[557,690],[487,630],[451,570],[465,510],[522,460],[575,420],[605,390]])
parts[2].update(drop=[40,150],rigidY=510,points=[[790,870],[839,830],[835,790],[780,750],[731,710],[724,670],[761,630],[822,590],[889,550],[930,510]])
parts[3].update(drop=[0,125],rigidY=540,points=[[790,900],[843,860],[849,820],[755,780],[645,740],[586,700],[573,660],[594,620],[646,580],[715,540]])
parts[2]['eyes']=[[1093,332,23,19]]
rig=dict(version=26,width=1536,height=1024,duration=20,parts=parts,joints=[],meshes=[],method='hierarchical 2D linear blend skinning; rigid skull; localized eye swap')
for p in parts:
 p['eyeDelay']=0.05;p['eyeDuration']=0.18;p['idle']=1;p['visible']=True
 p['jointIds']=[]
 for j,point in enumerate(p['points']):
  idx=len(rig['joints']);p['jointIds'].append(idx)
  prev=p['points'][j-1] if j else [0,0]
  rig['joints'].append(dict(name=p['name']+('_Skull' if j==len(p['points'])-1 else '_Neck_'+str(j)),parent=idx-1 if j else -1,rest=point,local=[point[0]-prev[0],point[1]-prev[1]],inverseBind=[1,0,0,1,-point[0],-point[1]]))
 # A regular mesh preserves connected texture topology through bends.
 vertices=[];uv=[];joints=[];weights=[];indices=[];step=16;nx=97;ny=65
 for gy in range(ny):
  for gx in range(nx):
   x,y=gx*step,gy*step;vertices.append([x,y]);uv.append([x/1536,y/1024])
   if y < p['rigidY']:
    pair=[len(p['points'])-1]*2;ww=[1,0]
   else:
    k=next((k for k,(a,b) in enumerate(zip(p['points'],p['points'][1:])) if y>=b[1]),len(p['points'])-2)
    a,b=p['points'][k:k+2];t=max(0,min(1,(a[1]-y)/(a[1]-b[1])))
    pair=[k,k+1];ww=[1-t,t]
   joints.append([p['jointIds'][k] for k in pair]);weights.append([round(w,6) for w in ww])
 for y in range(ny-1):
  for x in range(nx-1):
   a=y*nx+x;indices.extend([a,a+1,a+nx,a+1,a+nx+1,a+nx])
 rig['meshes'].append(dict(name=p['name'],vertices=vertices,uv=uv,joints=joints,weights=weights,indices=indices))
# Shared body skin is not a moving backdrop: six bones and its own weighted mesh.
rig['bodyJointIds']=[]
for i,x in enumerate([260,460,660,860,1060,1260]):
 idx=len(rig['joints']);rig['bodyJointIds'].append(idx);point=[x,830]
 rig['joints'].append(dict(name='SharedCoil_'+str(i),parent=idx-1 if i else -1,rest=point,local=[200,0] if i else point,inverseBind=[1,0,0,1,-x,-830]))
body=json.loads(json.dumps(rig['meshes'][0]));body['name']='Coils'
for i,(x,y) in enumerate(body['vertices']):
 u=max(0,min(5,(x-260)/200));k=min(4,int(u));w=u-k
 body['joints'][i]=[rig['bodyJointIds'][k],rig['bodyJointIds'][k+1]];body['weights'][i]=[1-w,w]
rig['meshes'].append(body)
(R/'WakeRig.json').write_text(json.dumps(rig,separators=(',',':'),ensure_ascii=False),'utf-8')
print('Packaged native parented skeleton and shared body rig.')

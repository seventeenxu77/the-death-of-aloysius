from pathlib import Path
import json,base64,math
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter1d
R=Path(__file__).resolve().parent
S=R.parent/'BlackGoldWake_v026'
original=json.loads((S/'WakeRig.json').read_text('utf8'))

def reference(points):
    points=np.array(points,dtype=float);out=[]
    for k in range(len(points)-1):
        a,b,c,d=points[max(0,k-1)],points[k],points[k+1],points[min(len(points)-1,k+2)]
        for t in np.linspace(0,1,48,endpoint=False):
            out.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    out.append(points[-1]);out=np.array(out);lengths=np.linalg.norm(np.diff(out,axis=0),axis=1)
    cumulative=np.r_[0,np.cumsum(lengths)];return out,cumulative

def mesh(part):
    # Release the curled throat from the old rectangular skull region.
    # Facial masks below keep the mouth, eyes and crown rigid independently.
    if part['name']=='Time':part['rigidY']=470
    if part['name']=='Fang':part['rigidY']=430
    # Read alpha only to bound geometry. Original bitmap pixels are never changed.
    im=np.array(Image.open(S/'Layers'/(part['name']+'_Open.png')).convert('RGBA'))
    alpha=im[:,:,3];ys,xs=np.where(alpha>24)
    source=part['points'];cut=805;end_y=int(part['rigidY'])
    row_y=np.arange(cut,end_y-1,-1);centers=[];halfwidth=[]
    for y in row_y:
        approx=float(np.interp(y,np.array(source)[::-1,1],np.array(source)[::-1,0]))
        occupied=alpha[y]>220;changes=np.diff(np.r_[False,occupied,False].astype(int));starts=np.where(changes==1)[0];ends=np.where(changes==-1)[0]-1
        runs=[(a,b) for a,b in zip(starts,ends) if b-a>12]
        if not runs:centers.append(approx);halfwidth.append(45);continue
        a,b=min(runs,key=lambda ab:max(ab[0]-approx,approx-ab[1],0))
        centers.append((a+b)*.5);halfwidth.append((b-a)*.5)
    centers=gaussian_filter1d(np.array(centers),7);halfwidth=gaussian_filter1d(np.array(halfwidth),6)
    arc=np.column_stack([centers,row_y]);cumulative=np.r_[0,np.cumsum(np.linalg.norm(np.diff(arc,axis=0),axis=1))]
    tangent=np.arctan2(np.gradient(arc[:,1]),np.gradient(arc[:,0]))
    radii=halfwidth*np.abs(np.sin(tangent));middle=radii[(row_y>end_y+65)&(row_y<cut-45)]
    part['neckRadius']=float(np.clip(np.median(middle) if len(middle) else 45,30,62))
    part['points']=[arc[0].tolist(),arc[-1].tolist()]
    x0=max(0,int(xs.min())//8*8-8);x1=min(1536,(int(xs.max())//8+2)*8)
    y0=max(0,int(ys.min())//8*8-8);y1=min(1024,(int(ys.max())//8+2)*8)
    xsgrid=list(range(x0,x1+1,8));ysgrid=list(range(y0,y1+1,8));nx=len(xsgrid)
    vertices=np.array([[x,y] for y in ysgrid for x in xsgrid],dtype=float)
    bindings=[]
    for point in vertices:
        k=int(np.clip(cut-point[1],0,len(arc)-1));center=arc[k];u=cumulative[k]/cumulative[-1]
        normalized=(point[0]-center[0])/max(halfwidth[k],1)
        # Smooth rigid-head collar; the complete face and crown never stretch.
        z=np.clip((point[1]-(part['rigidY']-8))/72,0,1);weight=1-z*z*(3-2*z)
        if part['name'] in ('Time','Fang'):
            # Their hoods and throat curl are asymmetric: a horizontal rigid
            # rectangle pins a bent piece of neck to the skull and makes a kink.
            towards=np.array([math.cos(tangent[-1]),math.sin(tangent[-1])])
            along=float(np.dot(point-arc[-1],towards));blend=float(np.clip((along+80)/80,0,1));weight=blend*blend*(3-2*blend)
            if part['name']=='Time':left,right,bottom=600,980,475
            else:left,right,bottom=795,1250,440
            def ease(v):
                v=float(np.clip(v,0,1));return v*v*(3-2*v)
            face=ease((point[0]-left)/30)*ease((right-point[0])/30)*ease((bottom-point[1])/30)
            if part['name']=='Fang':face=max(face,ease((point[0]-985)/30)*ease((580-point[1])/25))
            weight=max(weight,face)
        bindings.extend([round(float(u),6),round(float(normalized),6),round(float(point[1]-center[1]),4),round(float(tangent[k]),6),round(float(weight),5)])
    indices=[]
    for iy in range(len(ysgrid)-1):
        for ix in range(nx-1):
            x,y=xsgrid[ix],ysgrid[iy]
            if alpha[y:min(y+9,1024),x:min(x+9,1536)].max(initial=0)<=8:continue
            k=iy*nx+ix;indices.extend([k,k+1,k+nx,k+1,k+nx+1,k+nx])
    part['referenceLength']=round(float(cumulative[-1]),3)
    part['referenceEndAngle']=math.atan2(arc[-1,1]-arc[-2,1],arc[-1,0]-arc[-2,0])
    core_y=np.where(alpha>220)[0]
    part['artTop']=int(core_y.min())
    part['referenceStartAngle']=math.atan2(arc[1,1]-arc[0,1],arc[1,0]-arc[0,0])
    return dict(name=part['name'],vertices=vertices.tolist(),uv=(vertices/np.array([1536,1024])).tolist(),indices=indices,binding=bindings)

parts=original['parts'];meshes=[mesh(p) for p in parts]
body=next(m for m in original['meshes'] if m['name']=='Coils')
meshes.append({k:body[k] for k in ['name','vertices','uv','indices']})
rig={'parts':parts,'meshes':meshes}
source=(S/'animation.js').read_text('utf8')
prefix=source[:source.index('function pose(t){')]
prefix=prefix.replace('surface.height=1024','surface.height=1152')
draw=source[source.index('function drawSkin('):source.index('function bones(')]
draw=draw.replace('gl.viewport(0,0,1536,1024)','gl.viewport(0,0,1536,1152)')
draw=draw.replace('const closed=isBody?0:1-smooth((t-p.start-p.duration-p.eyeDelay)/p.eyeDuration);','const closed=0;')
start=draw.index('  for(let i=0;i<m.vertices.length;i++)');end=draw.index('  gl.bindBuffer(gl.ARRAY_BUFFER,m.pb)',start)
draw=draw[:start]+'  deformCurve(m,state);\n'+draw[end:]
prefix=prefix.replace('gl_FragColor=color;', 'if(color.a<.02)discard;gl_FragColor=color;')
prefix=prefix.replace('color.a*=1.-roots*smoothstep(845.,865.,p.y);','color.a*=1.-roots*smoothstep(785.,805.,1024.-gl_FragCoord.y);')
assets={p.name:'data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode() for p in (S/'Layers').glob('*.png')}
html=(R/'template.html').read_text('utf8')
js=prefix+'\n'+draw+'\n'+(R/'editor.js').read_text('utf8')
html=html.replace('__RIG__',json.dumps(rig,separators=(',',':'),ensure_ascii=False)).replace('__ASSETS__',json.dumps(assets)).replace('__JS__',js)
(R/'四头蛇_贝塞尔编辑器.html').write_text(html,'utf8')
(R/'BindingReport.json').write_text(json.dumps({'method':'arc-length frame skinning; rigid skull collar; 8 px alpha-bounded mesh; two C1 cubic Bezier segments','parts':[{k:p[k] for k in ['name','referenceLength','referenceEndAngle']}|{'vertices':len(m['vertices']),'triangles':len(m['indices'])//3} for p,m in zip(parts,meshes)]},indent=2),'utf8')
print('Built arc-length bindings and standalone Bezier editor.',flush=True)

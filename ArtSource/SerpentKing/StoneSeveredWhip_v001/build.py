"""Bind the unmodified cutout to one continuous 65-joint chain along its silhouette."""
from pathlib import Path
import json, base64
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter1d
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import dijkstra
from scipy.spatial import cKDTree
from skimage.morphology import skeletonize, remove_small_objects

R=Path(__file__).resolve().parent
image=np.array(Image.open(R/'SeveredNeck.png').convert('RGBA'))
h,w=image.shape[:2]
solid=remove_small_objects(image[:,:,3]>230,min_size=1000)
sk=skeletonize(solid)
ys,xs=np.where(sk);xy=np.column_stack([xs,ys]).astype(float)
lookup=np.full((h,w),-1,dtype=np.int32);lookup[ys,xs]=np.arange(len(xs))
rows=[];cols=[];values=[]
for i,(x,y) in enumerate(zip(xs,ys)):
    for dy,dx in ((-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)):
        if 0<=y+dy<h and 0<=x+dx<w:
            j=lookup[y+dy,x+dx]
            if j>=0: rows.append(i);cols.append(j);values.append(np.hypot(dx,dy))
red=(image[:,:,0]>55)&(image[:,:,0]>image[:,:,1]*1.8)&(image[:,:,3]>230)
ry,rx=np.where(red);wound=np.array([rx.mean(),ry.mean()])
_,_,vh=np.linalg.svd(np.column_stack([rx,ry])-wound,full_matrices=False)
cutaxis=vh[0];cutwidth=float(np.percentile((np.column_stack([rx,ry])-wound)@cutaxis,96)-np.percentile((np.column_stack([rx,ry])-wound)@cutaxis,4))
rootseed=np.array([515,1410]);start=np.argmin(np.linalg.norm(xy-rootseed,axis=1));end=np.argmin(np.linalg.norm(xy-wound,axis=1))
dist,prev=dijkstra(coo_matrix((values,(rows,cols)),shape=(len(xy),len(xy))).tocsr(),indices=start,return_predecessors=True)
if not np.isfinite(dist[end]):raise ValueError('Disconnected source silhouette')
path=[];i=end
while i!=start:path.append(xy[i]);i=prev[i]
path.append(xy[start]);path=np.array(path[::-1])
# Stable base anchored to the original bottom section, rather than the image origin.
root=np.array([515.,1440.]);path=np.vstack([root,path,wound])
path=gaussian_filter1d(path,4,axis=0);path[0]=root;path[-1]=wound
lengths=np.r_[0,np.cumsum(np.linalg.norm(np.diff(path,axis=0),axis=1))]
distance=np.linspace(0,lengths[-1],65)
chain=np.column_stack([np.interp(distance,lengths,path[:,0]),np.interp(distance,lengths,path[:,1])])
chain[0]=root;chain[-1]=wound

ys,xs=np.where(solid);x0=max(0,int(xs.min())//8*8-8);x1=min(w,(int(xs.max())//8+2)*8)
y0=max(0,int(ys.min())//8*8-8);y1=min(h,(int(ys.max())//8+2)*8)
xgrid=np.arange(x0,x1+1,8);ygrid=np.arange(y0,y1+1,8);nx=len(xgrid)
vertices=np.array([[x,y] for y in ygrid for x in xgrid],float)
tri=[]
for gy,y in enumerate(ygrid[:-1]):
    for gx,x in enumerate(xgrid[:-1]):
        if not solid[y:min(y+9,h),x:min(x+9,w)].any():continue
        k=gy*nx+gx;tri.extend([k,k+1,k+nx,k+1,k+nx+1,k+nx])
used,indices=np.unique(tri,return_inverse=True);vertices=vertices[used]
seg=chain[1:]-chain[:-1];den=(seg*seg).sum(axis=1)
bindings=[]
for point in vertices:
    t=np.clip(((point-chain[:-1])*seg).sum(axis=1)/den,0,1)
    near=chain[:-1]+t[:,None]*seg
    k=int(np.argmin(((near-point)**2).sum(axis=1)))
    # Keep the oblique cut plane and its red rim rigid together at the chain tip.
    if point[1]<310 and point[0]<515:k=63;t[k]=1
    bindings.append([k,float(t[k])])

data=dict(width=w,height=h,chain=np.round(chain,4).tolist(),vertices=vertices.tolist(),indices=indices.tolist(),binding=bindings,
          wound=wound.tolist(),cutAxis=cutaxis.tolist(),cutWidth=cutwidth)
(R/'WhipRig.json').write_text(json.dumps(data,separators=(',',':')),'utf8')
print('Packaged native 65-node continuous whip rig.')

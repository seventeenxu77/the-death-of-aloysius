from pathlib import Path
from PIL import Image
import json, shutil, base64, zipfile

ROOT = Path(__file__).resolve().parent
SRC = Path('C:/Users/SQ/.codex/generated_images/01a1afb4-fedd-77c0-8d7c-498979dd5a59')
FILES = {
 'Scene_Empty': ('d521cd62-21c9-4a25-a05e-35748804c742', '空房间建筑'),
 'Bed_Interior': ('c7de2ae4-41dc-47e7-a067-75532b4be112', '帘后床铺'),
 'Bed_Frame': ('d79b2197-4177-4ab1-99fb-02038b80f595', '前层床架'),
 'Curtain_Left': ('17bb82eb-11b2-4aad-9be9-41f33db549fa', '左床帘'),
 'Curtain_Right': ('bbe89cf8-90e7-41aa-868b-be7259df0918', '右床帘'),
 'Bench': ('069442aa-8154-463a-ade1-42eb81a7eb14', '窄凳'),
 'Cabinet': ('20ef1dd1-cd99-4ee0-9f45-c726ed887b4a', '柜台及托盘烛台'),
 'Bell_Pull': ('af6c2a82-8ddf-4069-a1c0-3b421debd192', '传唤铃及拉绳'),
 'Door_Leaf': ('eb62b820-7cdd-4da1-8cc7-6243626ce0f1', '独立门扇'),
 'Snakeskin_Opening': ('0bea8446-255a-4e49-ac7f-aa03db704610', '正面蛇皮洞口'),
 'Chandelier': ('ce37d6b4-8666-4999-9f9a-55ef9524cf80', '吊灯'),
 'Rug': ('120badea-ba5d-4d6f-a90a-b593cdbfeb2c', '小地毯'),
 'Door_Lock': ('73b5f92d-1dbb-4cd2-82e9-ecae0a2626a4', '门闩及挂锁'),
}
(ROOT/'Sprites').mkdir(exist_ok=True)
(ROOT/'Reference').mkdir(exist_ok=True)
assets = {}
embedded = {}
for name,(uid,label) in FILES.items():
    dst=ROOT/'Sprites'/f'{name}.png'
    shutil.copy2(SRC/f'exec-{uid}.png',dst)
    im=Image.open(dst).convert('RGBA')
    box=im.getchannel('A').point(lambda a:255 if a>16 else 0).getbbox()
    assets[name]={'file':f'Sprites/{name}.png','label':label,'size':list(im.size),'sourceRect':[box[0],box[1],box[2]-box[0],box[3]-box[1]],'pivot':[0.5,0.5]}
    embedded[name]='data:image/png;base64,'+base64.b64encode(dst.read_bytes()).decode()
for name,uid in [('Approved_Scene','a5e8db06-7ca5-41e0-a235-4861908ff30d'),('Open_Bed_Reference','4619bccb-58ee-4b29-b6f8-5f470e38e716')]:
    shutil.copy2(SRC/f'exec-{uid}.png', ROOT/'Reference'/f'{name}.png')
placements=[
 ['Rug',[1310,490,330,30]],['Door_Leaf',[1532,642,118,195]],['Door_Lock',[1555,755,53,28]],
 ['Door_Leaf',[1246,354,26,174]],['Bench',[1354,448,98,39]],['Cabinet',[1510,429,77,60]],
 ['Bell_Pull',[1265,283,40,170]],['Snakeskin_Opening',[1354,708,56,56]],['Snakeskin_Opening',[1672,438,38,47]],
 ['Chandelier',[592,0,78,112]],['Chandelier',[1460,205,66,52]],['Chandelier',[287,550,48,93]],['Chandelier',[966,550,48,93]],
]
assets['Curtain_Left']['pivot']=[0,1]
assets['Curtain_Right']['pivot']=[1,1]
assets['Door_Leaf']['pivot']=[0,0.5]
manifest={'coordinateSystem':'Preview pixels, origin top-left; Unity pivots bottom-left normalized.', 'designSize':[1774,887], 'assets':assets,'instances':[{'asset':a,'rect':r,'order':i+1} for i,(a,r) in enumerate(placements)],'bed':{'sceneRect':[340,108,620,390],'localSize':[1536,1024],'backToFront':['Bed_Interior','Future_Serpent_Actor','Curtain_Left','Curtain_Right','Bed_Frame'],'interiorRect':[225,350,1090,600],'curtainTop':225,'curtainHeight':740,'closedWidth':585,'gatheredWidth':140,'leftOuterX':190,'rightOuterX':1345},'notes':['Reconstructed from approved concept with imagegen; not pixel-exact extraction.','Scene architecture remains one opaque plate; props and bed parts have alpha.','Future serpent actor is an empty layer slot, not an included image.','Curtain gathering is a mesh-like strip deformation preview, not cloth simulation.']}
(ROOT/'layers.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
template=(ROOT/'preview_template.html').read_text(encoding='utf-8')
template=template.replace('__MANIFEST__',json.dumps(manifest,ensure_ascii=False)).replace('__IMAGES__',json.dumps(embedded))
(ROOT/'Curtain_Preview.html').write_text(template,encoding='utf-8')
print(json.dumps({n:{'size':a['size'],'sourceRect':a['sourceRect']} for n,a in assets.items()},ensure_ascii=False))
with zipfile.ZipFile(ROOT/'Bedchamber_Layers.zip','w',zipfile.ZIP_DEFLATED) as z:
    for p in ROOT.rglob('*'):
        if p.is_file() and p.suffix.lower() not in ['.zip','.py'] and p.name!='preview_template.html':
            z.write(p,p.relative_to(ROOT))
print('Packaged assets, manifest, self-contained preview, reference art and prompts.')

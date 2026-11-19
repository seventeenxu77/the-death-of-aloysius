import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { TransformControls } from 'three/addons/controls/TransformControls.js';

const $ = (id) => document.getElementById(id);
const clone = (value) => JSON.parse(JSON.stringify(value));
const kindNames = {wall:'墙体',floor:'地面',roof:'屋顶',door:'门',room:'房间',suite:'套院',region:'区域',level:'楼层',group:'组合',mesh:'构件'};
const identity = new THREE.Matrix4();
const white = new THREE.Color(1, 1, 1), contextTint = new THREE.Color(.64, .68, .72);
let model, project, scene, camera, renderer, orbit, transform, proxy, selectionBox, selectionMaterial, grid;
let selectionHighlight = {totalCount:0, visibleCount:0, batchCount:0, nodeIds:[]};
let nodes = new Map(), roots = [], batches = [], meshes = [], modelBounds = new THREE.Box3();
let geometries = [], materials = [], selectedIds = [], expanded = new Set(), treeLimit = 900;
let undoStack = [], redoStack = [], savedSerial = 0, editSerial = 0, saveTimer, saveInFlight = false, conflict = false;
let backupKey, pendingBackup, toastTimer, treeSearchTimer, dragContext = null, pointerDown = null;
let initialized = false, frameRequested = false, needsRender = true, currentMode = 'translate';
let lastSavedAt = null, currentServerRevision = 0, textEditSession = null, maxModelHeight = 40;
const view = {roof:false, floor:'all', section:false, sectionHeight:20, grid:true, preset:'iso', autoWalls:false};
let autoHiddenWalls=new Set(), autoWallGroups=[], autoWallsDirty=true, autoWallGeometryDirty=true, lastAutoWallUpdate=0, autoHiddenGroups=[];
const raycaster = new THREE.Raycaster(), pointer = new THREE.Vector2();
const unitScale = new THREE.Vector3(1,1,1);

function toast(message, duration = 3800) {
  $('toast').textContent = message; $('toast').classList.remove('hidden');
  clearTimeout(toastTimer); toastTimer = setTimeout(() => $('toast').classList.add('hidden'), duration);
}
function setStatus(message, type = '') { $('saveStatus').textContent = message; $('saveStatus').className = `save-status ${type}`; }
function showBanner(message, isConflict = false) {
  $('connectionMessage').textContent = message; $('connectionBanner').classList.remove('hidden');
  $('retryBtn').classList.toggle('hidden', isConflict);
  $('conflictExportBtn').classList.toggle('hidden', !isConflict);
  $('loadServerBtn').classList.toggle('hidden', !isConflict);
  requestAnimationFrame(resize);
}
function clearBanner() { $('connectionBanner').classList.add('hidden'); requestAnimationFrame(resize); }
function changed() { return editSerial !== savedSerial; }
function stateSnapshot() { return clone({overrides:project.overrides, notes:project.notes || ''}); }
function snapshotEqual(a,b) { return JSON.stringify(a) === JSON.stringify(b); }
function pushHistory(before) {
  if (snapshotEqual(before, stateSnapshot())) return false;
  undoStack.push(before); if (undoStack.length > 100) undoStack.shift(); redoStack = [];
  markChanged(); return true;
}
function markChanged() {
  editSerial++; writeBackup(); updateChangeList(); updateHistory(); renderTree();
  setStatus(conflict ? '保存冲突 · 本地保留' : '有修改 · 等待保存', conflict ? 'error' : 'pending');
  clearTimeout(saveTimer); if (!conflict) saveTimer = setTimeout(saveProject, 1000);
}
function updateHistory() { $('undoBtn').disabled = !undoStack.length; $('redoBtn').disabled = !redoStack.length; }
function beginTextEdit(element) {
  if(textEditSession?.element===element)return;
  finishTextEdit();
  textEditSession={element,before:stateSnapshot(),nodeId:element.id==='objectNote'?selectedIds[0]:null};
}
function applyTextInput(element) {
  beginTextEdit(element);
  if(element.id==='objectNote') {if(!textEditSession.nodeId)return;setOverride(textEditSession.nodeId,'note',element.value);}
  else project.notes=element.value;
  markChanged();
}
function finishTextEdit() {
  if(!textEditSession)return;
  const before=textEditSession.before;textEditSession=null;
  if(snapshotEqual(before,stateSnapshot()))return;
  undoStack.push(before);if(undoStack.length>100)undoStack.shift();redoStack=[];updateHistory();
}
function restoreSnapshot(snapshot) {
  project.overrides = clone(snapshot.overrides); project.notes = snapshot.notes || '';
  applyAllOverrides(); markChanged(); refreshSelection(); $('projectNotes').value = project.notes;
}
function undo() {
  if (!undoStack.length || dragContext) return;
  redoStack.push(stateSnapshot()); const s = undoStack.pop(); restoreSnapshot(s); updateHistory(); toast('已撤销');
}
function redo() {
  if (!redoStack.length || dragContext) return;
  undoStack.push(stateSnapshot()); const s = redoStack.pop(); restoreSnapshot(s); updateHistory(); toast('已重做');
}
function writeBackup() {
  if (!backupKey || !project) return;
  try { localStorage.setItem(backupKey, JSON.stringify({...project, revision:currentServerRevision, overrides:project.overrides, notes:project.notes, pending:changed(), backupAt:new Date().toISOString()})); }
  catch { setStatus('浏览器备份空间不足 · 请保存', 'error'); }
}
async function readJSON(url, options) {
  const response = await fetch(url, options);
  if (!response.ok) { const error = new Error(`HTTP ${response.status}`); error.status = response.status; try { error.payload = await response.json(); } catch {} throw error; }
  return response.json();
}
async function saveProject() {
  clearTimeout(saveTimer);
  if (!initialized || saveInFlight || conflict || !changed()) {
    if (initialized && !changed() && !saveInFlight && !conflict) setStatus('已保存到本地项目', 'saved');
    return;
  }
  saveInFlight = true; setStatus('正在保存…', 'pending');
  const serial = editSerial, outgoing = {schemaVersion:1,sceneId:model.sceneId,baseRevision:currentServerRevision,revision:currentServerRevision,overrides:clone(project.overrides),notes:project.notes || ''};
  try {
    const saved = await readJSON('/api/project', {method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(outgoing)});
    currentServerRevision = saved.revision; project.revision = saved.revision; project.updatedAt = saved.updatedAt;
    savedSerial = serial; lastSavedAt = saved.updatedAt || new Date().toISOString(); clearBanner();
    $('revisionText').textContent = `版本 ${currentServerRevision}`;
    setStatus(changed() ? '有新修改 · 继续保存' : '已保存到本地项目', changed() ? 'pending' : 'saved'); writeBackup();
  } catch (error) {
    if (error.status === 409) {
      conflict = true; setStatus('版本冲突 · 本地保留', 'error');
      showBanner('项目已被其他窗口修改。当前修改保留在本页，请先导出本地修改，再加载服务器版本。', true);
    } else if(error.status && error.status < 500) {
      setStatus('保存被拒绝 · 本地保留', 'error');
      showBanner(error.payload?.message || `保存未被接受（HTTP ${error.status}），请检查修改数据。`);
    } else {
      setStatus('未同步 · 浏览器已备份', 'error'); showBanner('本地项目服务连接失败。当前修改保留在浏览器，请保持本页并重新连接。');
    }
    writeBackup();
  } finally {
    saveInFlight = false;
    if (!conflict && changed() && savedSerial === serial) saveTimer = setTimeout(saveProject, 250);
  }
}
async function loadServerVersion() {
  if (changed() && !confirm('加载服务器版本会替换本页未同步的修改。建议先导出 JSON 备份。继续吗？')) return;
  try {
    const server = await readJSON('/api/project'); validateProject(server);
    project = server; currentServerRevision = server.revision || 0; conflict = false; savedSerial = ++editSerial;
    undoStack = []; redoStack = []; applyAllOverrides(); refreshSelection(); updateHistory(); updateChangeList(); renderTree();
    $('projectNotes').value = project.notes || ''; $('revisionText').textContent = `版本 ${currentServerRevision}`;
    clearBanner(); setStatus('已加载服务器版本', 'saved'); writeBackup();
  } catch(error) { toast(`无法加载：${error.message}`); }
}
function validateProject(data) {
  if (!data || data.schemaVersion !== 1 || data.sceneId !== model.sceneId) throw new Error('这份修改文件不属于当前宫殿模型，或文件版本不支持。');
  if (!data.overrides || typeof data.overrides !== 'object' || Array.isArray(data.overrides)) throw new Error('修改文件缺少有效的 overrides 数据。');
  let unknown = 0;
  for (const [id, values] of Object.entries(data.overrides)) {
    if (!nodes.has(id)) { unknown++; continue; }
    if (!values || typeof values !== 'object' || Array.isArray(values)) throw new Error(`对象 ${id} 的修改格式无效。`);
    if(Object.keys(values).some(key=>!['position','rotation','scale','name','hidden','note'].includes(key)))throw new Error(`对象 ${id} 包含不支持的修改字段。`);
    for (const [key, size] of [['position',3],['rotation',4],['scale',3]]) if (values[key] !== undefined) {
      if (!Array.isArray(values[key]) || values[key].length !== size || values[key].some(v => !Number.isFinite(v))) throw new Error(`${id} 的 ${key} 数值无效。`);
      if (key === 'scale' && values[key].some(v => v <= 0)) throw new Error(`${id} 的缩放必须为正数。`);
      if (key === 'rotation' && Math.abs(Math.hypot(...values[key])-1) > .001) throw new Error(`${id} 的旋转必须是归一化四元数。`);
    }
    if (values.hidden !== undefined && typeof values.hidden !== 'boolean') throw new Error('隐藏状态必须为布尔值。');
    for (const [key,max] of [['name',200],['note',4000]]) if (values[key] !== undefined && (typeof values[key] !== 'string'||[...values[key]].length>max||values[key].includes('\u0000'))) throw new Error(`${key} 必须为 ${max} 字以内的文本，且不能包含空字符。`);
  }
  if (unknown) throw new Error(`文件中有 ${unknown} 个对象不在当前模型中，请使用相同模型版本的设计文件。`);
  if (data.notes !== undefined && (typeof data.notes !== 'string'||[...data.notes].length>20000||data.notes.includes('\u0000'))) throw new Error('整体说明必须为 20000 字以内的文本，且不能包含空字符。');
}

function unityVector(v) { return new THREE.Vector3(v[0],v[1],-v[2]); }
function unityQuaternion(q) { return new THREE.Quaternion(-q[0],-q[1],q[2],q[3]).normalize(); }
function toUnityPosition(v) { return [v.x,v.y,-v.z].map(cleanNumber); }
function toUnityQuaternion(q) { return [-q.x,-q.y,q.z,q.w].map(cleanNumber); }
function cleanNumber(v) { return Math.abs(v) < 1e-9 ? 0 : Number(v.toFixed(7)); }
function nodeValue(record, key) { return project.overrides[record.id]?.[key] ?? record.data[key]; }
function nodeName(record) { return project?.overrides[record.id]?.name ?? record.data.name; }
function normalizeKind(record) { return (record.data.kind || 'group').toLowerCase(); }
function kindLabel(record) { return kindNames[normalizeKind(record)] || record.data.kind || '组合'; }
function setOverride(id, key, value) {
  const record = nodes.get(id); let baseline = record.data[key];
  if (key === 'hidden') baseline = record.data.active === false;
  if (key === 'note') baseline = '';
  let same = Array.isArray(value) && Array.isArray(baseline) ? value.length === baseline.length && value.every((n,i) => Math.abs(n - baseline[i]) < .00001) : value === baseline;
  if (key === 'rotation' && Array.isArray(value) && Array.isArray(baseline)) same ||= value.every((n,i) => Math.abs(n + baseline[i]) < .00001);
  if (same || value === undefined) { if (project.overrides[id]) delete project.overrides[id][key]; }
  else { project.overrides[id] ||= {}; project.overrides[id][key] = value; }
  if (project.overrides[id] && !Object.keys(project.overrides[id]).length) delete project.overrides[id];
}
function captureNodeTransform(record) {
  const obj = record.object;
  setOverride(record.id, 'position', toUnityPosition(obj.position));
  setOverride(record.id, 'rotation', toUnityQuaternion(obj.quaternion));
  setOverride(record.id, 'scale', obj.scale.toArray().map(cleanNumber));
}
function applyAllOverrides() {
  for (const record of nodes.values()) {
    const obj = record.object;
    obj.position.copy(unityVector(nodeValue(record,'position')));
    obj.quaternion.copy(unityQuaternion(nodeValue(record,'rotation')));
    obj.scale.fromArray(nodeValue(record,'scale')); obj.updateMatrix();
  }
  scene.updateMatrixWorld(true); syncInstances();
}
function ownHidden(record) { return project.overrides[record.id]?.hidden ?? record.data.active === false; }
function isRoof(record) {
  if(record.data.roof===true)return true;
  let n = record;
  while(n) { if (normalizeKind(n) === 'roof' || /屋顶|天花|ceiling|roof/i.test(n.data.name)) return true; n = nodes.get(n.data.parentId); }
  return false;
}
function baseVisibility(record) {
  let n = record;
  while(n) { if (ownHidden(n)) return false; n = nodes.get(n.data.parentId); }
  return view.roof || !record.roof;
}
function effectiveVisibility(record) { return baseVisibility(record)&&!autoHiddenWalls.has(record.id); }

// Temporary view-only cutaway. Never write project overrides or change source geometry.
function wallName(record){return /墙|wall|enclosure|separation|threshold/i.test(record.data.name);}
function wallExcluded(record){
  if(record.roof)return true;
  if(!wallName(record)&&/floor|deck|slab|step|家具|地板|楼板|地面|踏步|底板|扶手|栏杆|柱础|柱头|床|柜|桌/i.test(record.data.name))return true;
  for(let n=record;n;n=nodes.get(n.data.parentId)){
    if(n.roof||['floor','roof','door'].includes(normalizeKind(n)))return true;
    if(['room','suite','region'].includes(normalizeKind(n)))break;
  }
  return false;
}
function rebuildAutoWallGroups(){
  const grouped=new Map(),matrix=new THREE.Matrix4(),size=new THREE.Vector3(),axis=new THREE.Vector3(),normalMatrix=new THREE.Matrix3();
  for(const record of nodes.values()){
    if(!record.geometry||wallExcluded(record))continue;
    let wall=null;
    for(let n=record;n;n=nodes.get(n.data.parentId)){
      const assembly=n.children.length>0&&n.children.length<=16&&n.children.every(id=>nodes.get(id).geometry)&&n.children.some(id=>wallName(nodes.get(id)));
      if(wallName(n)||assembly){wall=n;break;}
      if(['room','suite','region'].includes(normalizeKind(n)))break;
    }
    if(!wall)continue;
    // Own only the immediate wall assembly, never a whole room/enclosure wing.
    let owner=wall,parent=nodes.get(wall.data.parentId);
    const parentAssembly=parent&&parent.children.length>0&&parent.children.length<=16&&parent.children.every(id=>nodes.get(id).geometry)&&parent.children.some(id=>wallName(nodes.get(id)));
    if(parent&&(wallName(parent)||parentAssembly)&&parent.children.length<=24&&!['room','suite','region'].includes(normalizeKind(parent)))owner=parent;
    matrix.copy(record.object.matrixWorld).multiply(record.geometryMatrix);
    const worldBox=record.geometry.boundingBox.clone().applyMatrix4(matrix);
    record.geometry.boundingBox.getSize(size);
    let thinAxis=-1,thickness=Infinity;
    for(let i=0;i<3;i++){
      axis.setFromMatrixColumn(matrix,i);const length=axis.length();
      if(length<1e-8||Math.abs(axis.y/length)>.3)continue;
      const extent=size.getComponent(i)*length;
      if(extent<thickness){thickness=extent;thinAxis=i;}
    }
    const height=worldBox.max.y-worldBox.min.y;
    const span=Math.max(worldBox.max.x-worldBox.min.x,worldBox.max.z-worldBox.min.z);
    const isPanel=thinAxis>=0&&height>=1.6&&span>=.35&&thickness<=Math.min(1.25,Math.max(.22,span*.35))&&thickness<height*.45;
    if(!grouped.has(owner.id))grouped.set(owner.id,{id:owner.id,record:owner,members:[],panels:[]});
    const group=grouped.get(owner.id);group.members.push(record.id);
    if(isPanel){
      normalMatrix.getNormalMatrix(matrix);const normal=new THREE.Vector3().setComponent(thinAxis,1).applyMatrix3(normalMatrix).normalize();
      if(Math.abs(normal.y)<.35)group.panels.push({record,box:record.geometry.boundingBox,worldBox,inverse:matrix.clone().invert(),matrix:matrix.clone(),normal,center:worldBox.getCenter(new THREE.Vector3())});
    }
  }
  autoWallGroups=[...grouped.values()].filter(g=>g.panels.length);
  autoWallGeometryDirty=false;
}
function updateAutoWallStatus(){
  const status=$('autoWallsStatus');status.classList.toggle('hidden',!view.autoWalls);
  status.textContent=autoHiddenGroups.length?`已临时隐藏 ${autoHiddenGroups.length} 组近侧墙 · 关闭开关可恢复`:'自动隐藏已开启 · 可选中房间按 F 聚焦后查看';
}
function updateAutoWalls(){
  if(!initialized||!autoWallsDirty)return;
  autoWallsDirty=false;
  if(autoWallGeometryDirty){scene.updateMatrixWorld(true);rebuildAutoWallGroups();}
  const next=new Set(),hiddenGroups=[];
  if(view.autoWalls){
    camera.updateMatrixWorld();
    const target=orbit.target,origin=camera.position,distance=origin.distanceTo(target);
    const forward=new THREE.Vector3().subVectors(target,origin).normalize();
    const right=new THREE.Vector3().setFromMatrixColumn(camera.matrixWorld,0),up=new THREE.Vector3().setFromMatrixColumn(camera.matrixWorld,1);
    const halfHeight=distance*Math.tan(THREE.MathUtils.degToRad(camera.fov)*.5)*.72;
    const halfWidth=halfHeight*camera.aspect*.9;
    const planes=activePlanes(),rays=[],localRay=new THREE.Ray(),hit=new THREE.Vector3(),worldHit=new THREE.Vector3(),toEye=new THREE.Vector3();
    for(const x of [-1,-.5,0,.5,1])for(const y of [-1,-.5,0,.5,1]){
      const endpoint=target.clone().addScaledVector(right,x*halfWidth).addScaledVector(up,y*halfHeight);
      rays.push(new THREE.Ray(origin.clone(),endpoint.sub(origin).normalize()));
    }
    for(const group of autoWallGroups){
      // Keep explicitly selected walls available to edit. Room/region selections still cut away.
      if(selectedIds.includes(group.id)||group.panels.some(p=>selectedIds.includes(p.record.id)))continue;
      let blocked=false;
      for(const panel of group.panels){
        if(!baseVisibility(panel.record))continue;
        toEye.subVectors(origin,panel.center).normalize();
        if(Math.abs(panel.normal.dot(toEye))<.16)continue;
        for(const ray of rays){
          if(!ray.intersectsBox(panel.worldBox))continue;
          localRay.copy(ray).applyMatrix4(panel.inverse);
          if(!localRay.intersectBox(panel.box,hit))continue;
          worldHit.copy(hit).applyMatrix4(panel.matrix);
          // If a floor/section filter clips the entry face, its exit face can still obstruct.
          if(!planes.every(p=>p.distanceToPoint(worldHit)>=-.001)){
            localRay.origin.copy(hit).addScaledVector(localRay.direction,1e-5);
            if(!localRay.intersectBox(panel.box,hit))continue;
            worldHit.copy(hit).applyMatrix4(panel.matrix);
            if(!planes.every(p=>p.distanceToPoint(worldHit)>=-.001))continue;
          }
          const depth=worldHit.clone().sub(origin).dot(forward);
          if(depth<camera.near||depth>=distance-.06)continue;
          blocked=true;break;
        }
        if(blocked)break;
      }
      if(blocked){hiddenGroups.push(group.id);for(const id of group.members)if(baseVisibility(nodes.get(id)))next.add(id);}
    }
  }
  const differs=next.size!==autoHiddenWalls.size||[...next].some(id=>!autoHiddenWalls.has(id));
  autoHiddenWalls=next;autoHiddenGroups=hiddenGroups;updateAutoWallStatus();
  if(differs){syncInstances(false);updateVisibilityHint();}
}
function buildGeometry(data, buffer) {
  const geometry = new THREE.BufferGeometry();
  const position = new Float32Array(buffer.slice(data.position.offset, data.position.offset + data.position.count * 4));
  for(let i=2;i<position.length;i+=3) position[i] *= -1;
  geometry.setAttribute('position',new THREE.BufferAttribute(position,3));
  if (data.normal?.count) {
    const normal = new Float32Array(buffer.slice(data.normal.offset,data.normal.offset+data.normal.count*4));
    for(let i=2;i<normal.length;i+=3) normal[i] *= -1;
    geometry.setAttribute('normal',new THREE.BufferAttribute(normal,3));
  }
  if (data.uv?.count) geometry.setAttribute('uv',new THREE.BufferAttribute(new Float32Array(buffer.slice(data.uv.offset,data.uv.offset+data.uv.count*4)),2));
  if (data.index?.count) {
    const index = new Uint32Array(buffer.slice(data.index.offset,data.index.offset+data.index.count*4));
    for(let i=0;i<index.length;i+=3) {const swap=index[i+1];index[i+1]=index[i+2];index[i+2]=swap;}
    geometry.setIndex(new THREE.BufferAttribute(index,1));
  } else {
    const count=position.length/3,index=new Uint32Array(count);
    for(let i=0;i<count;i+=3){index[i]=i;index[i+1]=i+2;index[i+2]=i+1;}geometry.setIndex(new THREE.BufferAttribute(index,1));
  }
  if (!data.normal?.count) geometry.computeVertexNormals();
  for(const group of data.groups || []) geometry.addGroup(group.start,group.count,group.materialIndex);
  geometry.computeBoundingBox(); geometry.computeBoundingSphere(); return geometry;
}
function createMaterials() {
  const loader = new THREE.TextureLoader();
  materials = model.materials.map(data => {
    const c = data.color || [.75,.68,.55,1];
    const material = new THREE.MeshStandardMaterial({color:new THREE.Color(c[0],c[1],c[2]),roughness:.88,metalness:0,side:THREE.DoubleSide});
    material.name = data.name || '建筑材质';
    if (c[3] < .98) {material.transparent=true;material.opacity=Math.max(.15,c[3]);}
    if(data.texture) loader.load(`model/${data.texture}`, texture => {texture.colorSpace=THREE.SRGBColorSpace;texture.wrapS=texture.wrapT=THREE.RepeatWrapping;if(data.textureScale)texture.repeat.fromArray(data.textureScale);if(data.textureOffset)texture.offset.fromArray(data.textureOffset);texture.anisotropy=Math.min(4,renderer.capabilities.getMaxAnisotropy());material.map=texture;material.needsUpdate=true;requestRender();},undefined,() => console.warn('贴图读取失败，使用材质底色：',data.texture));
    return material;
  });
  materials.push(new THREE.MeshStandardMaterial({color:0xc9b18b,roughness:.9,side:THREE.DoubleSide}));
}
function mirroredGeometry(geometry) {
  if(geometry.userData.mirrored) return geometry.userData.mirrored;
  const copy=geometry.clone(),pos=copy.attributes.position,norm=copy.attributes.normal;
  for(let i=0;i<pos.count;i++){pos.setX(i,-pos.getX(i));if(norm)norm.setX(i,-norm.getX(i));}
  const arr=copy.index.array;for(let i=0;i<arr.length;i+=3){const a=arr[i+1];arr[i+1]=arr[i+2];arr[i+2]=a;}
  copy.computeBoundingBox();copy.computeBoundingSphere();geometry.userData.mirrored=copy;return copy;
}
function buildBatches() {
  for(const batch of batches){scene.remove(batch.mesh);batch.mesh.dispose();if(batch.highlight){scene.remove(batch.highlight);batch.highlight.dispose();}}
  batches=[];meshes=[];const map=new Map();scene.updateMatrixWorld(true);
  for(const record of nodes.values()) {
    const index=record.data.geometry;if(index===null||index===undefined||!geometries[index])continue;
    record.geometry=geometries[index];const negative=record.object.matrixWorld.determinant()<0;
    const materialIds=record.data.materials?.length?record.data.materials:[materials.length-1];
    const key=`${index}|${materialIds.join(',')}|${negative}`;
    if(!map.has(key))map.set(key,{geometry:negative?mirroredGeometry(record.geometry):record.geometry,materialIds,negative,records:[]});
    map.get(key).records.push(record);
  }
  for(const batch of map.values()) {
    const mats=batch.materialIds.map(i=>materials[i]||materials.at(-1));
    const mesh=new THREE.InstancedMesh(batch.geometry,mats.length===1?mats[0]:mats,batch.records.length);
    mesh.name='建筑实例';mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);mesh.frustumCulled=false;
    mesh.userData.batch=batch;batch.mesh=mesh;
    batch.records.forEach((record,i)=>{record.batch=batch;record.instance=i;mesh.setColorAt(i,white);});
    scene.add(mesh);meshes.push(mesh);batches.push(batch);
  }
  syncInstances();
}
function syncInstances(invalidateAutoWalls=true) {
  if(!scene)return;scene.updateMatrixWorld(true);
  if(invalidateAutoWalls){autoWallGeometryDirty=true;autoWallsDirty=true;}
  let rebuild=false;
  for(const record of nodes.values()) if(record.geometry&&record.batch && (record.object.matrixWorld.determinant()<0)!==record.batch.negative) {rebuild=true;break;}
  if(rebuild){buildBatches();return;}
  const collapsed=new THREE.Matrix4().makeScale(0,0,0),matrix=new THREE.Matrix4();
  const hasSelection=selectedIds.length>0,planes=activePlanes(),bounds=new THREE.Box3();
  selectionHighlight={totalCount:0,visibleCount:0,batchCount:0,nodeIds:[]};
  for(const batch of batches) {
    let highlightCount=0;
    for(let i=0;i<batch.records.length;i++) {
      const record=batch.records[i];record.visible=effectiveVisibility(record);record.highlighted=false;
      const selected=isInSelection(record);
      if(selected)selectionHighlight.totalCount++;
      if(record.visible){matrix.copy(record.object.matrixWorld).multiply(record.geometryMatrix);if(batch.negative)matrix.scale(new THREE.Vector3(-1,1,1));batch.mesh.setMatrixAt(i,matrix);}
      else batch.mesh.setMatrixAt(i,collapsed);
      batch.mesh.setColorAt(i,hasSelection&&!selected?contextTint:white);
      if(selected&&record.visible){
        bounds.copy(batch.geometry.boundingBox).applyMatrix4(matrix);
        // Omit fully clipped parts; the material clips partial intersections exactly.
        const inView=planes.every(p=>p.distanceToPoint(new THREE.Vector3(p.normal.x>=0?bounds.max.x:bounds.min.x,p.normal.y>=0?bounds.max.y:bounds.min.y,p.normal.z>=0?bounds.max.z:bounds.min.z))>=0);
        if(inView){
          if(!batch.highlight){
            const highlight=new THREE.InstancedMesh(batch.geometry,selectionMaterial,batch.records.length);
            highlight.name='所选区域高亮';highlight.instanceMatrix.setUsage(THREE.DynamicDrawUsage);highlight.frustumCulled=false;highlight.renderOrder=5;highlight.raycast=()=>{};
            batch.highlight=highlight;scene.add(highlight);
          }
          batch.highlight.setMatrixAt(highlightCount++,matrix);record.highlighted=true;
          selectionHighlight.nodeIds.push(record.id);
        }
      }
    }
    if(batch.highlight){batch.highlight.count=highlightCount;batch.highlight.visible=highlightCount>0;if(highlightCount){batch.highlight.instanceMatrix.needsUpdate=true;selectionHighlight.batchCount++;}}
    selectionHighlight.visibleCount+=highlightCount;
    batch.mesh.instanceMatrix.needsUpdate=true;batch.mesh.instanceColor.needsUpdate=true;
    batch.mesh.computeBoundingSphere();
  }
  updateSelectionBox();updateSelectionLabel();requestRender();
}
function isInSelection(record) { let n=record;while(n){if(selectedIds.includes(n.id))return true;n=nodes.get(n.data.parentId);}return false; }
function objectBounds(record, visibleOnly=false) {
  const box=new THREE.Box3();
  record.object.updateWorldMatrix(true,true);
  function visit(n){if(n.geometry&&(!visibleOnly||effectiveVisibility(n)))box.union(n.geometry.boundingBox.clone().applyMatrix4(n.object.matrixWorld.clone().multiply(n.geometryMatrix)));for(const id of n.children)visit(nodes.get(id));}
  visit(record);
  if(box.isEmpty()) {const p=record.object.getWorldPosition(new THREE.Vector3());box.setFromCenterAndSize(p,new THREE.Vector3(1,1,1));}
  return box;
}
function selectionBounds() {const box=new THREE.Box3();for(const id of selectedIds)box.union(objectBounds(nodes.get(id)));return box;}
function visibleBounds() {const box=new THREE.Box3();for(const r of nodes.values())if(r.geometry&&baseVisibility(r))box.union(r.geometry.boundingBox.clone().applyMatrix4(r.object.matrixWorld.clone().multiply(r.geometryMatrix)));return box.isEmpty()?modelBounds.clone():box;}
function updateSelectionBox() {
  if(!selectionBox)return;
  selectionBox.visible=!!selectedIds.length;
  if(selectedIds.length){selectionBox.box.copy(selectionBounds());selectionBox.updateMatrixWorld(true);}
}
function updateSelectionLabel(){
  const has=selectedIds.length>0;
  $('selectionLabel').classList.toggle('hidden',!has);$('viewport').classList.toggle('has-selection',has);
  if(!has)return;
  const names=selectedIds.map(id=>nodeName(nodes.get(id)));
  $('selectionLabelName').textContent=names.join(' + ');$('selectionLabelName').title=names.join(' + ');
  $('selectionLabelKind').textContent=selectedIds.length===1?`已选${kindLabel(nodes.get(selectedIds[0]))}`:`已选 ${selectedIds.length} 个对象`;
  $('selectionLabelDetail').textContent=selectionHighlight.visibleCount?`青蓝色为所属区域 · ${selectionHighlight.totalCount.toLocaleString()} 个构件${selectionHighlight.visibleCount<selectionHighlight.totalCount?'（部分已隐藏或剖切）':''}`:'此区域当前不可见，请检查隐藏、屋顶及剖切设置';
}
function setSelection(ids, focus=false) {
  selectedIds=[...new Set(ids)].filter(id=>nodes.has(id));
  selectedIds=selectedIds.filter(id=>{let p=nodes.get(id).data.parentId;while(p){if(selectedIds.includes(p))return false;p=nodes.get(p)?.data.parentId;}return true;});
  syncInstances();refreshSelection();renderTree();
  if(focus)frameSelection();
}
function clickedSelection(id, additive=false) {
  if(additive){const next=selectedIds.includes(id)?selectedIds.filter(i=>i!==id):[...selectedIds,id];setSelection(next);}
  else setSelection([id]);
}
function resetProxy() {
  if(!selectedIds.length){transform.detach();proxy.visible=false;return;}
  proxy.visible=true;proxy.position.copy(selectionBounds().getCenter(new THREE.Vector3()));proxy.scale.set(1,1,1);
  if(selectedIds.length===1)nodes.get(selectedIds[0]).object.getWorldQuaternion(proxy.quaternion);else proxy.quaternion.identity();
  proxy.updateMatrixWorld(true);transform.attach(proxy);transform.setMode(currentMode);
  transform.setSpace($('spaceMode').value);transform.enabled=true;requestRender();
}
function startDrag() {
  if(!selectedIds.length)return;
  proxy.updateMatrixWorld(true);
  dragContext={before:stateSnapshot(),proxyInverse:proxy.matrixWorld.clone().invert(),records:selectedIds.map(id=>{const r=nodes.get(id);r.object.updateWorldMatrix(true,false);return {record:r,world:r.object.matrixWorld.clone(),parentInverse:r.object.parent.matrixWorld.clone().invert()};})};
}
function updateDrag() {
  if(!dragContext)return;
  proxy.scale.set(...proxy.scale.toArray().map(v=>Math.max(.001,v)));proxy.updateMatrixWorld(true);
  const delta=new THREE.Matrix4().multiplyMatrices(proxy.matrixWorld,dragContext.proxyInverse);
  for(const entry of dragContext.records) {
    const local=new THREE.Matrix4().multiplyMatrices(entry.parentInverse,delta).multiply(entry.world);
    local.decompose(entry.record.object.position,entry.record.object.quaternion,entry.record.object.scale);
    entry.record.object.updateMatrix();
  }
  syncInstances();updateTransformFields();
}
function finishDrag() {
  if(!dragContext)return;
  const before=dragContext.before;for(const {record}of dragContext.records)captureNodeTransform(record);
  dragContext=null;pushHistory(before);refreshSelection();requestRender();
}
function scopeTarget(record) {
  const scope=$('selectionScope').value;
  const candidates=[];let n=record;while(n){candidates.push(n);n=nodes.get(n.data.parentId);}
  if(scope==='component')return candidates.find(r=>['wall','door'].includes(normalizeKind(r))&&r.children.length>0)||candidates.find(r=>['wall','door'].includes(normalizeKind(r)))||record;
  const kinds=scope==='room'?['room']:scope==='district'?['suite']:scope==='region'?['region']:['level'];
  const match=candidates.find(r=>kinds.includes(normalizeKind(r)));
  if(match)return match;
  if(scope==='room'){
    toast('此处没有独立房间组，已选构件；可用 Shift 多选墙和地板一起调整。',6500);
    return candidates.find(r=>['wall','door'].includes(normalizeKind(r))&&r.children.length>0)||record;
  }
  return candidates.find(r=>r.children.length>0&&r.id!==roots[0]?.id) || record;
}
function pick(event) {
  const rect=renderer.domElement.getBoundingClientRect();pointer.set(((event.clientX-rect.left)/rect.width)*2-1,-((event.clientY-rect.top)/rect.height)*2+1);
  raycaster.setFromCamera(pointer,camera);
  const planes=activePlanes();
  const hits=raycaster.intersectObjects(meshes,false);
  const hit=hits.find(h=>{const record=h.object.userData.batch.records[h.instanceId];return record?.visible&&planes.every(p=>p.distanceToPoint(h.point)>=-.0001);});
  if(hit){const record=scopeTarget(hit.object.userData.batch.records[hit.instanceId]);clickedSelection(record.id,event.shiftKey);}
  else if(!event.shiftKey)setSelection([]);
}
function frameBounds(box,preset=view.preset) {
  const center=box.getCenter(new THREE.Vector3()),size=box.getSize(new THREE.Vector3());
  let direction=preset==='top'?new THREE.Vector3(0,1,.0001):preset==='front'?new THREE.Vector3(0,.06,1):new THREE.Vector3(.9,.85,1.1);
  direction.normalize();const fov=THREE.MathUtils.degToRad(camera.fov),tan=Math.tan(fov/2);
  const right=new THREE.Vector3().crossVectors(new THREE.Vector3(0,1,0),direction).normalize(),up=new THREE.Vector3().crossVectors(direction,right).normalize();
  let distance=4;
  for(const x of [box.min.x,box.max.x])for(const y of [box.min.y,box.max.y])for(const z of [box.min.z,box.max.z]){const corner=new THREE.Vector3(x,y,z).sub(center),depth=corner.dot(direction);distance=Math.max(distance,Math.abs(corner.dot(right))/(tan*camera.aspect)+depth,Math.abs(corner.dot(up))/tan+depth);}
  distance*=1.12;
  camera.position.copy(center).addScaledVector(direction,distance);camera.near=Math.max(.05,distance/10000);camera.far=Math.max(2000,distance*15);camera.updateProjectionMatrix();
  orbit.target.copy(center);orbit.maxDistance=Math.max(2500,distance*6);orbit.update();requestRender();
}
function frameSelection(){if(selectedIds.length)frameBounds(selectionBounds());else frameBounds(visibleBounds());}
function activePlanes() {
  const planes=[];
  if(view.section)planes.push(new THREE.Plane(new THREE.Vector3(0,-1,0),view.sectionHeight));
  if(view.floor==='B1')planes.push(new THREE.Plane(new THREE.Vector3(0,-1,0),-.05));
  if(view.floor==='F1'){planes.push(new THREE.Plane(new THREE.Vector3(0,1,0),.05));planes.push(new THREE.Plane(new THREE.Vector3(0,-1,0),5.95));}
  if(view.floor==='F2')planes.push(new THREE.Plane(new THREE.Vector3(0,1,0),-5.95));
  return planes;
}
function applyView() {
  const planes=activePlanes();for(const material of materials){material.clippingPlanes=planes;material.clipShadows=true;material.needsUpdate=true;}
  selectionMaterial.clippingPlanes=planes;selectionMaterial.needsUpdate=true;
  grid.visible=view.grid;syncInstances();
  $('viewLabel').querySelector('b').textContent=view.floor==='all'?'宫殿总览':view.floor==='B1'?'负一层 · 地下空间':view.floor==='F1'?'一楼 · 大厅与套院':'二楼及错层 · 上部空间';
  $('viewLabel').querySelector('span').textContent=`${view.section?`剖切 +${view.sectionHeight.toFixed(1)} m`:'透视 / 真实模型'}${view.roof?' · 屋顶显示':' · 屋顶暂隐'}`;
  updateVisibilityHint();requestRender();
}

function renderTree() {
  if(!initialized)return;
  const query=$('treeSearch').value.trim().toLowerCase();let allowed=null;
  if(query){allowed=new Set();for(const r of nodes.values())if(`${nodeName(r)} ${r.data.path} ${kindLabel(r)}`.toLowerCase().includes(query)){let n=r;while(n){allowed.add(n.id);n=nodes.get(n.data.parentId);}}}
  const fragment=document.createDocumentFragment();let rows=0,cut=false;
  function append(record,depth) {
    if(allowed&&!allowed.has(record.id))return;if(rows>=treeLimit){cut=true;return;}rows++;
    const row=document.createElement('div');row.className=`tree-row${selectedIds.includes(record.id)?' selected':''}${project.overrides[record.id]?' edited':''}${ownHidden(record)?' muted':''}`;row.dataset.nodeId=record.id;row.setAttribute('role','treeitem');row.setAttribute('aria-selected',String(selectedIds.includes(record.id)));row.tabIndex=-1;row.style.paddingLeft=`${depth*13+3}px`;row.title=record.data.path;
    const hasChildren=record.children.length>0,isExpanded=!!query||expanded.has(record.id);
    if(hasChildren)row.setAttribute('aria-expanded',String(isExpanded));
    const expander=document.createElement('button');expander.className=`tree-expander${hasChildren?'':' blank'}`;expander.textContent=isExpanded?'▾':'▸';expander.tabIndex=-1;expander.setAttribute('aria-label',isExpanded?'收起':'展开');expander.addEventListener('click',e=>{e.stopPropagation();expanded.has(record.id)?expanded.delete(record.id):expanded.add(record.id);renderTree();});
    const icon=document.createElement('span');icon.className='tree-icon';icon.textContent=normalizeKind(record)==='wall'?'▥':normalizeKind(record)==='door'?'▯':hasChildren?'◇':'▫';
    const name=document.createElement('span');name.className='tree-name';name.textContent=nodeName(record);
    const kind=document.createElement('span');kind.className='tree-kind';kind.textContent=kindLabel(record);
    row.append(expander,icon,name,kind);row.addEventListener('click',e=>clickedSelection(record.id,e.shiftKey));row.addEventListener('dblclick',()=>{setSelection([record.id],true);});fragment.append(row);
    if(hasChildren&&isExpanded)for(const child of record.children)append(nodes.get(child),depth+1);
  }
  roots.forEach(r=>append(r,0));
  if(!rows){const e=document.createElement('div');e.className='tree-empty';e.textContent='没有找到匹配的建筑对象。';fragment.append(e);}
  if(cut){const more=document.createElement('button');more.className='tree-more';more.textContent='显示更多层级';more.onclick=()=>{treeLimit+=900;renderTree();};fragment.append(more);}
  $('tree').replaceChildren(fragment);
}
function revealSelection() {
  if(!selectedIds.length)return;
  $('treeSearch').value='';for(const id of selectedIds){let n=nodes.get(id);while(n){expanded.add(n.id);n=nodes.get(n.data.parentId);}}
  treeLimit=Math.max(treeLimit,nodes.size);renderTree();
  requestAnimationFrame(()=>[...$('tree').querySelectorAll('[data-node-id]')].find(el=>el.dataset.nodeId===selectedIds[0])?.scrollIntoView({block:'center'}));
}
function buildTransformFields() {
  for(const [field,label]of [['position','位置'],['rotation','旋转'],['scale','缩放']]){
    const row=document.createElement('div');row.className='transform-row';const title=document.createElement('label');title.textContent=label;const fields=document.createElement('div');fields.className='xyz-fields';
    for(let axis=0;axis<3;axis++){
      const wrap=document.createElement('label'),tag=document.createElement('span'),input=document.createElement('input');tag.textContent=['X','Y','Z'][axis];input.type='number';input.step=field==='rotation'?'1':'0.1';input.dataset.field=field;input.dataset.axis=axis;input.setAttribute('aria-label',`${label} ${tag.textContent}`);input.addEventListener('change',applyField);input.addEventListener('keydown',e=>{if(e.key==='Enter')input.blur();});wrap.append(tag,input);fields.append(wrap);
    }
    row.append(title,fields);$('transformFields').append(row);
  }
}
function updateTransformFields() {
  if(!selectedIds.length)return;
  const object=selectedIds.length===1?nodes.get(selectedIds[0]).object:proxy;
  const q=new THREE.Quaternion(-object.quaternion.x,-object.quaternion.y,object.quaternion.z,object.quaternion.w),euler=new THREE.Euler().setFromQuaternion(q,'YXZ');
  const values={position:toUnityPosition(object.position),rotation:[euler.x,euler.y,euler.z].map(v=>THREE.MathUtils.radToDeg(v)),scale:object.scale.toArray()};
  for(const input of $('transformFields').querySelectorAll('input'))if(document.activeElement!==input)input.value=Number(values[input.dataset.field][Number(input.dataset.axis)].toFixed(3));
  const size=selectionBounds().getSize(new THREE.Vector3());$('objectDimensions').textContent=`${size.x.toFixed(2)} × ${size.y.toFixed(2)} × ${size.z.toFixed(2)} m`;
}
function applyField(event) {
  if(!selectedIds.length)return;const input=event.target,value=Number(input.value),field=input.dataset.field,axis=Number(input.dataset.axis);
  if(!Number.isFinite(value)||(field==='scale'&&(value<.001||value>10000))){toast('请输入有效数值；缩放必须大于 0 且不超过 10000。');updateTransformFields();return;}
  const before=stateSnapshot();const object=selectedIds.length===1?nodes.get(selectedIds[0]).object:proxy;
  if(selectedIds.length>1)startDrag();
  if(field==='position'){const arr=toUnityPosition(object.position);arr[axis]=value;object.position.copy(unityVector(arr));}
  else if(field==='scale'){const arr=object.scale.toArray();arr[axis]=value;object.scale.fromArray(arr);}
  else {const q=new THREE.Quaternion(-object.quaternion.x,-object.quaternion.y,object.quaternion.z,object.quaternion.w),e=new THREE.Euler().setFromQuaternion(q,'YXZ'),arr=[e.x,e.y,e.z];arr[axis]=THREE.MathUtils.degToRad(value);q.setFromEuler(new THREE.Euler(...arr,'YXZ'));object.quaternion.copy(unityQuaternion(q.toArray()));}
  object.updateMatrixWorld(true);
  if(selectedIds.length>1){updateDrag();finishDrag();}
  else {captureNodeTransform(nodes.get(selectedIds[0]));syncInstances();pushHistory(before);refreshSelection();}
}
function refreshSelection() {
  const has=!!selectedIds.length,multiple=selectedIds.length>1;
  $('noSelection').classList.toggle('hidden',has);$('selectionProperties').classList.toggle('hidden',!has);$('emptySelection').classList.toggle('hidden',has);$('selectionCrumbs').classList.toggle('hidden',!has);
  $('selectionStatus').textContent=has?multiple?`已选择 ${selectedIds.length} 个对象`:nodeName(nodes.get(selectedIds[0])):'未选择对象';
  if(!has){resetProxy();return;}
  const record=nodes.get(selectedIds[0]);$('objectKind').textContent=multiple?'联合选择':kindLabel(record);$('objectId').textContent=multiple?`${selectedIds.length} 个对象`:`构件 ${String(record.ordinal).padStart(4,'0')}`;$('objectId').title=record.id;
  $('objectName').value=multiple?`${selectedIds.length} 个对象联合变换`:nodeName(record);$('objectName').disabled=multiple;
  $('objectPath').textContent=multiple?'Shift + 单击可增减选择；变换围绕共同中心进行。':record.data.path;
  $('parentBtn').disabled=multiple||!record.data.parentId;
  $('objectNote').value=multiple?'':project.overrides[record.id]?.note||'';$('objectNote').disabled=multiple;
  $('hideBtn').textContent=selectedIds.every(id=>ownHidden(nodes.get(id)))?'显示':'隐藏';
  $('selectionProperties').querySelector('.section-heading span').textContent=multiple?'联合变换 · 世界坐标':'Unity 坐标 · 米 / 度';
  const crumbs=[];let n=record;while(n){crumbs.unshift(n);n=nodes.get(n.data.parentId);}
  $('selectionCrumbs').replaceChildren();
  crumbs.forEach((r,i)=>{if(i){const s=document.createElement('span');s.textContent='/';$('selectionCrumbs').append(s);}const b=document.createElement('button');b.textContent=nodeName(r);b.title=r.data.path;b.onclick=()=>setSelection([r.id]);$('selectionCrumbs').append(b);});
  $('childCount').textContent=multiple?'—':`${record.children.length} 个`;$('childrenList').replaceChildren();
  if(!multiple)for(const id of record.children.slice(0,80)){const c=nodes.get(id),b=document.createElement('button');b.textContent=nodeName(c);const span=document.createElement('span');span.textContent=kindLabel(c);b.append(span);b.onclick=()=>setSelection([id]);$('childrenList').append(b);}
  if(!multiple&&record.children.length>80){const b=document.createElement('button');b.textContent='在左侧查看全部子对象';b.onclick=revealSelection;$('childrenList').append(b);}
  resetProxy();updateTransformFields();updateVisibilityHint();
}
function updateVisibilityHint() {
  if(!selectedIds.length)return;
  const record=nodes.get(selectedIds[0]);let message='';
  if(ownHidden(record))message='此对象已隐藏；点击“显示”恢复。';
  else if(!baseVisibility(record))message='此对象受父级隐藏或屋顶查看开关影响。';
  else if(view.autoWalls&&autoHiddenWalls.has(record.id))message='这面近侧墙正临时隐藏；关闭“自动隐藏近侧墙”可恢复。';
  else if(view.floor!=='all'||view.section)message='楼层筛选 / 剖切可能遮掉部分对象，变换仍作用于完整对象。';
  $('visibilityHint').textContent=message;$('visibilityHint').classList.toggle('hidden',!message);
}
function mutateSelected(callback) {
  if(!selectedIds.length)return;const before=stateSnapshot();for(const id of selectedIds)callback(nodes.get(id));
  applyAllOverrides();pushHistory(before);refreshSelection();
}
function toggleHidden() {const shouldHide=!selectedIds.every(id=>ownHidden(nodes.get(id)));mutateSelected(r=>setOverride(r.id,'hidden',shouldHide));}
function updateChangeList() {
  const entries=Object.entries(project.overrides);$('changeCount').textContent=entries.length;$('changesList').replaceChildren();
  if(!entries.length){const empty=document.createElement('p');empty.className='small-note';empty.textContent='还没有修改。拖动建筑构件或输入属性后，调整会出现在这里。';$('changesList').append(empty);}
  const labels={position:'位置',rotation:'旋转',scale:'缩放',name:'名称',hidden:'可见性',note:'批注'};
  for(const [id,override]of entries){const record=nodes.get(id);if(!record)continue;const item=document.createElement('div');item.className='change-item';item.dataset.changeId=id;const button=document.createElement('button');button.textContent=nodeName(record);button.onclick=()=>setSelection([id],true);const tags=document.createElement('div');tags.className='change-tags';for(const key of Object.keys(override)){const s=document.createElement('span');s.textContent=key==='hidden'?(override.hidden?'已隐藏':'已显示'):labels[key]||key;tags.append(s);}item.append(button,tags);if(override.note){const p=document.createElement('p');p.textContent=override.note;item.append(p);}$('changesList').append(item);}
}
function setTab(tab) {document.querySelectorAll('[data-tab]').forEach(b=>b.classList.toggle('active',b.dataset.tab===tab));$('propertiesTab').classList.toggle('hidden',tab!=='properties');$('changesTab').classList.toggle('hidden',tab!=='changes');}
function setMode(mode){currentMode=mode;if(transform)transform.setMode(mode);document.querySelectorAll('[data-tool]').forEach(b=>b.classList.toggle('active',b.dataset.tool===mode));requestRender();}
function inspectNode(id){const r=nodes.get(id);if(!r)return null;return {id,name:nodeName(r),position:toUnityPosition(r.object.position),rotation:toUnityQuaternion(r.object.quaternion),scale:r.object.scale.toArray(),matrixWorld:r.object.matrixWorld.toArray(),instanceMatrix:r.batch?(()=>{const m=new THREE.Matrix4();r.batch.mesh.getMatrixAt(r.instance,m);return m.toArray();})():null,geometryMatrix:r.geometryMatrix.toArray(),bounds:{min:objectBounds(r).min.toArray(),max:objectBounds(r).max.toArray()}};}
function downloadBlob(blob,name){const link=document.createElement('a');link.href=URL.createObjectURL(blob);link.download=name;link.click();setTimeout(()=>URL.revokeObjectURL(link.href),1000);}
function exportProject(data=project){const payload={schemaVersion:1,sceneId:model.sceneId,revision:currentServerRevision,exportedAt:new Date().toISOString(),overrides:data.overrides,notes:data.notes||''};downloadBlob(new Blob([JSON.stringify(payload,null,2)],{type:'application/json'}),`royal-design-${new Date().toISOString().replace(/[:.]/g,'-')}.json`);toast('修改 JSON 已导出');}
async function importProject(file){try{const data=JSON.parse(await file.text());validateProject(data);if(changed()&&!confirm('导入会替换当前修改，仍可使用撤销返回。继续吗？'))return;const before=stateSnapshot();project.overrides=clone(data.overrides);project.notes=data.notes||'';applyAllOverrides();pushHistory(before);refreshSelection();$('projectNotes').value=project.notes;toast('已导入设计；将自动保存到当前项目。');}catch(error){toast(`导入失败：${error.message}`,8000);}}
function exportPNG(){const helper=transform.getHelper?transform.getHelper():transform;const was=helper.visible,boxVisible=selectionBox.visible;helper.visible=false;selectionBox.visible=false;renderer.render(scene,camera);renderer.domElement.toBlob(blob=>{if(blob)downloadBlob(blob,`royal-view-${Date.now()}.png`);else toast('图像导出失败，请重新尝试。');},'image/png');helper.visible=was;selectionBox.visible=boxVisible;requestRender();}

function setupScene(){
  scene=new THREE.Scene();scene.background=new THREE.Color('#eae9e3');
  camera=new THREE.PerspectiveCamera(38,1,.05,5000);camera.position.set(150,160,180);
  renderer=new THREE.WebGLRenderer({antialias:true,preserveDrawingBuffer:true,powerPreference:'high-performance'});renderer.setPixelRatio(Math.min(devicePixelRatio,1.5));renderer.outputColorSpace=THREE.SRGBColorSpace;renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.1;renderer.localClippingEnabled=true;
  renderer.domElement.tabIndex=0;renderer.domElement.setAttribute('aria-label','三维宫殿，点击选择，拖动环绕');$('viewport').prepend(renderer.domElement);
  scene.add(new THREE.HemisphereLight(0xfff8e9,0x75858a,2.2));const sun=new THREE.DirectionalLight(0xffeed3,2.4);sun.position.set(-70,140,90);scene.add(sun);const fill=new THREE.DirectionalLight(0xdfe9ff,.65);fill.position.set(80,60,-80);scene.add(fill);
  orbit=new OrbitControls(camera,renderer.domElement);orbit.enableDamping=true;orbit.dampingFactor=.12;orbit.screenSpacePanning=true;orbit.minDistance=.35;orbit.maxPolarAngle=Math.PI*.98;orbit.addEventListener('change',()=>{autoWallsDirty=true;requestRender();});
  proxy=new THREE.Object3D();proxy.name='Selection center';scene.add(proxy);
  transform=new TransformControls(camera,renderer.domElement);transform.setSize(.85);scene.add(transform.getHelper?transform.getHelper():transform);
  transform.addEventListener('dragging-changed',event=>{orbit.enabled=!event.value;requestRender();});transform.addEventListener('mouseDown',startDrag);transform.addEventListener('objectChange',updateDrag);transform.addEventListener('mouseUp',finishDrag);transform.addEventListener('change',requestRender);
  selectionMaterial=new THREE.MeshBasicMaterial({color:0x15c9ef,transparent:true,opacity:.62,side:THREE.DoubleSide,depthWrite:false,polygonOffset:true,polygonOffsetFactor:-2,polygonOffsetUnits:-2,toneMapped:false});selectionMaterial.forceSinglePass=true;
  selectionBox=new THREE.Box3Helper(new THREE.Box3(),0x009ac1);selectionBox.material.depthTest=false;selectionBox.material.transparent=true;selectionBox.material.opacity=.9;selectionBox.material.toneMapped=false;selectionBox.renderOrder=20;selectionBox.visible=false;scene.add(selectionBox);
  grid=new THREE.GridHelper(500,100,0xb9b8aa,0xd2d1c6);grid.material.transparent=true;grid.material.opacity=.35;grid.position.y=-.04;scene.add(grid);
  renderer.domElement.addEventListener('pointerdown',event=>{pointerDown={x:event.clientX,y:event.clientY,button:event.button,gizmo:!!transform.axis};});
  renderer.domElement.addEventListener('pointerup',event=>{const start=pointerDown;pointerDown=null;if(start&&start.button===0&&!start.gizmo&&!transform.dragging&&Math.hypot(event.clientX-start.x,event.clientY-start.y)<5)pick(event);});
  renderer.domElement.addEventListener('dblclick',event=>{if(!transform.axis){pick(event);frameSelection();}});
  renderer.domElement.addEventListener('contextmenu',event=>event.preventDefault());
  renderer.domElement.addEventListener('webglcontextlost',event=>{event.preventDefault();showBanner('显卡连接暂时中断。当前修改已在浏览器备份，请刷新页面恢复视图。');});
  new ResizeObserver(resize).observe($('viewport'));resize();requestRender();
  $('selectionLabelFocus').onclick=frameSelection;
}
function resize(){if(!renderer)return;const rect=$('viewport').getBoundingClientRect();if(rect.width<=0||rect.height<=0)return;camera.aspect=rect.width/rect.height;camera.updateProjectionMatrix();renderer.setSize(rect.width,rect.height,false);autoWallsDirty=true;requestRender();}
function requestRender(){needsRender=true;if(!frameRequested){frameRequested=true;requestAnimationFrame(render);}}
function render(){frameRequested=false;if(!renderer)return;orbit.update();if(initialized&&autoWallsDirty&&(view.autoWalls||autoHiddenWalls.size)){if(performance.now()-lastAutoWallUpdate>=75){lastAutoWallUpdate=performance.now();updateAutoWalls();}else requestRender();}if(needsRender){renderer.render(scene,camera);needsRender=false;}if(transform.dragging)requestRender();}

function bindUI(){
  $('objectNote').maxLength=4000;$('projectNotes').maxLength=20000;
  const panelButtonState=()=>{ $('toggleHierarchy').setAttribute('aria-pressed',String(!document.body.classList.contains('hide-left')));$('toggleInspector').setAttribute('aria-pressed',String(!document.body.classList.contains('hide-right'))); };
  if(innerWidth<=760)document.body.classList.add('hide-left','hide-right');panelButtonState();
  $('toggleHierarchy').onclick=()=>{document.body.classList.toggle('hide-left');if(innerWidth<=760&&!document.body.classList.contains('hide-left'))document.body.classList.add('hide-right');panelButtonState();requestAnimationFrame(resize);};
  $('toggleInspector').onclick=()=>{document.body.classList.toggle('hide-right');if(innerWidth<=760&&!document.body.classList.contains('hide-right'))document.body.classList.add('hide-left');panelButtonState();requestAnimationFrame(resize);};
  let previousNarrow=innerWidth<=760;addEventListener('resize',()=>{const narrow=innerWidth<=760;if(narrow&&!previousNarrow){document.body.classList.add('hide-left','hide-right');panelButtonState();}previousNarrow=narrow;});
  buildTransformFields();document.querySelectorAll('[data-tool]').forEach(b=>b.onclick=()=>setMode(b.dataset.tool));document.querySelectorAll('[data-tab]').forEach(b=>b.onclick=()=>setTab(b.dataset.tab));
  $('undoBtn').onclick=undo;$('redoBtn').onclick=redo;$('saveBtn').onclick=saveProject;$('retryBtn').onclick=()=>{clearBanner();saveProject();};$('loadServerBtn').onclick=loadServerVersion;$('conflictExportBtn').onclick=()=>exportProject();
  $('filesBtn').onclick=()=>{const open=$('fileMenu').classList.toggle('hidden');$('filesBtn').setAttribute('aria-expanded',String(!open));};
  document.addEventListener('click',event=>{if(!event.target.closest('#fileMenu')&&!event.target.closest('#filesBtn')){$('fileMenu').classList.add('hidden');$('filesBtn').setAttribute('aria-expanded','false');}});
  $('exportBtn').onclick=()=>{exportProject();$('fileMenu').classList.add('hidden');};$('importBtn').onclick=()=>{$('importFile').click();$('fileMenu').classList.add('hidden');};$('pngBtn').onclick=()=>{exportPNG();$('fileMenu').classList.add('hidden');};
  $('importFile').onchange=event=>{const file=event.target.files[0];if(file)importProject(file);event.target.value='';};
  $('helpBtn').onclick=()=>$('helpDialog').showModal();document.querySelectorAll('[data-close]').forEach(b=>b.onclick=()=>b.closest('dialog').close());
  $('treeSearch').oninput=()=>{clearTimeout(treeSearchTimer);treeSearchTimer=setTimeout(()=>{treeLimit=900;renderTree();},150);};
  $('collapseTree').onclick=()=>{expanded.clear();roots.forEach(r=>expanded.add(r.id));renderTree();};$('revealSelection').onclick=revealSelection;
  $('selectRootBtn').onclick=()=>{if(roots.length)setSelection([roots[0].id]);};$('focusBtn').onclick=frameSelection;$('frameAll').onclick=()=>frameBounds(visibleBounds());
  $('parentBtn').onclick=()=>{if(selectedIds.length===1){const p=nodes.get(selectedIds[0]).data.parentId;if(p)setSelection([p]);}};$('hideBtn').onclick=toggleHidden;
  $('resetTransformBtn').onclick=()=>mutateSelected(r=>{for(const k of ['position','rotation','scale'])setOverride(r.id,k,undefined);});$('resetObjectBtn').onclick=()=>mutateSelected(r=>{delete project.overrides[r.id];});
  $('objectName').onchange=()=>{if(selectedIds.length===1){const name=$('objectName').value.trim();if(!name){refreshSelection();return;}mutateSelected(r=>setOverride(r.id,'name',name));}};
  for(const element of [$('objectNote'),$('projectNotes')]){element.addEventListener('focus',()=>beginTextEdit(element));element.addEventListener('input',()=>applyTextInput(element));element.addEventListener('blur',finishTextEdit);}
  $('showAllBtn').onclick=()=>{const before=stateSnapshot();for(const [id,override]of Object.entries(project.overrides))if(override.hidden)setOverride(id,'hidden',false);applyAllOverrides();pushHistory(before);refreshSelection();};
  $('spaceMode').onchange=()=>{transform.setSpace($('spaceMode').value);requestRender();};
  const snap=()=>{const on=$('snapToggle').checked;transform.setTranslationSnap(on?Number($('snapStep').value):null);transform.setRotationSnap(on?Math.PI/12:null);transform.setScaleSnap(on?.1:null);};$('snapToggle').onchange=snap;$('snapStep').onchange=snap;
  $('floorFilter').onchange=()=>{view.floor=$('floorFilter').value;applyView();};$('roofToggle').onchange=()=>{view.roof=$('roofToggle').checked;applyView();};$('gridToggle').onchange=()=>{view.grid=$('gridToggle').checked;applyView();};$('sectionToggle').onchange=()=>{view.section=$('sectionToggle').checked;applyView();};
  $('autoWallsToggle').onchange=()=>{view.autoWalls=$('autoWallsToggle').checked;autoWallsDirty=true;updateAutoWalls();requestRender();};
  const section=(event)=>{const value=Number(event.target.value);if(!Number.isFinite(value))return;view.sectionHeight=value;$('sectionHeight').value=value;$('sectionValue').value=value.toFixed(1);if(!view.section){view.section=true;$('sectionToggle').checked=true;}applyView();};$('sectionHeight').oninput=section;$('sectionValue').onchange=section;
  document.querySelectorAll('[data-view]').forEach(b=>b.onclick=()=>{view.preset=b.dataset.view;document.querySelectorAll('[data-view]').forEach(x=>x.classList.toggle('active',x===b));frameBounds(selectedIds.length?selectionBounds():visibleBounds());});
  document.addEventListener('keydown',event=>{
    const typing=/INPUT|TEXTAREA|SELECT/.test(document.activeElement?.tagName)||document.activeElement?.isContentEditable;
    if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='s'){event.preventDefault();document.activeElement?.blur();saveProject();return;}
    if(typing||document.querySelector('dialog[open]')||!initialized)return;
    const key=event.key.toLowerCase();
    if(event.ctrlKey||event.metaKey){if(key==='z'){event.preventDefault();event.shiftKey?redo():undo();}else if(key==='y'){event.preventDefault();redo();}return;}
    if(key==='w')setMode('translate');else if(key==='e')setMode('rotate');else if(key==='r')setMode('scale');else if(key==='f'){event.preventDefault();frameSelection();}else if(key==='h')toggleHidden();else if(key==='escape'){if(dragContext){const before=dragContext.before;dragContext=null;project.overrides=before.overrides;project.notes=before.notes;applyAllOverrides();refreshSelection();orbit.enabled=true;}else setSelection([]);}
  });
  addEventListener('beforeunload',event=>{writeBackup();if(changed()){event.preventDefault();event.returnValue='';}});
  document.addEventListener('visibilitychange',()=>{if(document.visibilityState==='hidden'&&changed()&&!conflict)saveProject();});
  $('downloadBackup').onclick=()=>{if(pendingBackup)exportProject(pendingBackup);};$('discardBackup').onclick=()=>{pendingBackup=null;$('backupDialog').close();writeBackup();};
  $('restoreBackup').onclick=()=>{if(!pendingBackup)return;const before=stateSnapshot();project.overrides=clone(pendingBackup.overrides);project.notes=pendingBackup.notes||'';applyAllOverrides();pushHistory(before);refreshSelection();$('projectNotes').value=project.notes;pendingBackup=null;$('backupDialog').close();toast('已恢复浏览器备份，准备保存。');};
}

async function initialize(){
  try {
    bindUI();setupScene();
    const [data,saved]=await Promise.all([readJSON('model/scene.json'),readJSON('/api/project')]);model=data;project=saved;project.overrides||={};project.notes||='';
    $('loadingText').textContent=`读取 ${model.nodes.length.toLocaleString()} 个建筑对象…`;
    const response=await fetch(`model/${model.buffer||'geometry.bin'}`);if(!response.ok)throw new Error(`模型几何文件读取失败：${response.status}`);const buffer=await response.arrayBuffer();
    geometries=model.geometries.map(g=>buildGeometry(g,buffer));createMaterials();
    for(const data of model.nodes){const object=new THREE.Object3D();object.name=data.name;object.position.copy(unityVector(data.position));object.quaternion.copy(unityQuaternion(data.rotation));object.scale.fromArray(data.scale);object.userData.nodeId=data.id;const geometryMatrix=new THREE.Matrix4().compose(unityVector(data.geometryOffset||[0,0,0]),new THREE.Quaternion(),new THREE.Vector3().fromArray(data.geometryScale||[1,1,1]));nodes.set(data.id,{id:data.id,ordinal:nodes.size+1,data,object,geometryMatrix,children:[],geometry:null,batch:null,visible:true});}
    for(const record of nodes.values()){const parent=nodes.get(record.data.parentId);if(parent){parent.object.add(record.object);parent.children.push(record.id);}else{scene.add(record.object);roots.push(record);}}
    for(const record of nodes.values())record.roof=isRoof(record);
    roots.forEach(r=>{expanded.add(r.id);for(const id of r.children)if(nodes.get(id).children.length<35)expanded.add(id);});
    validateProject(project);currentServerRevision=project.revision||0;lastSavedAt=project.updatedAt;backupKey=`royal-architecture-${model.sceneId}`;
    applyAllOverrides();buildBatches();
    modelBounds=visibleBounds();maxModelHeight=modelBounds.max.y;
    $('sectionHeight').min=Math.floor(modelBounds.min.y-2);$('sectionHeight').max=Math.ceil(maxModelHeight+2);view.sectionHeight=Math.ceil(maxModelHeight+1);$('sectionHeight').value=view.sectionHeight;$('sectionValue').value=view.sectionHeight;
    const extent=modelBounds.getSize(new THREE.Vector3());grid.scale.setScalar(Math.max(1,Math.ceil(Math.max(extent.x,extent.z)/500)));grid.position.y=Math.min(-.04,modelBounds.min.y-.04);
    initialized=true;document.body.dataset.ready='true';$('nodeCount').textContent=nodes.size.toLocaleString();$('revisionText').textContent=`版本 ${currentServerRevision}`;$('modelStatus').textContent=`${nodes.size.toLocaleString()} 对象 · ${meshes.reduce((n,m)=>n+m.count,0).toLocaleString()} 构件`;
    $('projectNotes').value=project.notes;applyView();renderTree();updateChangeList();updateHistory();frameBounds(modelBounds);$('loading').classList.add('hidden');setStatus('已连接 · 修改自动保存', 'saved');
    try{const backup=JSON.parse(localStorage.getItem(backupKey)||'null');if(backup?.pending){validateProject(backup);if(!snapshotEqual({overrides:backup.overrides,notes:backup.notes||''},stateSnapshot())){pendingBackup=backup;$('backupMessage').textContent=backup.revision===currentServerRevision?'发现浏览器里尚未同步的修改。可以恢复后继续编辑，也可以使用已保存的服务器版本。':'浏览器备份与服务器版本已不同。请先导出备份；为避免覆盖其他编辑，恢复按钮暂不可用。';$('restoreBackup').disabled=backup.revision!==currentServerRevision;$('backupDialog').showModal();}}}catch(error){console.warn('浏览器备份读取提示：',error.message);}
    if(!pendingBackup)writeBackup();
    window.RoyalEditor={
      getState:()=>({ready:initialized,sceneId:model.sceneId,revision:currentServerRevision,dirty:changed(),saveInFlight,conflict,selectedIds:[...selectedIds],view:{...view},nodeCount:nodes.size,instanceCount:meshes.reduce((n,m)=>n+m.count,0),visibleCount:[...nodes.values()].filter(r=>r.geometry&&r.visible).length,undoCount:undoStack.length,redoCount:redoStack.length,overrides:clone(project.overrides),notes:project.notes,selection:selectedIds.map(inspectNode),selectionProxy:{position:proxy.position.toArray(),quaternion:proxy.quaternion.toArray(),scale:proxy.scale.toArray()},camera:{position:camera.position.toArray(),target:orbit.target.toArray(),projectionMatrix:camera.projectionMatrix.toArray(),matrixWorldInverse:camera.matrixWorldInverse.toArray()},renderInfo:{calls:renderer.info.render.calls,triangles:renderer.info.render.triangles}}),
      getNodes:()=>[...nodes.values()].map(r=>({id:r.id,parentId:r.data.parentId,name:nodeName(r),kind:normalizeKind(r),geometry:r.data.geometry,children:r.children.length,path:r.data.path})),
      getNode:inspectNode,
      getAutoWallsState:()=>({enabled:view.autoWalls,hiddenIds:[...autoHiddenWalls],hiddenGroupIds:[...autoHiddenGroups],count:autoHiddenGroups.length,candidates:autoWallGroups.length,renderedHiddenCount:[...autoHiddenWalls].filter(id=>nodes.get(id)?.visible).length}),
      getSelectionHighlight:()=>({...selectionHighlight,nodeIds:[...selectionHighlight.nodeIds],color:selectionMaterial.color.getHexString(),clippingPlanes:selectionMaterial.clippingPlanes?.map(p=>[...p.normal.toArray(),p.constant])||[],renderedInstances:batches.reduce((n,b)=>n+(b.highlight?.visible?b.highlight.count:0),0)}),
    };
  } catch(error) {
    console.error(error);$('loadingText').textContent=`载入失败：${error.message}。请确认使用项目启动器打开。`;$('loading').querySelector('h2').textContent='暂时无法载入宫殿';$('loading').querySelector('.loading-line').style.display='none';setStatus('连接失败','error');
  }
}
initialize();

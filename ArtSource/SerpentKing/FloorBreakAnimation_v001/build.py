from pathlib import Path
import base64
R=Path(__file__).resolve().parent
def embed(name):return 'data:image/png;base64,'+base64.b64encode((R/name).read_bytes()).decode()
html='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>裂缝地砖 · 撞碎塌落</title>
<style>body{margin:0;background:#160c14;color:#d8bd91;font:16px/1.7 "Microsoft YaHei",sans-serif;padding:24px}main{max-width:1280px;margin:auto}canvas{width:100%;background:#24121e;border:1px solid #795231}section{display:flex;gap:18px;align-items:center;flex-wrap:wrap;padding:14px 0}button,select{background:#38202b;color:#ead4ac;border:1px solid #9c7945;padding:8px 16px}input{accent-color:#ba8c48}#seek{flex:1;min-width:240px}p{margin:6px 0;color:#b79b8f}</style>
<main><canvas id="stage" width="1280" height="720"></canvas><section><button id="play">暂停</button><button id="replay">重新播放</button><span id="clock"></span><input id="seek" type="range" min="0" max="5.5" step=".01" value="0"><span id="phase"></span></section>
<section><label><input id="detail" type="checkbox" checked>局部放大</label><label>播放速度 <select id="speed"><option value=".25">0.25×</option><option value=".5">0.5×</option><option selected value="1">1×</option></select></label></section>
<p>裂纹扩展 → 撞碎 → 石砾飞散与塌落 → 尘雾消散 → 洞口显露。5.5 秒循环，撞击在 1 秒。</p><p>独立效果预览；未接入游戏。</p></main><script>window.BEFORE='__BEFORE__';window.AFTER='__AFTER__';</script><script>__JS__</script></html>'''
html=html.replace('__BEFORE__',embed('Before.png')).replace('__AFTER__',embed('After.png')).replace('__JS__',(R/'animation.js').read_text('utf-8'))
(R/'FloorBreak.html').write_text(html,encoding='utf-8')
print('Offline floor destruction preview built.')

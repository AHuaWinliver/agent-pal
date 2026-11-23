"""Create an offline viewer and an animation proof from assembled PNG frames."""
import sys
from pathlib import Path
from PIL import Image

folder=Path(sys.argv[1]);pack=folder/'sun-wukong'
frames=[]
for index in range(24):
    canvas=Image.new('RGB',(480,270),'#f5f4ef')
    for col,state in enumerate(('running-right','punching')):
        image=Image.open(pack/state/f'{index:02}.png').convert('RGBA');image.thumbnail((230,250),Image.Resampling.LANCZOS)
        canvas.paste(image,(col*240+(240-image.width)//2,10),image)
    frames.append(canvas)
frames[0].save(folder/'wukong-walk-and-staff.gif',save_all=True,append_images=frames[1:],duration=55,loop=0,optimize=True)
html=r'''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Sun Wukong · animation preview</title><style>
:root{font:15px system-ui,-apple-system,"Segoe UI","Microsoft YaHei",sans-serif;color:#273b33;background:#f4f5f0}*{box-sizing:border-box}body{max-width:1080px;margin:40px auto;padding:0 24px}h1{font-size:30px;margin:4px 0}p{color:#6b776f;line-height:1.7}.layout{display:grid;grid-template-columns:minmax(340px,1fr) 320px;gap:24px;margin-top:28px}.stage,.controls{background:white;border:1px solid #dfe6dc;border-radius:20px;padding:24px;box-shadow:0 12px 40px #263b330a}.stage{display:flex;justify-content:center;align-items:center}.controls label{display:block;font-weight:600;margin:18px 0 8px}select,button{font:inherit;border:1px solid #d6dfd6;border-radius:10px;background:white;padding:10px 12px;width:100%;color:inherit}button{background:#2b6c58;color:white;cursor:pointer;margin:20px 0 8px}input[type=range]{width:100%}canvas{max-width:100%;height:auto;border-radius:12px}small{display:block;color:#748178;margin-top:16px;line-height:1.6}output{font-variant-numeric:tabular-nums}.check{display:flex;gap:10px;align-items:center}@media(max-width:760px){.layout{grid-template-columns:1fr}body{margin-top:20px}}
</style><h1>Sun Wukong</h1><p>Both arms counter-swing · continuous staff spinning · 24 walking phases<br>孙悟空：双臂交替摆动 · 连续棍花 · 离线动作预览</p>
<div class="layout"><div class="stage"><canvas id="canvas" width="384" height="470"></canvas></div><section class="controls"><label for="state">Animation</label><select id="state"></select><label for="fps">Playback speed · <output id="speed">24 fps</output></label><input id="fps" type="range" min="8" max="48" value="24"><label for="background">Background</label><select id="background"><option value="#ffffff">White</option><option value="#20352e">Dark jade</option><option value="#f4f1e9">Warm paper</option></select><label class="check"><input id="cloud" type="checkbox">Flying cloud</label><button id="play">Pause</button><output id="phase">Loading…</output><small>12 animation states · 240 RGBA PNG frames<br>Open this HTML directly; no server or network required.<br>Import the ZIP through Companions → Import character pack.</small></section></div>
<script>
const labels={'idle':'Idle / 待机','waiting':'Thinking / 思考','failed':'Disappointed / 失落','running-right':'Walk right / 向右行走','running-left':'Walk left / 向左行走','running':'Walk cycle / 步态','waving':'Wave / 挥手','review':'Review / 审阅','jumping':'Jump / 跳跃','punching':'Staff practice / 站立练棍','eating':'Eating / 吃文件','wiping-mouth':'Wipe mouth / 擦嘴'};
const choose=document.getElementById('state'),canvas=document.getElementById('canvas'),ctx=canvas.getContext('2d');for(const [value,label] of Object.entries(labels)){const option=new Option(label,value);choose.add(option)}choose.value='punching';
let images=[],serial=0,start=0,paused=false,pausePhase=0;
const cloud=new Image();cloud.src='sun-wukong/artifact-platform.png';
const load=src=>new Promise((resolve,reject)=>{const image=new Image();image.onload=()=>resolve(image);image.onerror=()=>reject(new Error('Cannot load '+src));image.src=src});
async function select(){const version=++serial,count=['idle','waiting','failed'].includes(choose.value)?8:24;try{const ready=await Promise.all(Array.from({length:count},(_,i)=>load('sun-wukong/'+choose.value+'/'+String(i).padStart(2,'0')+'.png')));if(version!==serial)return;images=ready;start=performance.now();pausePhase=0}catch(error){document.getElementById('phase').textContent=error.message}}
choose.onchange=select;document.getElementById('fps').oninput=event=>document.getElementById('speed').textContent=event.target.value+' fps';
document.getElementById('play').onclick=()=>{paused=!paused;document.getElementById('play').textContent=paused?'Play':'Pause';if(!paused)start=performance.now()-pausePhase*1000/Number(document.getElementById('fps').value)};
function draw(now){const bg=document.getElementById('background').value;ctx.fillStyle=bg;ctx.fillRect(0,0,canvas.width,canvas.height);if(images.length){const index=paused?pausePhase:Math.floor((now-start)*Number(document.getElementById('fps').value)/1000)%images.length;pausePhase=index;const flying=document.getElementById('cloud').checked;if(flying&&cloud.complete)ctx.drawImage(cloud,60,355,275,138);ctx.drawImage(images[index],0,flying?-14:0);document.getElementById('phase').textContent='Frame '+(index+1)+' / '+images.length}requestAnimationFrame(draw)}select();requestAnimationFrame(draw);
</script></html>'''
(folder/'animation-preview.html').write_text(html,encoding='utf-8')
print(folder/'animation-preview.html')

"""Render offline animation proofs and inspect actual exported frame pixels.

This formats existing rendered PNGs; it does not generate or paint character art.
"""
import hashlib,json,math,statistics,sys
from pathlib import Path
from PIL import Image,ImageChops,ImageStat,ImageDraw,ImageFont,ImageFilter

root=Path(sys.argv[1]).resolve()
roles=[('orbit','青衡','Qingheng','assets'),('spark','绯笺','Feijian','assets/characters/spark'),('moss','苍芷','Cangzhi','assets/characters/moss'),('sun-wukong','行者悟空','Wukong','assets/bundled/sun-wukong'),('bailong','小白龙','Bailong','assets/bundled/bailong'),('pippin','小墨','Xiaomo','assets/characters/pippin')]
states=['idle','waiting','failed','running','running-left','running-right','waving','review','jumping','punching','eating','wiping-mouth']
report={'checks':[],'note':'Pixel/contact/loop checks complement the kinematic tests; visual review is still required.'}
report['articulated_arms']=[]
for cid in ['sun-wukong','bailong']:
 path=root/'rig-audits'/(cid+'.json')
 if not path.exists():raise ValueError(f'Missing actual-render arm audit: {cid}')
 rows=json.loads(path.read_text(encoding='utf-8-sig'))
 if len(rows)!=480:raise ValueError(f'{cid}: incomplete actual-render arm audit')
 worst=0
 for row in rows:
  def dist(a,b):return math.hypot(a['X']-b['X'],a['Y']-b['Y'])
  error=dist(row['palm'],row['requested']);worst=max(worst,error)
  if error>1:raise ValueError(f"{cid}/{row['state']}/{row['frame']}: unreachable palm target {error:.3f}px")
  if abs(dist(row['shoulder'],row['elbow'])-row['upperLength'])>.001 or abs(dist(row['elbow'],row['palm'])-row['lowerLength'])>.001:raise ValueError(f'{cid}: arm bone length changed')
  if row['profile'] and row['bodyAlreadyDrawn']!=(row['arm']==1):raise ValueError(f'{cid}: walking arm is on the wrong side of the torso draw pass')
  if row['profile'] or row['state'] in ['idle','waiting']:
   upper=(row['elbow']['X']-row['shoulder']['X'],row['elbow']['Y']-row['shoulder']['Y']);lower=(row['palm']['X']-row['elbow']['X'],row['palm']['Y']-row['elbow']['Y'])
   bend=math.degrees(math.acos(max(-1,min(1,(upper[0]*lower[0]+upper[1]*lower[1])/(row['upperLength']*row['lowerLength'])))))
   limit=11 if row['profile'] else .01
   if bend>2 or row['palm']['Y']-row['shoulder']['Y']<.99*(row['upperLength']+row['lowerLength']) or abs(row['palm']['X']-row['shoulder']['X'])>limit:raise ValueError(f'{cid}: arm is held up or bent instead of hanging naturally')
  if row['state'] in ['waving','wiping-mouth'] and row['arm']==1 and 8<=row['frame']<=16:
   if row['elbow']['X']<=row['palm']['X']+2:raise ValueError(f"{cid}/{row['state']}/{row['frame']}: right elbow folds inward")
 for state in ['running','running-left','running-right']:
  for arm in [0,1]:
   poses=[r for r in rows if r['state']==state and r['arm']==arm]
   a,b=poses[0],poses[12];dx0=a['palm']['X']-a['shoulder']['X'];dx1=b['palm']['X']-b['shoulder']['X']
   if arm==0 and not(dx0<0<dx1) or arm==1 and not(dx1<0<dx0):raise ValueError(f'{cid}: same-side hand/foot advance')
 report['articulated_arms'].append({'id':cid,'poses':len(rows),'maximum_target_error_px':round(worst,5),'constant_bone_lengths':True,'opposite_arm_leg_swing':True,'raised_right_elbow_outward':True,'walking_far_arm_behind_body':True,'walking_arms_hang_straight':True})
native_wave=root/'previews/wave-audit.json'
if native_wave.exists():
 wave_rows=json.loads(native_wave.read_text(encoding='utf-8-sig'))
 for cid in ['sun-wukong','bailong']:
  path=root/'assets/bundled'/(cid+'.wave-audit.json')
  if not path.exists():raise ValueError(f'Missing whole-cycle wave audit: {cid}')
  wave_rows.extend(json.loads(path.read_text(encoding='utf-8-sig')))
 report['wave_lowering']=[]
 for cid in ['orbit','spark','moss','sun-wukong','bailong']:
  rows=sorted([p for p in wave_rows if p['id']==cid],key=lambda p:p['frame'])
  if len(rows)!=24:raise ValueError(f'{cid}: missing wave phases')
  for row in rows:
   if row['elbow']['X']<row['shoulder']['X']-.001:raise ValueError(f'{cid}: elbow folds into torso during wave lowering at {row["frame"]}')
   for a,b,length in [('shoulder','elbow','upperLength'),('elbow','palm','lowerLength')]:
    if abs(math.hypot(row[a]['X']-row[b]['X'],row[a]['Y']-row[b]['Y'])-row[length])>.001:raise ValueError(f'{cid}: wave changes limb length')
  report['wave_lowering'].append({'id':cid,'frames':24,'lowering_frames':7,'elbow_outside_torso_entire_cycle':True,'constant_bone_lengths':True})
for cid,zh,en,folder in roles:
 for state in states:
  paths=sorted((root/folder/state).glob('*.png'));expected=8 if state in ['idle','waiting','failed'] else 24
  if len(paths)!=expected:raise ValueError(f'{cid}/{state}: wrong frame count')
  frames=[];ground=[];bounds=[]
  for p in paths:
   im=Image.open(p).convert('RGBA')
   if im.size!=(384,416):raise ValueError(f'{p}: canvas changed')
   box=im.getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
   if not box or box[0]<2 or box[1]<2 or box[2]>382 or box[3]>414:raise ValueError(f'{p}: clipped/empty silhouette {box}')
   bounds.append(box);frames.append(im)
   if state.startswith('running'):
    contact_rows=range(375,383) if cid=='pippin' else range(392,400)
    n=sum(im.getpixel((x,y))[3]>=128 for y in contact_rows for x in range(384));ground.append(n)
    if n<4:raise ValueError(f'{p}: neither sole reaches the ground')
  unique=len({hashlib.sha256(p.read_bytes()).hexdigest() for p in paths})
  if state.startswith('running') and unique!=24:raise ValueError(f'{cid}/{state}: repeated walking pose')
  # Compare alpha-aware visible frame differences, including the final seam.
  visible=[]
  for im in frames:
   bg=Image.new('RGB',im.size,'#f4f3ee');bg.paste(im,(0,0),im);visible.append(bg)
  deltas=[sum(ImageStat.Stat(ImageChops.difference(visible[i],visible[(i+1)%expected])).mean)/3 for i in range(expected)]
  seam=deltas[-1];reference=statistics.median(deltas[:-1])
  report['checks'].append({'id':cid,'state':state,'frames':expected,'unique':unique,'ground_min':min(ground) if ground else None,'seam_delta':round(seam,3),'median_adjacent_delta':round(reference,3),'seam_requires_review':seam>max(1.0,reference*2.5)})
# Check the actual body-only exports and composited collar coverage for every humanoid.
art=Path(__file__).resolve().parent.parent/'art'
calibrations=json.loads((art/'anatomy-calibration.json').read_text(encoding='utf-8-sig'))
report['neck_collars']=[]
for cid,zh,en,folder in roles:
 if cid=='pippin':continue
 for profile in [False,True]:
  view=calibrations[cid]['profile' if profile else 'normal'];poly=view['aperture']
  u=sum(p[0] for p in poly)/len(poly);v=sum(p[1] for p in poly)/len(poly)
  if not profile:box=(86,191,212,177)
  else:
   base=art if cid in ['orbit','spark','moss'] else art/'companion-rigs'/cid;key=cid if cid in ['orbit','spark','moss'] else 'orbit'
   rect=json.loads((base/'side-torsos/regions.json').read_text(encoding='utf-8-sig'))[key]
   anchor=view.get('torsoAnchorUv',{'orbit':[.630,.044],'spark':[.651,.036],'moss':[.663,.033]}.get(cid,[.630,.044]))
   neck={'spark':[199,187],'moss':[190,200]}.get(cid,[185,194]);h=(368-neck[1])/(1-anchor[1]);w=h*rect[2]/rect[3]
   box=(neck[0]-anchor[0]*w,neck[1]-anchor[1]*h,w,h)
  x0,y0=box[0]+u*box[2],box[1]+v*box[3]
  layer_root=root/'previews/body-layers' if cid in ['orbit','spark','moss'] else root/'assets/bundled/body-layers'
  body=Image.open(layer_root/cid/('profile.png' if profile else 'normal.png')).convert('RGBA')
  if body.getpixel((round(x0),round(y0)))[3]>12:raise ValueError(f'{cid}: foreground body still includes a painted neck')
  # Broad cap regressions need edge coverage; a transparent centroid alone
  # missed duplicate neck pixels at the top and sides of these two torsos.
  cap_samples=0
  if cid in ['moss','bailong']:
   cap=Image.new('L',body.size)
   ImageDraw.Draw(cap).polygon([(round(box[0]+u*box[2]),round(box[1]+v*box[3])) for u,v in poly],fill=255)
   cap=cap.filter(ImageFilter.MinFilter(5)) # exclude the 2px antialias boundary
   values=[a for a,m in zip(body.getchannel('A').getdata(),cap.getdata()) if m]
   cap_samples=len(values)
   if cap_samples<100 or max(values)>12:raise ValueError(f'{cid}: residual foreground neck pixels across collar cap')
  alphas=[]
  for state in states:
   if state.startswith('running')!=profile:continue
   paths=sorted((root/folder/state).glob('*.png'))
   for frame,path in enumerate(paths):
    t=frame/len(paths)*math.pi*2;a=math.radians(math.sin(t)*.45 if profile else 2 if state=='failed' else 0)
    x=192+(x0-192)*math.cos(a)-(y0-260)*math.sin(a);y=260+(x0-192)*math.sin(a)+(y0-260)*math.cos(a)
    if profile:x+=math.sin(t)*.9;y+=math.cos(t*2)*.75
    else:y+=math.sin(t)*.35
    if state=='jumping':y-=15*math.sin(t/2)**2
    if state=='running-left':x=384-x
    alpha=Image.open(path).convert('RGBA').getpixel((round(x),round(y)))[3];alphas.append(alpha)
    if alpha<200:raise ValueError(f'{cid}/{state}/{frame}: gap at the real collar aperture')
  report['neck_collars'].append({'id':cid,'view':'profile' if profile else 'normal','body_aperture_cleared':True,'dense_cap_samples':cap_samples,'composite_frames':len(alphas),'minimum_composite_alpha':min(alphas)})
root.joinpath('motion-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
fontfile=Path('C:/Windows/Fonts/msyh.ttc');font=ImageFont.truetype(str(fontfile),17);small=ImageFont.truetype(str(fontfile),13)
poster=Image.new('RGB',(1440,390),'#f0f2ec');draw=ImageDraw.Draw(poster)
draw.text((28,16),'AgentPal · 六位伙伴 / Six companions',font=font,fill='#233f33')
for i,(cid,zh,en,folder) in enumerate(roles):
 im=Image.open(root/folder/'idle/00.png').convert('RGBA');im.thumbnail((220,286),Image.Resampling.LANCZOS);x=i*240+(240-im.width)//2;poster.paste(im,(x,52),im)
 draw.text((i*240+120,344),zh+' / '+en,font=small,anchor='mm',fill='#435e4d')
poster.save(root/'six-companions.png')
comparison=Image.new('RGB',(1200,1070),'#f0f2ec');d=ImageDraw.Draw(comparison)
compare_roles=[r for r in roles if r[0]!='pippin']
for col,(cid,zh,en,folder) in enumerate(compare_roles):
 d.text((col*240+120,22),zh+' / '+en,font=small,anchor='mm',fill='#435e4d')
 for row,state in enumerate(['idle','running-right','waving','wiping-mouth']):
  index=0 if state=='idle' else 12
  im=Image.open(root/folder/state/f'{index:02}.png').convert('RGBA');im.thumbnail((220,232),Image.Resampling.LANCZOS);comparison.paste(im,(col*240+(240-im.width)//2,45+row*255),im)
  d.text((col*240+120,285+row*255),state,font=small,anchor='mm',fill='#435e4d')
comparison.save(root/'arm-comparison.png')
for label,pairs in [('wukong-motion',[('sun-wukong','running-right'),('sun-wukong','punching'),('sun-wukong','wiping-mouth')]),('wukong-arms',[('sun-wukong','running-right'),('sun-wukong','waving'),('sun-wukong','wiping-mouth')]),('bailong-motion',[('bailong','running-right'),('bailong','waving'),('bailong','eating')])]:
 gif=[]
 for index in range(24):
  frame=Image.new('RGB',(720,290),'#f0f2ec');d=ImageDraw.Draw(frame)
  for col,(cid,state) in enumerate(pairs):
   role=next(r for r in roles if r[0]==cid);im=Image.open(root/role[3]/state/(f'{index:02}.png')).convert('RGBA');im.thumbnail((220,245),Image.Resampling.LANCZOS);frame.paste(im,(col*240+(240-im.width)//2,8),im);d.text((col*240+120,269),state,font=small,anchor='mm',fill='#435e4d')
  gif.append(frame)
 gif[0].save(root/(label+'.gif'),save_all=True,append_images=gif[1:],duration=80,loop=0,optimize=True)
for background,name in [('#263830','neck-details.png'),('#f8f7f2','neck-details-light.png')]:
 necks=Image.new('RGB',(1500,600),background);d=ImageDraw.Draw(necks)
 for col,(cid,zh,en,folder) in enumerate(compare_roles):
  for row,state in enumerate(['idle','running-right']):
   im=Image.open(root/folder/state/'00.png').convert('RGBA').crop((145,165,235,245)).resize((270,240),Image.Resampling.LANCZOS)
   necks.paste(im,(col*300,30+row*285),im);d.text((col*300,8+row*285),en+' '+state,font=small,fill='#8a9e8e')
 necks.save(root/name)
data=[{'id':cid,'name':zh+' · '+en,'folder':folder} for cid,zh,en,folder in roles]
page='''<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>AgentPal · 动作审核</title>
<style>*{box-sizing:border-box}body{font:15px system-ui,"Microsoft YaHei",sans-serif;background:#f0f2ec;color:#233f33;margin:24px auto;max-width:1260px;padding:0 20px}h1{font-size:26px}p{line-height:1.7;color:#586e60}nav{display:flex;gap:12px;align-items:center;flex-wrap:wrap;padding:16px;background:#fff;border:1px solid #dce3da;border-radius:14px;position:sticky;top:0;z-index:2}select,button{font:inherit;padding:8px 14px;border:1px solid #cbd6cd;border-radius:8px;background:white;color:inherit}.cards{display:grid;grid-template-columns:repeat(3,1fr);gap:16px;margin-top:20px}article{border:1px solid #dce3da;border-radius:16px;background:#fff;padding:14px;text-align:center}h2{font-size:16px}canvas{max-width:100%;height:auto;width:270px}small{display:block;color:#6b7d70}a{color:#2a6b55}@media(max-width:850px){.cards{grid-template-columns:repeat(2,1fr)}}@media(max-width:560px){.cards{grid-template-columns:1fr}}</style>
<h1>六位伙伴 · 动作审核 / Motion review</h1><p>青衡确认标准已同步 · 独立颈领校准 · 自然垂臂与外侧屈肘 · 六位伙伴完整动作<br>所有动作可切换、暂停与逐帧查看。先检查画面，再进行安装包发布。<a href="motion-audit.json">查看接地、帧数与循环检测</a></p>
<p><a href="neck-details.png">颈领放大图</a> · <a href="approved-baseline-audit.json">青衡确认稿逐像素比对</a></p><nav><label>动作 <select id="state"></select></label><label>速度 <select id="speed"><option value=".5">0.5× 慢速检查</option><option value="1" selected>1× 正常速度</option><option value="1.5">1.5×</option></select></label><button id="play">暂停</button><button id="next">下一帧</button><label>背景 <select id="bg"><option value="#ffffff">白色</option><option value="#24392e">深色</option><option value="#f0f2ec">浅灰</option></select></label></nav><div class="cards" id="cards"></div>
<script>const roles=ROLES,states=STATES,labels={'idle':'待机','waiting':'等待','failed':'失落','running':'行走循环','running-left':'向左行走','running-right':'向右行走','waving':'招手','review':'思考 / 审阅','jumping':'跳跃','punching':'特色动作 / 悟空转棍','eating':'吃东西','wiping-mouth':'擦嘴'};const state=document.querySelector('#state'),speed=document.querySelector('#speed');for(const s of states)state.add(new Option(labels[s]+' · '+s,s));let paused=false,start=performance.now(),manual=0,serial=0,clock=0,lastTick=performance.now();roles.forEach(r=>{const card=document.createElement('article');card.innerHTML='<h2>'+r.name+'</h2><canvas width="384" height="416"></canvas><small></small>';document.querySelector('#cards').append(card);r.ctx=card.querySelector('canvas').getContext('2d');r.label=card.querySelector('small');r.frames=[]});async function load(){const generation=++serial;clock=0;lastTick=performance.now();manual=0;const n=['idle','waiting','failed'].includes(state.value)?8:24;for(const r of roles){const frames=await Promise.all(Array.from({length:n},(_,i)=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src=r.folder+'/'+state.value+'/'+String(i).padStart(2,'0')+'.png'})));if(serial!==generation)return;r.frames=frames;r.current=0;r.offset=0}}state.onchange=load;speed.onchange=()=>{start=performance.now()};document.querySelector('#play').onclick=e=>{paused=!paused;e.target.textContent=paused?'播放':'暂停';if(!paused){clock=0;for(const r of roles)r.offset=durations(r).slice(0,r.current||0).reduce((a,b)=>a+b,0)}};document.querySelector('#next').onclick=()=>{paused=true;document.querySelector('#play').textContent='播放';for(const r of roles)r.current=((r.current||0)+1)%Math.max(1,r.frames.length)};function durations(r){const n=r.frames.length,s=state.value;if(s==='idle'||s==='waiting')return Array.from({length:n},(_,i)=>i===6?120:500);if(s==='review'&&r.id==='sun-wukong')return Array(n).fill(58);return Array(n).fill(s.startsWith('running')?28:s==='review'||s==='eating'?60:40)}function draw(t){if(!paused)clock+=(t-lastTick)*Number(speed.value);lastTick=t;for(const r of roles){if(!r.frames.length)continue;let i=r.current||0;if(!paused){const d=durations(r),total=d.reduce((a,b)=>a+b,0);let ms=(clock+(r.offset||0))%total;i=0;while(ms>=d[i]&&i<d.length-1){ms-=d[i];i++}}r.current=i;r.ctx.fillStyle=document.querySelector('#bg').value;r.ctx.fillRect(0,0,384,416);r.ctx.drawImage(r.frames[i],0,0);r.label.textContent='Frame '+(i+1)+' / '+r.frames.length}requestAnimationFrame(draw)}load();requestAnimationFrame(draw);</script>'''
if not (root/'approved-baseline-audit.json').exists():page=page.replace(' · <a href="approved-baseline-audit.json">青衡确认稿逐像素比对</a>','')
page=page.replace('ROLES',json.dumps(data,ensure_ascii=False)).replace('STATES',json.dumps(states));root.joinpath('index.html').write_text(page,encoding='utf-8')
print('Rendered six-companion review, three slowed GIF proofs and',len(report['checks']),'state audits:',root)

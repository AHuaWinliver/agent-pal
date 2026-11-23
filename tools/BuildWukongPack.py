"""Assemble AI-drawn keyframes into the data-only agent-pal character format.

Artwork is drawn by imagegen. This tool only slices cells, fits the fixed canvas,
mirrors the left-facing cycle and writes the manifest/archive. Requires Pillow.
"""
from __future__ import annotations
import argparse,hashlib,json,statistics,zipfile
from pathlib import Path
from PIL import Image,ImageOps,ImageDraw

SIZE=(384,416)

def sheet(path:Path):
    image=Image.open(path).convert('RGBA')
    frames=[]
    for row in range(6):
        for col in range(4):
            frames.append(image.crop((round(col*image.width/4),round(row*image.height/6),round((col+1)*image.width/4),round((row+1)*image.height/6))))
    boxes=[frame.getchannel('A').point(lambda value:255 if value>48 else 0).getbbox() for frame in frames]
    if any(box is None for box in boxes):raise ValueError(f'Empty sprite cell: {path}')
    # One transform for the entire sheet, preserving authored relative motion.
    first=boxes[0];maximum_width=max(box[2]-box[0] for box in boxes)
    scale=min(334/(first[3]-first[1]),344/maximum_width,365/(max(box[3] for box in boxes)-min(box[1] for box in boxes)))
    left=(SIZE[0]-frames[0].width*scale)/2
    top=min(388-first[3]*scale,404-max(box[3] for box in boxes)*scale)
    output=[]
    for frame in frames:
        resized=frame.resize((round(frame.width*scale),round(frame.height*scale)),Image.Resampling.LANCZOS)
        canvas=Image.new('RGBA',SIZE,(0,0,0,0));canvas.alpha_composite(resized,(round(left),round(top)))
        visible=canvas.getchannel('A').point(lambda value:255 if value>48 else 0).getbbox()
        if visible is None or visible[0]<3 or visible[1]<3 or visible[2]>381 or visible[3]>413:raise ValueError(f'Clipped sprite: {path}')
        output.append(canvas)
    return output

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--artwork',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--version',default='1.0.0')
    args=parser.parse_args();root=args.output/'sun-wukong'
    if root.exists():raise SystemExit('Output already exists. Choose a fresh output directory.')
    root.mkdir(parents=True)
    pools={name:sheet(args.artwork/(name+'.png')) for name in ('walk','staff','emotions','wave','jump','eat','wipe')}
    states={'idle':pools['emotions'][:8],'waiting':pools['emotions'][8:16],'failed':pools['emotions'][16:24],
            'running':pools['walk'],'running-right':pools['walk'],'running-left':[ImageOps.mirror(frame) for frame in pools['walk']],
            'waving':pools['wave'],'review':(pools['emotions'][8:16])*3,'jumping':pools['jump'],'punching':pools['staff'],
            'eating':pools['eat'],'wiping-mouth':pools['wipe']}
    hashes={}
    for name,frames in states.items():
        (root/name).mkdir()
        for index,frame in enumerate(frames):
            target=root/name/f'{index:02}.png';frame.save(target,optimize=True)
            hashes[target.relative_to(root).as_posix()]=hashlib.sha256(target.read_bytes()).hexdigest()
    cloud=Image.open(args.artwork/'cloud.png').convert('RGBA')
    bounds=cloud.getchannel('A').point(lambda value:255 if value>32 else 0).getbbox();cloud=cloud.crop(bounds)
    cloud.thumbnail((488,250),Image.Resampling.LANCZOS)
    platform=Image.new('RGBA',(512,256),(0,0,0,0));platform.alpha_composite(cloud,((512-cloud.width)//2,(256-cloud.height)//2))
    artifact=root/'artifact-platform.png';platform.save(artifact,optimize=True);hashes[artifact.name]=hashlib.sha256(artifact.read_bytes()).hexdigest()
    manifest={'schemaVersion':1,'id':'sun-wukong','version':args.version,'name':'孙悟空',
        'persona':'你是以古典神话孙悟空为灵感的桌面道友。机灵、勇敢、讲义气，偶尔自称俺老孙，语气爽朗但不浮夸。用用户使用的语言回复。处理工作时先看清任务和当前目录，保留已有修改，遇到问题给出具体证据与可行办法；不编造已执行的命令，不擅自扩大授权。陪用户把大任务分成可完成的小步，不催促过度工作。',
        'quotes':['俺老孙来也！今天先办哪件要紧事？','路不熟，就先探清，再往前走。','金箍棒备好了，难题咱们一起拆。','遇到关口别急，找准症结再动手。','筋斗云也得歇一歇，道友起来活动活动吧。','把手头这一小步做好，就又近了一程。'],
        'reminderStyle':'stone','canvasWidth':384,'canvasHeight':416,'mouthX':.69,'mouthY':.49,'files':hashes}
    (root/'character.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    (root/'README.md').write_text('# Sun Wukong / 孙悟空\n\nImport this ZIP using Companions → Import character pack. Includes 240 transparent 384×416 PNG frames, 24 distinct walking phases in both directions, a standing staff-spinning action and a flying cloud. Both arms counter-swing during walking; wrists and hands exchange their grip during a complete staff revolution.\n\n此角色包独立导入，不覆盖内置人物，也不修改千问项目。动作素材由 imagegen 绘制；构建脚本只做切帧、统一画布、左右镜像和清单打包。更新同一角色时确认替换即可。\n',encoding='utf-8')
    archive=args.output/f'agent-pal-sun-wukong-{args.version}.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=7) as packed:
        for path in sorted(root.rglob('*')):
            if path.is_file():packed.write(path,'sun-wukong/'+path.relative_to(root).as_posix())
    digest=hashlib.sha256(archive.read_bytes()).hexdigest();archive.with_suffix('.zip.sha256').write_text(digest+'  '+archive.name+'\n',encoding='utf-8')
    # A real-size visual review image; generated art is not redrawn or composited into new poses.
    contact=Image.new('RGB',(960,760),'#f5f4ef');draw=ImageDraw.Draw(contact)
    for slot,state in enumerate(('idle','running-right','running-left','punching','eating','wiping-mouth')):
        preview=states[state][len(states[state])//2].copy();preview.thumbnail((280,310),Image.Resampling.LANCZOS)
        x=(slot%3)*320+(320-preview.width)//2;y=(slot//3)*380+30;contact.paste(preview,(x,y),preview)
        draw.text(((slot%3)*320+20,(slot//3)*380+350),state,fill='#30443b')
    contact.save(args.output/'wukong-actions-preview.png')
    for state in ('running','running-left','running-right'):
        if len(set(hashes[f'{state}/{index:02}.png'] for index in range(24)))!=24:raise ValueError('Repeated walking frames: '+state)
    print('PASS assembled 240 RGBA frames, distinct walking phases, fixed canvas and cloud:',root)
    print('ZIP:',archive,'SHA256:',digest)

if __name__=='__main__':main()

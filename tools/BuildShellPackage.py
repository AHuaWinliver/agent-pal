"""Build the small Windows 11 Explorer identity package, without bundling tools.

Requires a compiled x64 shell DLL, Pillow for existing-icon resampling, and the
official Windows SDK MakeAppx tool. For signed distribution, pass the publisher
matching your signing certificate and sign the resulting MSIX in your pipeline.
"""
from pathlib import Path
import argparse,hashlib,json,shutil,subprocess,tempfile
from xml.etree import ElementTree as ET
from PIL import Image

def main():
    parser=argparse.ArgumentParser()
    for name in ('dll','icon','makeappx','output'):parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--version',default='1.0.0.4')
    parser.add_argument('--publisher',default='CN=agent-pal, OID.2.25.311729368913984317654407730594956997722=1')
    args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    digest=hashlib.sha256(args.dll.read_bytes()).hexdigest()[:12]
    dll_name=f'AgentPalShell64-{digest}.dll';shutil.copyfile(args.dll,args.output/dll_name)
    core='http://schemas.microsoft.com/appx/manifest/foundation/windows10'
    namespaces={'':core,'uap':'http://schemas.microsoft.com/appx/manifest/uap/windows10','uap10':'http://schemas.microsoft.com/appx/manifest/uap/windows10/10','desktop4':'http://schemas.microsoft.com/appx/manifest/desktop/windows10/4','desktop5':'http://schemas.microsoft.com/appx/manifest/desktop/windows10/5','com':'http://schemas.microsoft.com/appx/manifest/com/windows10','rescap':'http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities'}
    for prefix,url in namespaces.items():ET.register_namespace(prefix,url)
    def tag(name):
        prefix,local=name.split(':',1) if ':' in name else ('',name)
        return '{'+namespaces[prefix]+'}'+local
    def child(parent,name,attrs=None,text=None):
        item=ET.SubElement(parent,tag(name),{tag(key) if ':' in key else key:str(value) for key,value in (attrs or {}).items()})
        if text is not None:item.text=text
        return item
    package=ET.Element(tag('Package'),{'IgnorableNamespaces':'uap uap10 desktop4 desktop5 com rescap'})
    child(package,'Identity',{'Name':'AgentPal.ContextMenu','Publisher':args.publisher,'Version':args.version,'ProcessorArchitecture':'x64'})
    properties=child(package,'Properties')
    for name,text in [('DisplayName','AgentPal'),('PublisherDisplayName','AgentPal'),('Logo','Assets/StoreLogo.png'),('uap10:AllowExternalContent','true')]:child(properties,name,text=text)
    dependencies=child(package,'Dependencies');child(dependencies,'TargetDeviceFamily',{'Name':'Windows.Desktop','MinVersion':'10.0.22000.0','MaxVersionTested':'10.0.26200.0'})
    resources=child(package,'Resources');child(resources,'Resource',{'Language':'en-us'})
    apps=child(package,'Applications');app=child(apps,'Application',{'Id':'AgentPalShell','Executable':'agent-pal.exe','uap10:RuntimeBehavior':'win32App','uap10:TrustLevel':'mediumIL'})
    child(app,'uap:VisualElements',{'DisplayName':'AgentPal','Description':'Assign tasks to your desktop companions','Square150x150Logo':'Assets/Square150x150Logo.png','Square44x44Logo':'Assets/Square44x44Logo.png','BackgroundColor':'transparent','AppListEntry':'none'})
    extensions=child(app,'Extensions');com=child(extensions,'com:Extension',{'Category':'windows.comServer'});server=child(child(com,'com:ComServer'),'com:SurrogateServer',{'DisplayName':'agent-pal folder tasks'})
    context=child(child(extensions,'desktop4:Extension',{'Category':'windows.fileExplorerContextMenus'}),'desktop4:FileExplorerContextMenus')
    item_types={}
    for kind,item_type in enumerate(('Directory',r'Directory\Background',r'Directory\Background')):
        item=item_types.get(item_type)
        if item is None:item=item_types[item_type]=child(context,'desktop5:ItemType',{'Type':item_type})
        for slot in range(64):
            ordinal=slot*3+kind+1;clsid=f'BC365B20-422D-4FEF-B90B-570000{ordinal:06X}'
            child(server,'com:Class',{'Id':clsid,'Path':'shell\\'+dll_name,'ThreadingModel':'STA'})
    for slot in range(64):
        child(server,'com:Class',{'Id':f'BC365B20-422D-4FEF-B90B-570000{193+slot:06X}','Path':'shell\\'+dll_name,'ThreadingModel':'STA'})
    for kind,item_type in enumerate(('Directory',r'Directory\Background')):
        clsid=f'BC365B20-422D-4FEF-B90B-570000{4097+kind:06X}'
        child(server,'com:Class',{'Id':clsid,'Path':'shell\\'+dll_name,'ThreadingModel':'STA'})
        child(item_types[item_type],'desktop5:Verb',{'Id':f'AgentPalEntry{kind}','Clsid':clsid})
    caps=child(package,'Capabilities');child(caps,'rescap:Capability',{'Name':'runFullTrust'});child(caps,'rescap:Capability',{'Name':'unvirtualizedResources'})
    with tempfile.TemporaryDirectory(prefix='agent-pal-shell-manifest-') as staging:
        staging=Path(staging);(staging/'Assets').mkdir()
        ET.ElementTree(package).write(staging/'AppxManifest.xml',encoding='utf-8',xml_declaration=True)
        image=Image.open(args.icon).convert('RGBA')
        for name,size in [('StoreLogo',50),('Square150x150Logo',150),('Square44x44Logo',44)]:image.resize((size,size),Image.Resampling.LANCZOS).save(staging/'Assets'/f'{name}.png')
        result=subprocess.run([str(args.makeappx),'pack','/o','/nv','/d',str(staging),'/p',str(args.output/'agent-pal.ContextMenu.msix')],capture_output=True)
        if result.returncode:
            print(result.stdout.decode('utf-8',errors='replace'));print(result.stderr.decode('utf-8',errors='replace'))
            raise SystemExit('MakeAppx failed; identity package was not produced.')
        shutil.copyfile(staging/'AppxManifest.xml',args.output/'AppxManifest.xml')
        shutil.copytree(staging/'Assets',args.output/'Assets',dirs_exist_ok=True)
    (args.output/'deployment.json').write_text(json.dumps({'name':'AgentPal.ContextMenu','publisher':args.publisher,'unsigned':'OID.2.25.' in args.publisher,'version':args.version,'dll':dll_name},indent=2),encoding='utf-8')
    print('Built native shell extension and identity package:',args.output)
if __name__=='__main__':main()

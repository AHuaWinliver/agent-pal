// SPDX-License-Identifier: Apache-2.0
// Native, dependency-free IExplorerCommand bridge. Never runs Claude in Explorer.
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shobjidl.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <string>
#include <vector>
#ifdef AGENT_PAL_SHELL_DIAGNOSTICS
#include <cstdio>
#endif

static LONG objects = 0;
static constexpr GUID baseId = {0xbc365b20,0x422d,0x4fef,{0xb9,0x0b,0x57,0,0,0,0,0}};
static std::wstring StringValue(HKEY key, const wchar_t* name) {
    DWORD size=0; if(RegGetValueW(key,nullptr,name,RRF_RT_REG_SZ,nullptr,nullptr,&size)!=ERROR_SUCCESS || size>65536)return {};
    std::wstring value(size/sizeof(wchar_t),L'\0');
    if(RegGetValueW(key,nullptr,name,RRF_RT_REG_SZ,nullptr,&value[0],&size)!=ERROR_SUCCESS)return {};
    value.resize(size/sizeof(wchar_t));
    while(!value.empty()&&value.back()==L'\0')value.pop_back(); return value;
}
struct Settings {
    std::wstring title,icon,exe,command,pet; bool enabled=false;
    std::wstring Binding()const{return command+L"/"+pet;}
};
static bool SafeId(const std::wstring& value) {
    if(value.empty()||value.size()>80)return false;
    for(wchar_t ch:value)if(!((ch>=L'a'&&ch<=L'z')||(ch>=L'A'&&ch<=L'Z')||(ch>=L'0'&&ch<=L'9')||ch==L'-'))return false;
    return true;
}
static Settings ReadSettings(int slot,int context) {
    Settings value; wchar_t path[128]; wsprintfW(path,L"Software\\agent-pal\\ShellCommands\\%02d",slot);
    HKEY key=nullptr; if(RegOpenKeyExW(HKEY_CURRENT_USER,path,0,KEY_READ,&key)!=ERROR_SUCCESS)return value;
    DWORD enabled=0,size=sizeof(enabled);
    RegGetValueW(key,nullptr,context==2?L"Desktop":L"Folders",RRF_RT_REG_DWORD,nullptr,&enabled,&size);
    if(context==3){DWORD other=0;size=sizeof(other);RegGetValueW(key,nullptr,L"Desktop",RRF_RT_REG_DWORD,nullptr,&other,&size);enabled|=other;}
    value.title=StringValue(key,L"Title");value.icon=StringValue(key,L"Icon");value.exe=StringValue(key,L"Executable");
    value.command=StringValue(key,L"Command");value.pet=StringValue(key,L"Companion");
    RegCloseKey(key);
    DWORD attributes=GetFileAttributesW(value.exe.c_str());
    value.enabled=enabled&&SafeId(value.command)&&SafeId(value.pet)&&!value.title.empty()&&!PathIsRelativeW(value.exe.c_str())&&attributes!=INVALID_FILE_ATTRIBUTES&&!(attributes&FILE_ATTRIBUTE_DIRECTORY);
#ifdef AGENT_PAL_SHELL_DIAGNOSTICS
    fwprintf(stderr,L"native slot=%d context=%d enabled=%lu command=%d pet=%d title=%d relative=%d attributes=%lu commandLength=%llu petLength=%llu commandFirst=%u commandLast=%u\n",slot,context,enabled,SafeId(value.command),SafeId(value.pet),!value.title.empty(),PathIsRelativeW(value.exe.c_str()),attributes,(unsigned long long)value.command.size(),(unsigned long long)value.pet.size(),value.command.empty()?0:value.command.front(),value.command.empty()?0:value.command.back());
#endif
    return value;
}
static std::wstring RootValue(const wchar_t* name,const wchar_t* fallback=L""){
    HKEY key=nullptr;std::wstring value;if(RegOpenKeyExW(HKEY_CURRENT_USER,L"Software\\agent-pal\\ShellCommands",0,KEY_READ,&key)==ERROR_SUCCESS){value=StringValue(key,name);RegCloseKey(key);}return value.empty()?fallback:value;
}
static int Ordinal(REFCLSID clsid) {
    GUID test=clsid;int value=(test.Data4[5]<<16)|(test.Data4[6]<<8)|test.Data4[7];
    test.Data4[5]=test.Data4[6]=test.Data4[7]=0;
    return IsEqualGUID(test,baseId)&&((value>=1&&value<=256)||value==4097||value==4098)?value-1:-1;
}
class Command final:public IExplorerCommand,public IObjectWithSite {
    LONG refs=1;GUID id;int slot,context;IUnknown* site=nullptr;std::wstring displayed;
    std::vector<int> VisibleSlots(){
        int mode=context;
        if(context==3){std::wstring path;bool desktop=false;if(SUCCEEDED(Directory(nullptr,path,&desktop)))mode=desktop?2:1;}
        std::vector<int> result;for(int i=0;i<64;i++)if(ReadSettings(i,mode).enabled)result.push_back(i);return result;
    }
    HRESULT Directory(IShellItemArray* items,std::wstring& path,bool* desktop=nullptr) {
        if(desktop)*desktop=false;
        if(items){DWORD count=0;if(SUCCEEDED(items->GetCount(&count))&&count>0){
            if(count!=1)return E_INVALIDARG;
            IShellItem* item=nullptr;HRESULT hr=items->GetItemAt(0,&item);if(FAILED(hr))return hr;
            PWSTR name=nullptr;hr=item->GetDisplayName(SIGDN_FILESYSPATH,&name);item->Release();
            if(SUCCEEDED(hr)&&name){path=name;CoTaskMemFree(name);return S_OK;}return hr;
        }}
        // Background invokes have no selected item. Resolve the actual view folder.
        IServiceProvider* provider=nullptr;IFolderView* view=nullptr;IPersistFolder2* folder=nullptr;
        HRESULT hr=site?site->QueryInterface(IID_PPV_ARGS(&provider)):E_NOINTERFACE;
        if(SUCCEEDED(hr)){
            hr=provider->QueryService(SID_SFolderView,IID_PPV_ARGS(&view));
            if(FAILED(hr)){
                IShellBrowser* browser=nullptr;IShellView* shell=nullptr;
                if(SUCCEEDED(provider->QueryService(SID_STopLevelBrowser,IID_PPV_ARGS(&browser)))){
                    if(SUCCEEDED(browser->QueryActiveShellView(&shell))){hr=shell->QueryInterface(IID_PPV_ARGS(&view));shell->Release();}browser->Release();
                }
            }
            provider->Release();
        }
        if(SUCCEEDED(hr)&&view){hr=view->GetFolder(IID_PPV_ARGS(&folder));view->Release();}
        if(SUCCEEDED(hr)&&folder){
            PIDLIST_ABSOLUTE pidl=nullptr;hr=folder->GetCurFolder(&pidl);folder->Release();
            if(SUCCEEDED(hr)&&pidl){
                if(desktop)*desktop=pidl->mkid.cb==0;
                wchar_t name[32768];if(SHGetPathFromIDListEx(pidl,name,32768,GPFIDL_DEFAULT))path=name;
                else if(context==2&&pidl->mkid.cb==0){PWSTR desktop=nullptr;if(SUCCEEDED(SHGetKnownFolderPath(FOLDERID_Desktop,0,nullptr,&desktop))){path=desktop;CoTaskMemFree(desktop);}}
                CoTaskMemFree(pidl);
            }
        }
        return path.empty()?E_FAIL:S_OK;
    }
public:
    explicit Command(REFCLSID clsid):id(clsid){int n=Ordinal(clsid);if(n>=4096){slot=-1;context=n==4096?0:3;}else if(n>=192){slot=n-192;context=3;}else{slot=n/3;context=n%3;}InterlockedIncrement(&objects);}
    ~Command(){if(site)site->Release();InterlockedDecrement(&objects);}
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void** value)override{
        if(!value)return E_POINTER;*value=nullptr;
        if(IsEqualIID(iid,IID_IUnknown)||IsEqualIID(iid,IID_IExplorerCommand))*value=static_cast<IExplorerCommand*>(this);
        else if(IsEqualIID(iid,IID_IObjectWithSite))*value=static_cast<IObjectWithSite*>(this);
        else return E_NOINTERFACE;AddRef();return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef()override{return InterlockedIncrement(&refs);}
    ULONG STDMETHODCALLTYPE Release()override{LONG n=InterlockedDecrement(&refs);if(!n)delete this;return n;}
    HRESULT STDMETHODCALLTYPE GetTitle(IShellItemArray*,LPWSTR* title)override{
        if(slot<0){auto slots=VisibleSlots();displayed=slots.size()==1?ReadSettings(slots[0],context).Binding():L"";return SHStrDupW(RootValue(L"Title",L"发送Agent Pal任务").c_str(),title);}
        auto settings=ReadSettings(slot,context);displayed=settings.Binding();return SHStrDupW(settings.title.c_str(),title);
    }
    HRESULT STDMETHODCALLTYPE GetIcon(IShellItemArray*,LPWSTR* icon)override{
        if(slot<0){auto slots=VisibleSlots();
            std::wstring fallback=slots.empty()?L"":ReadSettings(slots[0],context).exe+L",0";return SHStrDupW(RootValue(L"Icon",fallback.c_str()).c_str(),icon);}
        return SHStrDupW(ReadSettings(slot,context).icon.c_str(),icon);
    }
    HRESULT STDMETHODCALLTYPE GetToolTip(IShellItemArray*,LPWSTR* text)override{*text=nullptr;return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE GetCanonicalName(GUID* value)override{if(!value)return E_POINTER;*value=id;return S_OK;}
    HRESULT STDMETHODCALLTYPE GetState(IShellItemArray*,BOOL,EXPCMDSTATE* state)override{
        if(!state)return E_POINTER;*state=ECS_HIDDEN;
        if(slot<0){if(context==3){std::wstring path;if(FAILED(Directory(nullptr,path)))return S_OK;}if(VisibleSlots().empty())return S_OK;*state=ECS_ENABLED;return S_OK;}
        if(!ReadSettings(slot,context).enabled)return S_OK;
        if(context>0){std::wstring path;bool desktop=false;if(FAILED(Directory(nullptr,path,&desktop))||(context!=3&&desktop!=(context==2))||!ReadSettings(slot,desktop?2:1).enabled)return S_OK;}
        *state=ECS_ENABLED;return S_OK;
    }
    HRESULT STDMETHODCALLTYPE Invoke(IShellItemArray* items,IBindCtx*)override{
        int selected=slot;if(slot<0){auto slots=VisibleSlots();if(slots.size()!=1)return HRESULT_FROM_WIN32(ERROR_CANCELLED);selected=slots[0];}
        auto settings=ReadSettings(selected,context);
        if(!settings.enabled||(!displayed.empty()&&displayed!=settings.Binding()))return HRESULT_FROM_WIN32(ERROR_CANCELLED);
        std::wstring path;bool desktop=false;HRESULT hr=Directory(items,path,&desktop);if(FAILED(hr))return hr;
        if(context==3&&!ReadSettings(selected,desktop?2:1).enabled)return HRESULT_FROM_WIN32(ERROR_CANCELLED);
        DWORD attributes=GetFileAttributesW(path.c_str());if(attributes==INVALID_FILE_ATTRIBUTES||!(attributes&FILE_ATTRIBUTE_DIRECTORY)||path.find(L'"')!=std::wstring::npos)return E_INVALIDARG;
        std::wstring arguments=L"--quick-task "+settings.command+L" --companion "+settings.pet+L" --directory \""+path+L"\\.\"";
        SHELLEXECUTEINFOW execute={sizeof(execute)};execute.fMask=SEE_MASK_FLAG_NO_UI;execute.lpFile=settings.exe.c_str();execute.lpParameters=arguments.c_str();execute.lpDirectory=path.c_str();execute.nShow=SW_SHOWNORMAL;
        return ShellExecuteExW(&execute)?S_OK:HRESULT_FROM_WIN32(GetLastError());
    }
    HRESULT STDMETHODCALLTYPE GetFlags(EXPCMDFLAGS* flags)override{if(!flags)return E_POINTER;*flags=slot<0&&VisibleSlots().size()>1?ECF_HASSUBCOMMANDS:ECF_DEFAULT;return S_OK;}
    HRESULT STDMETHODCALLTYPE EnumSubCommands(IEnumExplorerCommand** value)override;
    HRESULT STDMETHODCALLTYPE SetSite(IUnknown* value)override{if(site)site->Release();site=value;if(site)site->AddRef();return S_OK;}
    HRESULT STDMETHODCALLTYPE GetSite(REFIID iid,void** value)override{if(!site){*value=nullptr;return E_FAIL;}return site->QueryInterface(iid,value);}
};
class Commands final:public IEnumExplorerCommand {
    LONG refs=1;std::vector<IExplorerCommand*> commands;ULONG index=0;
public:
    explicit Commands(const std::vector<IExplorerCommand*>& values,ULONG position=0):commands(values),index(position){for(auto command:commands)command->AddRef();InterlockedIncrement(&objects);}
    ~Commands(){for(auto command:commands)command->Release();InterlockedDecrement(&objects);}
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void** value)override{if(!value)return E_POINTER;*value=nullptr;if(!IsEqualIID(iid,IID_IUnknown)&&!IsEqualIID(iid,IID_IEnumExplorerCommand))return E_NOINTERFACE;*value=this;AddRef();return S_OK;}
    ULONG STDMETHODCALLTYPE AddRef()override{return InterlockedIncrement(&refs);}
    ULONG STDMETHODCALLTYPE Release()override{LONG count=InterlockedDecrement(&refs);if(!count)delete this;return count;}
    HRESULT STDMETHODCALLTYPE Next(ULONG count,IExplorerCommand** values,ULONG* fetched)override{if(!values||(!fetched&&count!=1))return E_POINTER;ULONG actual=0;while(actual<count&&index<commands.size()){values[actual]=commands[index++];values[actual++]->AddRef();}if(fetched)*fetched=actual;return actual==count?S_OK:S_FALSE;}
    HRESULT STDMETHODCALLTYPE Skip(ULONG count)override{ULONG remaining=(ULONG)commands.size()-index;index+=count<remaining?count:remaining;return count<=remaining?S_OK:S_FALSE;}
    HRESULT STDMETHODCALLTYPE Reset()override{index=0;return S_OK;}
    HRESULT STDMETHODCALLTYPE Clone(IEnumExplorerCommand** value)override{if(!value)return E_POINTER;*value=new Commands(commands,index);return S_OK;}
};
HRESULT Command::EnumSubCommands(IEnumExplorerCommand** value){
    if(!value)return E_POINTER;*value=nullptr;if(slot>=0)return E_NOTIMPL;
    auto slots=VisibleSlots();if(slots.size()<2)return E_NOTIMPL;
    std::vector<IExplorerCommand*> commands;
    for(int selected:slots){GUID clsid=baseId;int ordinal=context==3?193+selected:selected*3+1;clsid.Data4[5]=(BYTE)(ordinal>>16);clsid.Data4[6]=(BYTE)(ordinal>>8);clsid.Data4[7]=(BYTE)ordinal;auto command=new Command(clsid);command->SetSite(site);commands.push_back(command);}
    *value=new Commands(commands);for(auto command:commands)command->Release();return S_OK;
}
class Factory final:public IClassFactory {
    LONG refs=1;GUID id;
public:
    explicit Factory(REFCLSID clsid):id(clsid){InterlockedIncrement(&objects);}
    ~Factory(){InterlockedDecrement(&objects);}
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void** value)override{if(!value)return E_POINTER;*value=nullptr;if(IsEqualIID(iid,IID_IUnknown)||IsEqualIID(iid,IID_IClassFactory)){*value=this;AddRef();return S_OK;}return E_NOINTERFACE;}
    ULONG STDMETHODCALLTYPE AddRef()override{return InterlockedIncrement(&refs);}
    ULONG STDMETHODCALLTYPE Release()override{LONG n=InterlockedDecrement(&refs);if(!n)delete this;return n;}
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer,REFIID iid,void** value)override{if(outer)return CLASS_E_NOAGGREGATION;auto command=new Command(id);HRESULT hr=command->QueryInterface(iid,value);command->Release();return hr;}
    HRESULT STDMETHODCALLTYPE LockServer(BOOL lock)override{if(lock)InterlockedIncrement(&objects);else InterlockedDecrement(&objects);return S_OK;}
};
extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid,REFIID iid,void** value){if(Ordinal(clsid)<0)return CLASS_E_CLASSNOTAVAILABLE;auto factory=new Factory(clsid);HRESULT hr=factory->QueryInterface(iid,value);factory->Release();return hr;}
extern "C" HRESULT __stdcall DllCanUnloadNow(){return objects==0?S_OK:S_FALSE;}

// SPDX-License-Identifier: Apache-2.0
// Test-only Explorer site. It does not modify or automate the real desktop.
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shobjidl.h>
#include <shlobj.h>

class FolderSite final:public IServiceProvider,public IFolderView,public IPersistFolder2 {
    LONG refs=1;PIDLIST_ABSOLUTE folder=nullptr;
public:
    FolderSite(PCWSTR path,BOOL desktop){if(desktop)SHGetSpecialFolderLocation(nullptr,CSIDL_DESKTOP,&folder);else SHParseDisplayName(path,nullptr,&folder,0,nullptr);}
    ~FolderSite(){CoTaskMemFree(folder);}
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid,void** value)override{
        if(!value)return E_POINTER;*value=nullptr;
        if(IsEqualIID(iid,IID_IUnknown)||IsEqualIID(iid,IID_IServiceProvider))*value=static_cast<IServiceProvider*>(this);
        else if(IsEqualIID(iid,IID_IFolderView))*value=static_cast<IFolderView*>(this);
        else if(IsEqualIID(iid,IID_IPersist)||IsEqualIID(iid,IID_IPersistFolder)||IsEqualIID(iid,IID_IPersistFolder2))*value=static_cast<IPersistFolder2*>(this);
        else return E_NOINTERFACE;AddRef();return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef()override{return InterlockedIncrement(&refs);}
    ULONG STDMETHODCALLTYPE Release()override{LONG count=InterlockedDecrement(&refs);if(!count)delete this;return count;}
    HRESULT STDMETHODCALLTYPE QueryService(REFGUID service,REFIID iid,void** value)override{return IsEqualGUID(service,SID_SFolderView)?QueryInterface(iid,value):E_NOINTERFACE;}
    HRESULT STDMETHODCALLTYPE GetClassID(CLSID* id)override{*id=CLSID_NULL;return S_OK;}
    HRESULT STDMETHODCALLTYPE Initialize(PCIDLIST_ABSOLUTE)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE GetCurFolder(PIDLIST_ABSOLUTE* value)override{if(!folder)return E_FAIL;*value=ILCloneFull(folder);return *value?S_OK:E_OUTOFMEMORY;}
    HRESULT STDMETHODCALLTYPE GetCurrentViewMode(UINT*)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE SetCurrentViewMode(UINT)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE GetFolder(REFIID iid,void** value)override{return QueryInterface(iid,value);}
    HRESULT STDMETHODCALLTYPE Item(int,PITEMID_CHILD*)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE ItemCount(UINT,int*)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE Items(UINT,REFIID,void**)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE GetSelectionMarkedItem(int*)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE GetFocusedItem(int*)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE GetItemPosition(PCUITEMID_CHILD,POINT*)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE GetSpacing(POINT*)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE GetDefaultSpacing(POINT*)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE GetAutoArrange()override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE SelectItem(int,DWORD)override{return E_NOTIMPL;}
    HRESULT STDMETHODCALLTYPE SelectAndPositionItems(UINT,PCUITEMID_CHILD_ARRAY,POINT*,DWORD)override{return E_NOTIMPL;}
};
extern "C" __declspec(dllexport) HRESULT __stdcall CreateFolderSite(PCWSTR path,BOOL desktop,IUnknown** value){if(!value)return E_POINTER;auto site=new FolderSite(path,desktop);*value=static_cast<IServiceProvider*>(site);return S_OK;}

// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
#if AGENT_PAL_LINUX
using JavaScriptSerializer = AgentPal.Linux.CompatibleJson;
#else
using System.Web.Script.Serialization;
#endif
namespace AgentPal
{
    // Default data-only companions reuse the validated pack renderer. They are
    // separate from the four fixed rigs and never copied over a user's packs.
    internal static class DefaultCompanionCatalog
    {
        private static readonly object gate=new object();
        private static string loadedRoot;
        private static PetCharacter[] profiles=new PetCharacter[0];
        internal static PetCharacter[] Profiles
        {
            get
            {
                string root=PetCharacter.AssetsRoot;
                lock(gate)
                {
                    if(loadedRoot!=root){loadedRoot=root;try{profiles=ReadProfiles(root);}catch{profiles=new PetCharacter[0];}}
                    return (PetCharacter[])profiles.Clone();
                }
            }
        }
        internal static PetCharacter[] ReadProfiles(string root)
        {
            string manifest=Path.Combine(root,"default-companions.json");
            if(!File.Exists(manifest))return new PetCharacter[0]; // older build/test assets
            byte[] data=CharacterPackCatalog.ReadBytes(manifest,8192);
            string[] ids=new JavaScriptSerializer().Deserialize<string[]>(System.Text.Encoding.UTF8.GetString(data).TrimStart('\uFEFF'));
            if(ids==null||ids.Length>CharacterPackCatalog.MaxPacks)throw new InvalidDataException("Invalid default companion index.");
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var result=new List<PetCharacter>();
            foreach(string id in ids)
            {
                if(!CharacterPackCatalog.ValidId(id)||!seen.Add(id))throw new InvalidDataException("Invalid default companion ID.");
                CharacterAnimationSet images=null;
                try
                {
                    var profile=CharacterPackCatalog.ReadPack(Path.Combine(root,"bundled",id),false,out images);
                    if(profile.Id!=id)throw new InvalidDataException("Default companion ID differs from its index.");
                    profile.IsBundled=true;
                    result.Add(profile);
                }
                finally{if(images!=null)images.Dispose();}
            }
            return result.ToArray();
        }
    }
}

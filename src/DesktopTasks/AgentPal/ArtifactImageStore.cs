// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

namespace AgentPal
{
    internal sealed class ArtifactImageStore
    {
        private readonly string directory;

        internal ArtifactImageStore(string stateDirectory)
        { directory = Path.Combine(stateDirectory, "flight-artwork"); }

        private string PathFor(string characterId)
        { return Path.Combine(directory, characterId + ".dat"); }

        internal bool Contains(string characterId)
        { return File.Exists(PathFor(characterId)); }

        internal void Save(string characterId, string source, bool wings)
        {
            using (Bitmap image = CharacterPackFromImages.ReadArtwork(source,
                UiLanguage.Text(wings ? "玄翼" : "灵器"), CharacterPackCatalog.MaxArtifactEdge,
                CharacterPackCatalog.MaxArtifactEdge, CancellationToken.None))
            using (var data = new MemoryStream())
            {
                image.Save(data, ImageFormat.Png);
                byte[] png = data.ToArray();
                CharacterPackCatalog.ReadArtifactPng(png, false);
                Directory.CreateDirectory(directory);
                string target = PathFor(characterId), stage = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (FileStream output = new FileStream(stage, FileMode.CreateNew, FileAccess.Write))
                    {
                        output.WriteByte(wings ? (byte)1 : (byte)2);
                        output.Write(png, 0, png.Length);
                    }
                    if (File.Exists(target)) File.Replace(stage, target, null);
                    else File.Move(stage, target);
                }
                finally { if (File.Exists(stage)) File.Delete(stage); }
            }
        }

        internal Image Load(string characterId, out bool wings)
        {
            wings = false;
            string path = PathFor(characterId);
            if (!File.Exists(path)) return null;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                {
                    int mode = stream.ReadByte();
                    if (stream.Length <= 1 || stream.Length > CharacterPackCatalog.MaxFrameBytes + 1 || (mode != 1 && mode != 2))
                        return null;
                    using (var reader = new BinaryReader(stream))
                    {
                        byte[] png = reader.ReadBytes((int)stream.Length - 1);
                        Image image = CharacterPackCatalog.ReadArtifactPng(png, true);
                        wings = mode == 1;
                        return image;
                    }
                }
            }
            catch (IOException) { return null; }
            catch (InvalidDataException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
            catch (System.Runtime.InteropServices.ExternalException) { return null; }
        }

        internal void Reset(string characterId)
        { File.Delete(PathFor(characterId)); }
    }
}

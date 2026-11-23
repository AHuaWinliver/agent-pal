// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace AgentPal
{
    internal static partial class CharacterPackFromImages
    {
        private const int Width = 384, Height = 416, Baseline = 395;
        private const int WorkSize = 768;

        internal static CharacterPackDraft Create(string root, string name, string front, string left, string right,
            CancellationToken cancellation, string artifact = null, bool wings = false, string description = null)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Trim().Length > 20 || name.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                throw new InvalidDataException(UiLanguage.Text("请输入 1 到 20 个字符的角色名。"));
            if (description != null && (description.Length > 4000 || description.IndexOf('\0') >= 0))
                throw new InvalidDataException(UiLanguage.Text("人物描述最多 4000 个字符。"));
            string id = "custom-" + Guid.NewGuid().ToString("N").Substring(0, 16);
            Sprite frontSprite = ReadView(front, UiLanguage.Text("正面"), cancellation);
            Sprite leftSprite = ReadView(left, UiLanguage.Text("左侧"), cancellation);
            Sprite rightSprite = ReadView(right, UiLanguage.Text("右侧"), cancellation);
            var manifest = new CharacterPackManifest
            {
                schemaVersion = 1, id = id, version = "1.0.0", name = name.Trim(),
                canvasWidth = Width, canvasHeight = Height, mouthX = .5f, mouthY = .43f,
                reminderStyle = "beam",
                persona = String.IsNullOrWhiteSpace(description)
                    ? UiLanguage.Text("友好、好奇的桌面伙伴，回答清楚，给出有用的下一步。") : description.Trim(),
                quotes = new[]
                {
                    UiLanguage.Text("今天一起做点有趣的事吧。"), UiLanguage.Text("我在这里。"),
                    UiLanguage.Text("累了就休息一下。"), UiLanguage.Text("我们一步一步来。"),
                    UiLanguage.Text("好，准备出发。")
                },
                files = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
            };
            using (var leftGait = new SideGait(leftSprite.SourceWidth, leftSprite.SourceHeight, leftSprite.Pixels, -1))
            using (var rightGait = new SideGait(rightSprite.SourceWidth, rightSprite.SourceHeight, rightSprite.Pixels, 1))
                return CharacterPackDraft.Prepare(root, id, manifest.version, delegate(string folder)
            {
                long bytes = 0;
                using (var hash = SHA256.Create())
                {
                    if (!String.IsNullOrEmpty(artifact))
                    {
                        string filename = wings ? CharacterPackCatalog.WingsFile : CharacterPackCatalog.PlatformFile;
                        string path = Path.Combine(folder, filename);
                        using (Bitmap artwork = ReadArtwork(artifact, UiLanguage.Text(wings ? "玄翼" : "灵器"),
                            CharacterPackCatalog.MaxArtifactEdge, CharacterPackCatalog.MaxArtifactEdge, cancellation))
                            artwork.Save(path, ImageFormat.Png);
                        using (FileStream data = File.OpenRead(path))
                        {
                            bytes += data.Length;
                            manifest.files.Add(filename, BitConverter.ToString(hash.ComputeHash(data)).Replace("-", ""));
                        }
                    }
                    foreach (string state in CharacterPackCatalog.States)
                    {
                        SideGait gait = state == "running-left" ? leftGait : state == "running-right" ? rightGait : null;
                        string directory = Path.Combine(folder, state);
                        Directory.CreateDirectory(directory);
                        int count = CharacterPackCatalog.FrameCount(state);
                        for (int i = 0; i < count; i++)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            string frame = i.ToString("00") + ".png";
                            string filename = Path.Combine(directory, frame);
                            using (Bitmap image = gait == null ? frontSprite.Render(state, i, count) :
                                gait.Render(i / (double)count)) image.Save(filename, ImageFormat.Png);
                            using (FileStream data = File.OpenRead(filename))
                            {
                                bytes += data.Length;
                                if (bytes + CharacterPackCatalog.MaxManifestBytes > CharacterPackCatalog.MaxPackBytes)
                                    throw new InvalidDataException(UiLanguage.Text("生成的角色包过大，请使用细节较少的图片。"));
                                manifest.files.Add(state + "/" + frame, BitConverter.ToString(hash.ComputeHash(data)).Replace("-", ""));
                            }
                        }
                    }
                }
                var serializer = new JavaScriptSerializer();
                File.WriteAllText(Path.Combine(folder, "character.json"), serializer.Serialize(manifest), new UTF8Encoding(false));
            }, cancellation);
        }

        private static Sprite ReadView(string path, string label, CancellationToken cancellation)
        {
            using (Bitmap image = ReadArtwork(path, label, 286, 316, cancellation))
                return new Sprite(image.Width, image.Height, ReadPixels(image));
        }

        internal static Bitmap ReadArtwork(string path, string label, int maxWidth, int maxHeight, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                using (FileStream file = File.OpenRead(path))
                {
                    if (file.Length > 16 * 1024 * 1024) throw InvalidImage();
                    using (Image image = Image.FromStream(file, true, false))
                    {
                        Guid format = image.RawFormat.Guid;
                        if ((format != ImageFormat.Png.Guid && format != ImageFormat.Jpeg.Guid) ||
                            image.Width > 4096 || image.Height > 4096 || image.Width < 8 || image.Height < 8)
                            throw InvalidImage();
                        RotatePhoto(image);
                        double factor = Math.Min(1, WorkSize / (double)Math.Max(image.Width, image.Height));
                        using (var resized = new Bitmap((int)Math.Round(image.Width * factor), (int)Math.Round(image.Height * factor), PixelFormat.Format32bppArgb))
                        {
                            DrawImage(resized, image, new Rectangle(Point.Empty, resized.Size), new Rectangle(Point.Empty, image.Size));
                            int[] pixels = ReadPixels(resized);
                            RemoveSolidBackground(pixels, resized.Width, resized.Height);
                            Rectangle bounds = AlphaBounds(pixels, resized.Width, resized.Height);
                            if (bounds.Width < 5 || bounds.Height < 5)
                                throw new InvalidDataException(UiLanguage.Text("图片中找不到完整的角色轮廓。"));
                            WritePixels(resized, pixels);
                            double scale = Math.Min(maxWidth / (double)bounds.Width, maxHeight / (double)bounds.Height);
                            int width = Math.Max(1, (int)Math.Round(bounds.Width * scale));
                            int height = Math.Max(1, (int)Math.Round(bounds.Height * scale));
                            var normalized = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                            try
                            {
                                DrawImage(normalized, resized, new Rectangle(0, 0, width, height), bounds);
                                return normalized;
                            }
                            catch { normalized.Dispose(); throw; }
                        }
                    }
                }
            }
            catch (InvalidDataException ex) { throw ViewError(label, ex); }
            catch (IOException ex) { throw ViewError(label, ex); }
            catch (ArgumentException) { throw ViewError(label, InvalidImage()); }
            catch (ExternalException ex) { throw ViewError(label, ex); }
        }

        private static InvalidDataException ViewError(string label, Exception error)
        { return new InvalidDataException(label + " — " + error.Message, error); }

        private static InvalidDataException InvalidImage()
        { return new InvalidDataException(UiLanguage.Text("请选择不超过 16 MB、最长边不超过 4096 像素的 PNG 或 JPEG。")); }

        private static void RotatePhoto(Image image)
        {
            const int orientationKey = 0x112;
            if (Array.IndexOf(image.PropertyIdList, orientationKey) < 0) return;
            byte[] value = image.GetPropertyItem(orientationKey).Value;
            if (value == null || value.Length < 2) return;
            switch (BitConverter.ToUInt16(value, 0))
            {
                case 2: image.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
                case 3: image.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                case 4: image.RotateFlip(RotateFlipType.Rotate180FlipX); break;
                case 5: image.RotateFlip(RotateFlipType.Rotate90FlipX); break;
                case 6: image.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                case 7: image.RotateFlip(RotateFlipType.Rotate270FlipX); break;
                case 8: image.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
            }
        }

        private static void DrawImage(Bitmap target, Image source, Rectangle destination, Rectangle crop)
        {
            using (Graphics g = Graphics.FromImage(target))
            using (var edges = new ImageAttributes())
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                edges.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(source, destination, crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel, edges);
            }
        }

        private static bool Similar(int color, int background)
        {
            return Math.Abs(((color >> 16) & 255) - ((background >> 16) & 255)) <= 35 &&
                Math.Abs(((color >> 8) & 255) - ((background >> 8) & 255)) <= 35 &&
                Math.Abs((color & 255) - (background & 255)) <= 35;
        }

        private static void RemoveSolidBackground(int[] pixels, int width, int height)
        {
            // A source with real transparency already contains its own matte.
            foreach (int pixel in pixels) if (unchecked((uint)pixel) >> 24 < 20) return;
            int[] corners = { pixels[0], pixels[width - 1], pixels[(height - 1) * width], pixels[pixels.Length - 1] };
            int background = corners[0], best = 0;
            foreach (int color in corners)
            {
                int same = 0;
                foreach (int other in corners) if (Similar(color, other)) same++;
                if (same > best) { best = same; background = color; }
            }
            if (best < 3) throw NeedMatte();
            int[] frontier = new int[pixels.Length];
            int head = 0, tail = 0;
            for (int x = 0; x < width; x++)
            {
                Flood(pixels, frontier, ref tail, x, background);
                Flood(pixels, frontier, ref tail, (height - 1) * width + x, background);
            }
            for (int y = 1; y < height - 1; y++)
            {
                Flood(pixels, frontier, ref tail, y * width, background);
                Flood(pixels, frontier, ref tail, y * width + width - 1, background);
            }
            if (tail * 10 < (2 * width + 2 * height - 4) * 7) throw NeedMatte();
            while (head < tail)
            {
                int index = frontier[head++];
                if (index % width != 0) Flood(pixels, frontier, ref tail, index - 1, background);
                if (index % width != width - 1) Flood(pixels, frontier, ref tail, index + 1, background);
                if (index >= width) Flood(pixels, frontier, ref tail, index - width, background);
                if (index < pixels.Length - width) Flood(pixels, frontier, ref tail, index + width, background);
            }
        }

        private static void Flood(int[] pixels, int[] frontier, ref int tail, int index, int background)
        {
            if (pixels[index] == 0 || !Similar(pixels[index], background)) return;
            pixels[index] = 0;
            frontier[tail++] = index;
        }

        private static InvalidDataException NeedMatte()
        { return new InvalidDataException(UiLanguage.Text("请使用透明背景或纯色背景的全身图。")); }

        private static Rectangle AlphaBounds(int[] pixels, int width, int height)
        {
            int left = width, top = height, right = 0, bottom = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (unchecked((uint)pixels[y * width + x]) >> 24 > 16)
                    {
                        left = Math.Min(left, x); top = Math.Min(top, y);
                        right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1);
                    }
            return right <= left || bottom <= top ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static int[] ReadPixels(Bitmap bitmap)
        {
            var pixels = new int[bitmap.Width * bitmap.Height];
            AccessPixels(bitmap, pixels, false);
            return pixels;
        }

        private static void WritePixels(Bitmap bitmap, int[] pixels)
        { AccessPixels(bitmap, pixels, true); }

        private static void AccessPixels(Bitmap bitmap, int[] pixels, bool write)
        {
            BitmapData data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size),
                write ? ImageLockMode.WriteOnly : ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    IntPtr row = IntPtr.Add(data.Scan0, data.Stride * y);
                    if (write) Marshal.Copy(pixels, y * bitmap.Width, row, bitmap.Width);
                    else Marshal.Copy(row, pixels, y * bitmap.Width, bitmap.Width);
                }
            }
            finally { bitmap.UnlockBits(data); }
        }

        private sealed class Sprite
        {
            private readonly int width, height;
            private readonly int[] pixels;
            internal int SourceWidth { get { return width; } }
            internal int SourceHeight { get { return height; } }
            internal int[] Pixels { get { return pixels; } }

            internal Sprite(int width, int height, int[] pixels)
            { this.width = width; this.height = height; this.pixels = pixels; }

            internal Bitmap Render(string state, int frame, int count)
            {
                double angle = frame * 2.0 * Math.PI / count;
                double swing = Math.Sin(angle), lift = .5 - .5 * Math.Cos(angle);
                double zoom = 1, hop = 0, lean = 0, drift = 0;
                if (state == "running") { zoom += .009 * Math.Cos(angle); lean = 2 * swing; drift = 1.5 * Math.Cos(angle); }
                else if (state == "jumping") { hop = 20 * lift; zoom -= .018 * lift; }
                else if (state == "eating" || state == "punching") { zoom += .04 * lift; lean = 6 * swing; }
                else if (state == "failed") { zoom -= .025 * lift; lean = -3 * lift; }
                else if (state == "waving" || state == "review" || state == "wiping-mouth") { lean = 7 * swing; zoom += .012 * lift; }
                else zoom += .011 * lift;

                // Inverse-sample the entire view. No limb is cut out, mirrored
                // or painted over another limb; each image owns its occlusion.
                var output = new int[Width * Height];
                for (int y = 0; y < Height; y++)
                {
                    double row = height + (y - Baseline + hop) / zoom;
                    if (row <= -1 || row >= height) continue;
                    double depth = row / height;
                    double shift = drift + lean * (1 - depth);
                    int top = (int)Math.Floor(row);
                    int vertical = (int)((row - top) * 256);
                    int first = Math.Max(0, (int)Math.Floor((Width - width * zoom) / 2 + shift) - 2);
                    int last = Math.Min(Width, (int)Math.Ceiling((Width + width * zoom) / 2 + shift) + 2);
                    for (int x = first; x < last; x++)
                    {
                        double column = width * .5 + (x - Width * .5 - shift) / zoom;
                        if (column <= -1 || column >= width) continue;
                        int left = (int)Math.Floor(column);
                        int horizontal = (int)((column - left) * 256);
                        int a = Mix(Pixel(left, top), Pixel(left + 1, top), horizontal);
                        int b = Mix(Pixel(left, top + 1), Pixel(left + 1, top + 1), horizontal);
                        output[y * Width + x] = Mix(a, b, vertical);
                    }
                }
                var result = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                try { WritePixels(result, output); return result; }
                catch { result.Dispose(); throw; }
            }

            private int Pixel(int x, int y)
            { return x < 0 || x >= width || y < 0 || y >= height ? 0 : pixels[y * width + x]; }

            private static int Mix(int a, int b, int weight)
            {
                int other = 256 - weight;
                // Blend red/blue and alpha/green in pairs without crossing channels.
                int rb = ((((a & 0x00ff00ff) * other) + ((b & 0x00ff00ff) * weight)) >> 8) & 0x00ff00ff;
                int ag = (((((a >> 8) & 0x00ff00ff) * other) + (((b >> 8) & 0x00ff00ff) * weight)) >> 8) & 0x00ff00ff;
                return rb | (ag << 8);
            }
        }
    }
}

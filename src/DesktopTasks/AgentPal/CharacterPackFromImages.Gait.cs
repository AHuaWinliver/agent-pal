// SPDX-License-Identifier: Apache-2.0
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace AgentPal
{
    internal static partial class CharacterPackFromImages
    {
        // A profile drawing is a flattened image. Keep its head and garment in
        // place, take the visible sleeve and footwear from the same drawing,
        // and put the inferred far limbs behind it.
        private sealed class SideGait : IDisposable
        {
            private readonly Bitmap body, sleeve, boot, farSleeve, farBoot;
            private readonly int facing;
            private readonly float offsetX, offsetY, height;
            private readonly PointF shoulder, hip;
            private readonly float sleeveAngle, ankleX, bootSocket, shinWidth, stride, legLink;
            private readonly Color outerLeg, innerLeg;
            private const double Support = .60;

            internal SideGait(int width, int height, int[] pixels, int facing)
            {
                this.facing = facing;
                this.height = height;
                offsetX = (Width - width) / 2f;
                offsetY = Baseline - height;
                int[] bodyPixels = (int[])pixels.Clone();
                sleeve = CutSleeve(width, height, pixels, bodyPixels, out shoulder, out sleeveAngle);
                int soleStart = FindBootTop(width, height, pixels);
                boot = CutBoot(width, height, pixels, soleStart, out ankleX);
                bootSocket = BootSocket(boot);
                int hem = FindHem(width, height, pixels, soleStart);
                hip = new PointF(RowCenter(width, height, pixels, (int)(height * .56f)), height * .55f);
                shinWidth = Math.Max(2, Math.Min(boot.Width * .50f, height * .078f));
                stride = Math.Min(height * (hem > height * .81f ? .086f : .115f), 34);
                // Place the two boot cuffs around the position of the visible
                // ankle. Keep upper-leg roots just inside the original hip.
                hip.X = HipCenter(hip.X, ankleX, width);
                float reachX = Math.Abs(ankleX - hip.X) + stride + 1;
                float reachY = height - boot.Height + 3 - hip.Y;
                legLink = (float)Math.Sqrt(reachX * reachX + reachY * reachY) / 2;
                outerLeg = Fabric(width, height, pixels, (int)(height * .73f), (int)hip.X);
                innerLeg = Darken(outerLeg, .71f);
                Array.Clear(bodyPixels, hem * width, (height - hem) * width);
                body = MakeBitmap(width, height, bodyPixels);
                farSleeve = Shade(sleeve, .78f);
                farBoot = Shade(boot, .77f);
            }

            internal Bitmap Render(double phase)
            {
                var canvas = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (Graphics g = Graphics.FromImage(canvas))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.TranslateTransform(offsetX, offsetY);
                        float bob = (float)(-.8 * Math.Sin(4 * Math.PI * phase));
                        float sway = (float)(.9 * Math.Sin(2 * Math.PI * phase));
                        double far = (phase + .5) % 1;
                        DrawLeg(g, far, false, sway, bob);
                        DrawLeg(g, phase, true, sway, bob);
                        DrawSleeve(g, far, false, sway, bob);
                        g.DrawImage(body, new RectangleF(sway, bob, body.Width, body.Height));
                        DrawSleeve(g, phase, true, sway, bob);
                    }
                    return canvas;
                }
                catch { canvas.Dispose(); throw; }
            }

            private void DrawLeg(Graphics g, double phase, bool near, float sway, float bob)
            {
                double swing = Math.Max(0, (phase - Support) / (1 - Support));
                float travel = (float)(phase < Support ? 1 - 2 * ContactProgress(phase / Support) :
                    2 * CompanionMotion.Smooth(swing) - 1);
                double air = Math.Sin(Math.PI * swing);
                float lift = (float)(air * air * Math.Min(height * .051f, 15));
                float depth = facing * (near ? 1 : -1) * Math.Min(3, height * .012f);
                float cuffX = ankleX + facing * stride * travel + depth;
                float soleY = height - lift;
                double pitchLimit = Math.Atan2(lift, Math.Max(bootSocket, boot.Width - bootSocket)) * 180 / Math.PI;
                double pitch = facing * air * 8 * Math.Cos(Math.PI * swing);
                pitch = Math.Max(-pitchLimit, Math.Min(pitchLimit, pitch));
                // The sole stays on the same baseline through stance. Rotation is
                // enabled only once the shoe has clearance from the ground.
                var ankle = new PointF(cuffX, soleY - boot.Height + 2);
                var root = new PointF(hip.X + depth + sway, hip.Y + bob);
                PointF knee = BendKnee(root, ankle, facing, legLink);
                Color cloth = near ? outerLeg : innerLeg;
                DrawBone(g, root, knee, shinWidth * 1.22f, shinWidth, cloth);
                DrawBone(g, knee, ankle, shinWidth, shinWidth * .76f, cloth);
                GraphicsState saved = g.Save();
                g.TranslateTransform(cuffX, soleY);
                g.RotateTransform((float)pitch);
                g.DrawImage(near ? boot : farBoot, new RectangleF(-bootSocket, -boot.Height, boot.Width, boot.Height));
                g.Restore(saved);
            }

            private static double ContactProgress(double t)
            {
                const double ease = .12;
                if (t > 1 - ease) return 1 - ContactProgress(1 - t);
                if (t < ease) return t * t / (2 * ease * (1 - ease));
                return (t - ease / 2) / (1 - ease);
            }

            private void DrawSleeve(Graphics g, double legPhase, bool near, float sway, float bob)
            {
                Bitmap texture = near ? sleeve : farSleeve;
                float counter = (float)Math.Cos(2 * Math.PI * legPhase - .22);
                // The contact leg reaches forward close to phase zero; the
                // arm on its own side therefore swings backwards.
                float upper = facing * 13 * counter + sleeveAngle;
                double elbow = facing * (10 + 8 * .5 * (1 - counter));
                PointF[] path = CompanionMotion.ArmPath(upper, elbow, texture.Height, near, "running", legPhase, 24);
                float rootX = shoulder.X + sway + (near ? 0 : facing * Math.Min(height * .024f, body.Width * .12f));
                float rootY = shoulder.Y + bob + (near ? 0 : height * .012f);
                DrawSleeveBands(g, texture, path, rootX, rootY, near ? 1f : .85f);
            }

            private static void DrawSleeveBands(Graphics g, Bitmap image, PointF[] path, float x, float y, float scale)
            {
                using (var sampling = new ImageAttributes())
                {
                    sampling.SetWrapMode(WrapMode.TileFlipXY);
                    int bands = path.Length - 1;
                    float halfWidth = image.Width * scale * .5f;
                    for (int i = 0; i < bands; i++)
                    {
                        PointF a = path[i], b = path[i + 1];
                        float dx = b.X - a.X, dy = b.Y - a.Y;
                        float length = (float)Math.Sqrt(dx * dx + dy * dy);
                        float nx = dy * halfWidth / length, ny = -dx * halfWidth / length;
                        float end = i + 1 < bands ? .20f : 0;
                        var corners = new[]
                        {
                            new PointF(x+a.X-nx, y+a.Y-ny), new PointF(x+a.X+nx, y+a.Y+ny),
                            new PointF(x+b.X+dx*end-nx, y+b.Y+dy*end-ny)
                        };
                        float startRow = i * image.Height / (float)bands;
                        float rows = image.Height / (float)bands;
                        g.DrawImage(image, corners, new RectangleF(0, startRow, image.Width, rows * (1 + end)), GraphicsUnit.Pixel, sampling);
                    }
                }
            }

            private static PointF BendKnee(PointF start, PointF end, int facing, float link)
            {
                float dx = end.X - start.X, dy = end.Y - start.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                float bend = (float)Math.Sqrt(Math.Max(1, link * link - distance * distance / 4));
                return new PointF((start.X + end.X) / 2 + facing * bend * dy / Math.Max(1, distance),
                    (start.Y + end.Y) / 2 - facing * bend * dx / Math.Max(1, distance));
            }

            private static void DrawBone(Graphics g, PointF a, PointF b, float startWidth, float endWidth, Color cloth)
            {
                float dx = b.X - a.X, dy = b.Y - a.Y;
                float length = Math.Max(1, (float)Math.Sqrt(dx * dx + dy * dy));
                float nx = dy / length, ny = -dx / length;
                PointF[] contour =
                {
                    new PointF(a.X - nx * startWidth / 2, a.Y - ny * startWidth / 2),
                    new PointF(a.X + nx * startWidth / 2, a.Y + ny * startWidth / 2),
                    new PointF(b.X + nx * endWidth / 2, b.Y + ny * endWidth / 2),
                    new PointF(b.X - nx * endWidth / 2, b.Y - ny * endWidth / 2)
                };
                using (var fill = new SolidBrush(cloth))
                {
                    g.FillPolygon(fill, contour);
                    g.FillEllipse(fill, b.X - endWidth / 2, b.Y - endWidth / 2, endWidth, endWidth);
                }
            }

            private static int FindBootTop(int width, int height, int[] pixels)
            {
                int start = Math.Max(1, (int)(height * .90f)), selected = start, narrowest = width + 1;
                for (int row = start; row <= height * .946f; row++)
                {
                    int span = RowWidth(width, height, pixels, row);
                    if (span >= Math.Max(2, width / 12) && span < narrowest)
                    { selected = row; narrowest = span; }
                }
                return selected;
            }

            private static int FindHem(int width, int height, int[] pixels, int soleStart)
            {
                int hemWidth = RowWidth(width, height, pixels, (int)(height * .87f));
                int ankleWidth = RowWidth(width, height, pixels, soleStart);
                if (ankleWidth > 0 && hemWidth >= ankleWidth * 1.55)
                    return soleStart;
                // Short garments expose the leg above the shoe.
                return Math.Max(1, (int)(height * .66f));
            }

            private static Bitmap CutBoot(int width, int height, int[] pixels, int start, out float ankle)
            {
                float center = RowCenter(width, height, pixels, Math.Min(height - 1, start + 1));
                int footRow = Math.Max(start, height - Math.Max(2, height / 35));
                // A strictly side-on frame usually has one visible shoe. If
                // the source shows two, use the run closest to the ankle.
                int left, right;
                NearestRun(width, pixels, footRow, center, out left, out right);
                float shoeWidth = Math.Max(right - left + 1, height * .055f);
                int cropLeft = Math.Max(0, (int)Math.Min(left - 2, center - shoeWidth * .46f));
                int cropRight = Math.Min(width, (int)Math.Ceiling(Math.Max(right + 3, center + shoeWidth * .47f)));
                int cutWidth = Math.Max(1, cropRight - cropLeft), cutHeight = height - start;
                int[] texture = new int[cutWidth * cutHeight];
                for (int row = 0; row < cutHeight; row++)
                    Array.Copy(pixels, (row + start) * width + cropLeft, texture, row * cutWidth, cutWidth);
                ankle = center;
                return MakeBitmap(cutWidth, cutHeight, texture);
            }

            private static float BootSocket(Bitmap texture)
            {
                int row = Math.Min(texture.Height - 1, 2), count = 0, total = 0;
                for (int x = 0; x < texture.Width; x++) if (texture.GetPixel(x, row).A > 80) { count++; total += x; }
                return count == 0 ? texture.Width * .5f : total / (float)count;
            }

            private static Bitmap CutSleeve(int width, int height, int[] pixels, int[] bodyPixels,
                out PointF shoulder, out float angle)
            {
                PointF hand = FindHand(width, height, pixels);
                int top = Math.Max(1, (int)(hand.Y - height * .30f));
                int bottom = Math.Min(height, (int)(hand.Y + height * .038f));
                float chest = RowCenter(width, height, pixels, top + 4);
                shoulder = new PointF((chest + hand.X) / 2, top + 6);
                angle = (float)(-Math.Atan2(hand.X - shoulder.X, hand.Y - top) * 180 / Math.PI);
                int radius = Math.Max(2, Math.Min((int)(height * .071f), (int)(RowWidth(width, height, pixels, top + 7) * .29f)));
                int textureWidth = 2 * radius + 1, textureHeight = bottom - top;
                int[] texture = new int[textureWidth * textureHeight];
                for (int row = top; row < bottom; row++)
                {
                    float u = (row - top) / (float)Math.Max(1, textureHeight - 1);
                    int center = (int)Math.Round(shoulder.X + (hand.X - shoulder.X) * CompanionMotion.Smooth(u));
                    float localRadius = radius * (u < .16f ? .68f + 2 * u : u > .73f ? 1 - 2.1f * (u - .73f) : 1);
                    if (u > .84f) localRadius = Math.Max(localRadius, radius * .56f);
                    int left = Math.Max(0, center - (int)Math.Ceiling(localRadius));
                    int right = Math.Min(width - 1, center + (int)Math.Ceiling(localRadius));
                    int behindLeft = FindFabric(pixels, width, row, left - 1, -1);
                    int behindRight = FindFabric(pixels, width, row, right + 1, 1);
                    for (int x = left; x <= right; x++)
                    {
                        double cap = CompanionMotion.Smooth(u / .10);
                        int alpha = (int)(255 * cap * Math.Max(0, Math.Min(1, (localRadius - Math.Abs(x - center)) / 2)));
                        int source = pixels[row * width + x];
                        texture[(row - top) * textureWidth + x - center + radius] = Fade(source, alpha);
                        if (behindLeft == 0 && behindRight == 0) continue;
                        int shade = behindLeft == 0 ? behindRight : behindRight == 0 ? behindLeft :
                            Lerp(behindLeft, behindRight, (x - left) * 255 / Math.Max(1, right - left));
                        if (Opaque(source)) bodyPixels[row * width + x] = Lerp(source, shade, alpha);
                    }
                }
                return MakeBitmap(textureWidth, textureHeight, texture);
            }

            private static PointF FindHand(int width, int height, int[] pixels)
            {
                int top = (int)(height * .43f), bottom = (int)(height * .71f);
                int[] rowSkin = new int[height], centers = new int[height];
                for (int y = top; y <= bottom; y++)
                    for (int x = 0; x < width; x++)
                        if (Skin(pixels[y * width + x])) { rowSkin[y]++; centers[y] += x; }
                int window = Math.Max(2, (int)(height * .033f));
                int best = 0, bestX = 0, bestY = 0;
                for (int y = top; y < bottom - window; y++)
                {
                    int count = 0, weight = 0;
                    for (int i = y; i < y + window; i++) { count += rowSkin[i]; weight += centers[i]; }
                    if (count >= best) { best = count; bestX = weight; bestY = y + window / 2; }
                }
                if (best >= window * Math.Max(2, width / 36))
                    return new PointF(bestX / (float)best, bestY);
                int rest = (int)(height * .60f);
                return new PointF(RowCenter(width, height, pixels, rest), rest);
            }

            private static bool Skin(int pixel)
            {
                if (!Opaque(pixel)) return false;
                int r = (pixel >> 16) & 255, g = (pixel >> 8) & 255, b = pixel & 255;
                return r >= 100 && r - g >= 16 && g - b >= 2 && g - b < 47 && r - b < 115;
            }

            private static int FindFabric(int[] pixels, int width, int row, int start, int direction)
            {
                for (int x = start; x >= 0 && x < width; x += direction)
                {
                    int pixel = pixels[row * width + x];
                    if (Opaque(pixel) && !Skin(pixel)) return pixel;
                }
                return 0;
            }

            private static Color Fabric(int width, int height, int[] pixels, int row, int center)
            {
                int r = 0, g = 0, b = 0, samples = 0, radius = Math.Max(2, width / 15);
                for (int y = Math.Max(0, row - radius); y < Math.Min(height, row + radius); y++)
                    for (int x = Math.Max(0, center - radius); x < Math.Min(width, center + radius); x++)
                    {
                        int c = pixels[y * width + x];
                        if (!Opaque(c)) continue;
                        r += (c >> 16) & 255; g += (c >> 8) & 255; b += c & 255; samples++;
                    }
                return samples == 0 ? Color.FromArgb(52, 51, 57) : Color.FromArgb(r / samples, g / samples, b / samples);
            }

            private static float HipCenter(float torso, float ankle, int width)
            { return Math.Max(1, Math.Min(width - 1, .55f * torso + .45f * ankle)); }

            private static bool Opaque(int value) { return (uint)value >> 24 > 130; }

            private static void NearestRun(int width, int[] pixels, int row, float center, out int left, out int right)
            {
                left = right = Math.Max(0, Math.Min(width - 1, (int)center));
                float best = Single.MaxValue;
                for (int x = 0; x < width; x++)
                {
                    if (!Opaque(pixels[row * width + x])) continue;
                    int start = x;
                    while (x + 1 < width && Opaque(pixels[row * width + x + 1])) x++;
                    float distance = Math.Abs((x + start) / 2f - center) - (x - start) * .18f;
                    if (distance < best) { best = distance; left = start; right = x; }
                }
            }

            private static int RowWidth(int width, int height, int[] pixels, int row)
            {
                row = Math.Max(0, Math.Min(height - 1, row));
                int count = 0;
                for (int x = 0; x < width; x++) if (Opaque(pixels[row * width + x])) count++;
                return count;
            }

            private static float RowCenter(int width, int height, int[] pixels, int row)
            {
                row = Math.Max(0, Math.Min(height - 1, row));
                int count = 0, total = 0;
                for (int x = 0; x < width; x++)
                    if (Opaque(pixels[row * width + x])) { count++; total += x; }
                return count == 0 ? width * .5f : total / (float)count;
            }

            private static int Lerp(int a, int b, int fraction)
            {
                int result = 0;
                for (int shift = 0; shift < 32; shift += 8)
                    result |= (((((a >> shift) & 255) * (255 - fraction)) + (((b >> shift) & 255) * fraction)) / 255) << shift;
                return result;
            }

            private static int Fade(int pixel, int alpha) { return Lerp(0, pixel, alpha); }

            private static Bitmap MakeBitmap(int width, int height, int[] pixels)
            {
                var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                try { WritePixels(bitmap, pixels); return bitmap; }
                catch { bitmap.Dispose(); throw; }
            }

            private static Color Darken(Color color, float factor)
            { return Color.FromArgb((int)(color.R * factor), (int)(color.G * factor), (int)(color.B * factor)); }

            private static Bitmap Shade(Bitmap source, float amount)
            {
                int[] pixels = ReadPixels(source);
                for (int i = 0; i < pixels.Length; i++)
                {
                    int p = pixels[i];
                    pixels[i] = (p & unchecked((int)0xff000000)) |
                        ((int)(((p >> 16) & 255) * amount) << 16) |
                        ((int)(((p >> 8) & 255) * amount) << 8) | (int)((p & 255) * amount);
                }
                return MakeBitmap(source.Width, source.Height, pixels);
            }

            public void Dispose()
            { body.Dispose(); sleeve.Dispose(); boot.Dispose(); farSleeve.Dispose(); farBoot.Dispose(); }
        }
    }
}

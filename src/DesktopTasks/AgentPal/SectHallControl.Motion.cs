// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AgentPal
{
    internal sealed partial class SectHallControl
    {
        private readonly SectHallMotion motion = new SectHallMotion();
        private readonly Dictionary<string,Rectangle> actorHitBoxes = new Dictionary<string,Rectangle>();
        private double? previewTime;
        internal void SetPreviewTime(double? seconds) { previewTime = seconds; Invalidate(); }
        private void DrawActiveMembers(Graphics graphics, CompanionSect sect, SectRun run)
        {
            double now = previewTime ?? elapsed.Elapsed.TotalSeconds;
            var members = SectMemberStatus.Build(sect, run);
            var homes = new Dictionary<string, PointF>(); var scales = new Dictionary<string, float>();
            float unit = Math.Max(1, Font.Height / 16f);
            foreach (SectMemberStatus member in members)
            {
                Rectangle box = seats[member.Name];
                float scale = Math.Max(.01f, Math.Min(1.5f * unit, Math.Min(box.Width / SectHallLayout.WorkstationWidth, box.Height / SectHallLayout.WorkstationHeight)));
                float x = box.Left + (box.Width - 220 * scale) / 2, y = box.Top + (box.Height - SectHallLayout.WorkstationHeight * scale) / 2;
                homes[member.Name] = new PointF(x + 110 * scale, y + 229 * scale); scales[member.Name] = scale;
            }
            var actors = motion.Sample(run, members, homes, scales, now, UiMotion.ReducedMotion);
            actorHitBoxes.Clear();
            var layers=new List<HallLayer>();
            foreach(var member in members){layers.Add(new HallLayer{Depth=actors[member.Name].Feet.Y,Member=member});layers.Add(new HallLayer{Depth=homes[member.Name].Y+.1f,Member=member,Desk=true});}
            layers.Sort(delegate(HallLayer a,HallLayer b){return a.Depth.CompareTo(b.Depth);});
            foreach(var layer in layers)
            {
                var member=layer.Member;float scale=scales[member.Name];PointF home=homes[member.Name];
                if(!layer.Desk){DrawActor(graphics,member,actors[member.Name],scale,now);continue;}
                GraphicsState state=graphics.Save();graphics.TranslateTransform(home.X-110*scale,home.Y-229*scale);graphics.ScaleTransform(scale,scale);
                DrawDesk(graphics,110,229,174,member.Status=="running");
                if(member.Status=="running"&&actors[member.Name].AtDesk&&actors[member.Name].Reading)DrawReport(graphics,110,167,now);
                graphics.Restore(state);
            }
            foreach(var member in members)
            {
                PointF home=homes[member.Name];float scale=scales[member.Name];
                Rectangle box = seats[member.Name];
                var title = new Rectangle(box.Left + 4, (int)(home.Y - 208 * scale), box.Width - 8, Font.Height + 7);
                using (var fill = new SolidBrush(Color.FromArgb(215, 16, 38, 40))) graphics.FillRectangle(fill, title);
                TextRenderer.DrawText(graphics, UiLanguage.Text(member.Name), Font, title, Color.FromArgb(251, 224, 166), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }
        private sealed class HallLayer {internal float Depth;internal SectMemberStatus Member;internal bool Desk;}
        private void DrawActor(Graphics graphics, SectMemberStatus member, HallActorPose pose, float scale, double now)
        {
            string state = pose.Moving ? (pose.FaceLeft ? "running-left" : "running-right") : pose.Reading ? "review" : member.Status == "success" ? "waving" : member.Status == "failed" || member.Status == "needs-user" ? "failed" : "idle";
            int phase = pose.Moving ? (int)(pose.Distance / Math.Max(1, 30 * scale) * 24) : (int)(now * (pose.Reading ? 24 : 12));
            Image sprite = Sprite(member.CharacterId, state, phase);
            int size = Math.Max(1, (int)(180 * scale));
            Rectangle rectangle = sprite == null ? new Rectangle((int)pose.Feet.X - size / 2, (int)pose.Feet.Y - size, size, size) : PetForm.FitSpriteRectangle(sprite.Size, new Size(size, size));
            if (sprite != null) rectangle.Offset((int)pose.Feet.X - size / 2, (int)pose.Feet.Y - size);
            using (var shadow = new SolidBrush(Color.FromArgb(80, 7, 24, 26))) graphics.FillEllipse(shadow, pose.Feet.X - 48 * scale, pose.Feet.Y - 8 * scale, 96 * scale, 12 * scale);
            if (sprite != null)
            {
                GraphicsState saved = graphics.Save(); graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; graphics.PixelOffsetMode = PixelOffsetMode.HighQuality; graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.DrawImage(sprite, rectangle); graphics.Restore(saved);
            }
            actorHitBoxes[member.Name] = rectangle;
            if (pose.Reading && !pose.AtDesk)
            {
                GraphicsState saved = graphics.Save(); graphics.TranslateTransform(pose.Feet.X, pose.Feet.Y - 62 * scale); graphics.ScaleTransform(scale, scale); DrawReport(graphics, 0, 0, now); graphics.Restore(saved);
            }
            if (!pose.AtDesk)
            {
                string text = UiLanguage.Text(member.Name) + " · " + (pose.Handoff ? UiLanguage.Text("交接中") : pose.Moving ? UiLanguage.Text("工位巡视") : UiLanguage.Text("翻阅报告"));
                var label = new Rectangle((int)pose.Feet.X - 88, (int)pose.Feet.Y + 2, 176, Font.Height + 6);
                using (var fill = new SolidBrush(Color.FromArgb(230, 16, 38, 40))) graphics.FillRectangle(fill, label);
                TextRenderer.DrawText(graphics, text, Font, label, Color.LightGoldenrodYellow, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            else
            {
                var label = new Rectangle((int)(pose.Feet.X - 80 * scale), (int)(pose.Feet.Y + 7 * scale), Math.Max(1, (int)(160 * scale)), Font.Height + 6);
                using (var fill = new SolidBrush(Color.FromArgb(215, 16, 38, 40))) graphics.FillRectangle(fill, label);
                TextRenderer.DrawText(graphics, SectMemberStatus.Label(member.Status), Font, label, member.Status == "running" ? Color.FromArgb(102, 214, 194) : Color.Gainsboro, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
        private static void DrawReport(Graphics graphics, float x, float y, double now)
        {
            using (var paper = new SolidBrush(Color.FromArgb(249, 234, 198))) graphics.FillRectangle(paper, x - 27, y - 15, 54, 25);
            using (var ink = new Pen(Color.FromArgb(151, 127, 91), 1))
            {
                graphics.DrawLine(ink, x, y - 13, x, y + 8);
                for (int i = 0; i < 3; i++) { graphics.DrawLine(ink, x - 22, y - 9 + i * 6, x - 5, y - 9 + i * 6); graphics.DrawLine(ink, x + 5, y - 9 + i * 6, x + 22, y - 9 + i * 6); }
            }
            float turn = (float)((now * .65) % 1), edge = x + (float)Math.Cos(turn * Math.PI) * 24;
            using (var page = new SolidBrush(Color.FromArgb(255, 247, 221))) graphics.FillPolygon(page, new[] { new PointF(x, y - 15), new PointF(edge, y - 18 - (float)Math.Sin(turn * Math.PI) * 6), new PointF(edge, y + 6), new PointF(x, y + 10) });
        }
    }
}

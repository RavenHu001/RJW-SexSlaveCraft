// AI 辅助生成。只从当前已加载头图生成遮罩，不分发或改写原版贴图。
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>原版眼睛与头皮共用贴图：一次识别内部眼睛墨迹，后续按贴图弱引用复用。</summary>
    internal static class FullGelVanillaEyeMask
    {
        private sealed class Entry { public Texture mask; public int eyes; }
        private static readonly ConditionalWeakTable<Texture, Entry> masks = new ConditionalWeakTable<Texture, Entry>();

        internal static bool TryGet(Texture texture, int eyes, out Texture mask)
        {
            mask = null;
            if (texture == null) return false;
            if (masks.TryGetValue(texture, out Entry cached))
            {
                mask = cached.eyes == eyes ? cached.mask : null;
                return mask != null;
            }
            // 材质回调也可能来自工作线程；只允许主线程首次读像素和创建纹理。
            // 未预热时保留原头图，不能回退到会染眼睛的普通凝胶Shader。
            if (!UnityData.IsInMainThread || !(texture is Texture2D source)) return false;
            var entry = new Entry { eyes = eyes };
            try
            {
                if (TryBuild(source.GetPixels32(), source.width, source.height, eyes, out Color32[] pixels))
                {
                    var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                    result.name = "SSC_GelEyeMask";
                    result.wrapMode = TextureWrapMode.Clamp;
                    result.filterMode = FilterMode.Bilinear;
                    result.SetPixels32(pixels);
                    result.Apply(false, true);
                    entry.mask = result;
                }
            }
            catch (Exception ex)
            {
                SSCLog.WarningImportant("[SSC FullGel] Cannot read vanilla head eyes; preserving original head: " + ex.GetType().Name);
            }
            // 失败也缓存，避免不支持的贴图每帧扫描/抛异常。换成新贴图对象时会重新识别。
            masks.Add(texture, entry);
            mask = entry.mask;
            return mask != null;
        }

        /// <summary>限定头部内部、眼睛高度和成对关系；不把黑色轮廓当作眼睛，歧义时拒绝染色。</summary>
        internal static bool TryBuild(Color32[] pixels, int width, int height, int eyes, out Color32[] mask)
        {
            mask = null;
            if (width < 8 || height < 8 || width > 2048 || height > 2048 || pixels == null
                || pixels.Length != width * height || (eyes != 1 && eyes != 2)) return false;
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].a >= 128)
                {
                    int x = i % width, y = i / width;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
            int fw = maxX - minX + 1, fh = maxY - minY + 1;
            if (fw < 8 || fh < 8) return false;
            // 部分原版侧脸的眼角与鼻侧黑轮廓相连。先去掉贴近透明外缘的轮廓带，
            // 再做连通域识别；不能仅凭“是否连接外框”把这几种原版眼睛漏掉。
            int inset = Math.Max(1, Math.Min(fw, fh) / 10);
            // 两遍八邻域距离变换为O(像素数)，避免高分辨率头图按每个墨迹像素扫描大片邻域。
            var distance = new int[pixels.Length];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (pixels[i].a < 128) continue;
                int d = Math.Min(Math.Min(x + 1, width - x), Math.Min(y + 1, height - y));
                if (x > 0) d = Math.Min(d, distance[i - 1] + 1);
                if (y > 0)
                {
                    d = Math.Min(d, distance[i - width] + 1);
                    if (x > 0) d = Math.Min(d, distance[i - width - 1] + 1);
                    if (x + 1 < width) d = Math.Min(d, distance[i - width + 1] + 1);
                }
                distance[i] = d;
            }
            for (int y = height - 1; y >= 0; y--) for (int x = width - 1; x >= 0; x--)
            {
                int i = y * width + x; if (distance[i] == 0) continue;
                int d = distance[i];
                if (x + 1 < width) d = Math.Min(d, distance[i + 1] + 1);
                if (y + 1 < height)
                {
                    d = Math.Min(d, distance[i + width] + 1);
                    if (x > 0) d = Math.Min(d, distance[i + width - 1] + 1);
                    if (x + 1 < width) d = Math.Min(d, distance[i + width + 1] + 1);
                }
                distance[i] = d;
            }
            var ink = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                ink[i] = Ink(pixels[i]) && distance[i] > inset;
            var seen = new bool[pixels.Length];
            var candidates = new List<List<int>>();
            var centers = new List<float>();
            var queue = new Queue<int>();
            for (int start = 0; start < pixels.Length; start++)
            {
                if (seen[start] || !ink[start]) continue;
                var part = new List<int>(); bool boundary = false;
                int lx = width, ly = height, hx = -1, hy = -1;
                seen[start] = true; queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue(), x = i % width, y = i / width;
                    part.Add(i); lx = Math.Min(lx, x); hx = Math.Max(hx, x); ly = Math.Min(ly, y); hy = Math.Max(hy, y);
                    // 八连通合并压缩纹理中的斜向墨迹；任意透明邻点代表外轮廓，整块排除。
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= width || yy >= height) { boundary = true; continue; }
                        int n = yy * width + xx;
                        if (pixels[n].a < 128) boundary = true;
                        if (!seen[n] && ink[n]) { seen[n] = true; queue.Enqueue(n); }
                    }
                }
                float cx = ((lx + hx) * .5f - minX) / fw, cy = ((ly + hy) * .5f - minY) / fh;
                if (!boundary && part.Count >= Math.Max(3, fw * fh * .004f) && part.Count <= fw * fh * .08f
                    && hx - lx >= fw * .06f && hy - ly >= fh * .04f
                    && hx - lx < fw * .36f && hy - ly < fh * .30f
                    && cx > .12f && cx < .88f && cy > .22f && cy < .60f)
                { candidates.Add(part); centers.Add(cx); }
            }
            // 正面必须一左一右；侧面必须只有一只。未知纹理/闭眼改图不猜测位置。
            if (candidates.Count != eyes || (eyes == 2 && !((centers[0] < .5f) != (centers[1] < .5f)))) return false;
            mask = new Color32[pixels.Length];
            int radius = Math.Max(1, width / 128);
            foreach (var part in candidates)
                foreach (int i in part)
                {
                    int x = i % width, y = i / width;
                    for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= width || yy >= height) continue;
                        int n = yy * width + xx; Color32 p = pixels[n];
                        // 包含眼睛的灰色抗锯齿像素，不扩大到纯白皮肤。孔洞/背景仍由原Alpha控制。
                        if (p.a >= 128 && Math.Max(p.r, Math.Max(p.g, p.b)) < 245)
                            mask[n] = new Color32(255, 255, 255, 255);
                    }
                }
            // 眼睛墨迹包围的纯白高光同样属于眼睛；仅填封闭小孔，不把眼周白皮肤纳入保护。
            Color32[] maskData = mask;
            Array.Clear(seen, 0, seen.Length);
            for (int i = 0; i < pixels.Length; i++)
                if (mask[i].r == 0 && (i < width || i >= pixels.Length - width || i % width == 0 || i % width == width - 1))
                { seen[i] = true; queue.Enqueue(i); }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % width, y = i / width;
                if (x > 0) Visit(i - 1); if (x + 1 < width) Visit(i + 1);
                if (y > 0) Visit(i - width); if (y + 1 < height) Visit(i + width);
            }
            for (int i = 0; i < mask.Length; i++)
                if (!seen[i] && pixels[i].a >= 128) mask[i] = new Color32(255, 255, 255, 255);
            return true;

            void Visit(int i)
            {
                if (!seen[i] && maskData[i].r == 0) { seen[i] = true; queue.Enqueue(i); }
            }
        }

        private static bool Ink(Color32 p) => p.a >= 128 && Math.Max(p.r, Math.Max(p.g, p.b)) <= 110;
    }
}

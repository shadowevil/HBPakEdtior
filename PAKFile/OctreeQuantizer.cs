using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PAKFile
{
    public class OctreeQuantizer
    {
        public class OctreeNode
        {
            public bool IsLeaf;
            public int PixelCount;
            public int Red;
            public int Green;
            public int Blue;
            public int PaletteIndex;
            public OctreeNode[] Children = new OctreeNode[8];
            public OctreeNode Next;

            public void AddColor(Color color, int level, OctreeQuantizer quantizer)
            {
                if (IsLeaf)
                {
                    PixelCount++;
                    Red += color.R;
                    Green += color.G;
                    Blue += color.B;
                }
                else
                {
                    int index = GetColorIndexAtLevel(color, level);
                    if (Children[index] == null)
                    {
                        Children[index] = new OctreeNode();
                        if (level < 7)
                            quantizer.AddLevelNode(level + 1, Children[index]);
                        else
                        {
                            Children[index].IsLeaf = true;
                            quantizer.leafCount++;
                        }
                    }
                    Children[index].AddColor(color, level + 1, quantizer);
                }
            }

            public void GetPalette(List<Color> palette)
            {
                if (IsLeaf)
                {
                    int r = Red / PixelCount;
                    int g = Green / PixelCount;
                    int b = Blue / PixelCount;
                    PaletteIndex = palette.Count;
                    palette.Add(Color.FromArgb(r, g, b));
                }
                else
                {
                    foreach (var child in Children)
                        child?.GetPalette(palette);
                }
            }

            public int GetPaletteIndex(Color color, int level)
            {
                if (IsLeaf)
                    return PaletteIndex;

                int index = GetColorIndexAtLevel(color, level);
                if (Children[index] != null)
                    return Children[index].GetPaletteIndex(color, level + 1);

                // Fallback if node doesn't exist
                for (int i = 0; i < 8; i++)
                    if (Children[i] != null)
                        return Children[i].GetPaletteIndex(color, level + 1);

                return 0;
            }

            private int GetColorIndexAtLevel(Color color, int level)
            {
                int shift = 7 - level;
                int r = (color.R >> shift) & 1;
                int g = (color.G >> shift) & 1;
                int b = (color.B >> shift) & 1;
                return (r << 2) | (g << 1) | b;
            }
        }

        private OctreeNode root = new OctreeNode();
        private List<OctreeNode>[] levels = new List<OctreeNode>[8];
        internal int leafCount = 0;
        private int maxColors;

        public OctreeQuantizer(int maxColors)
        {
            this.maxColors = maxColors;
            for (int i = 0; i < levels.Length; i++)
                levels[i] = new List<OctreeNode>();
        }

        internal void AddLevelNode(int level, OctreeNode node)
        {
            levels[level].Add(node);
        }

        public Bitmap Quantize(Bitmap source)
        {
            // Build tree
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color c = source.GetPixel(x, y);
                    root.AddColor(c, 0, this);

                    while (leafCount > maxColors)
                        Reduce();
                }
            }

            // Generate palette
            List<Color> palette = new();
            root.GetPalette(palette);

            // Create 8bpp image
            Bitmap result = new Bitmap(source.Width, source.Height, PixelFormat.Format8bppIndexed);
            ColorPalette pal = result.Palette;
            for (int i = 0; i < palette.Count; i++)
                pal.Entries[i] = palette[i];
            result.Palette = pal;

            // Map pixels
            BitmapData data = result.LockBits(new Rectangle(0, 0, result.Width, result.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            int stride = data.Stride;
            byte[] row = new byte[stride];

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    Color c = source.GetPixel(x, y);
                    int index = root.GetPaletteIndex(c, 0);
                    row[x] = (byte)index;
                }
                Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * stride), stride);
            }

            result.UnlockBits(data);
            return result;
        }

        private void Reduce()
        {
            for (int level = levels.Length - 1; level >= 0; level--)
            {
                if (levels[level].Count > 0)
                {
                    var node = levels[level][0];
                    levels[level].RemoveAt(0);

                    int r = 0, g = 0, b = 0, count = 0;
                    foreach (var child in node.Children)
                    {
                        if (child != null)
                        {
                            r += child.Red;
                            g += child.Green;
                            b += child.Blue;
                            count += child.PixelCount;
                            leafCount--;
                        }
                    }

                    node.IsLeaf = true;
                    node.Red = r;
                    node.Green = g;
                    node.Blue = b;
                    node.PixelCount = count;
                    leafCount++;

                    return;
                }
            }
        }

        public int GetPaletteIndex(Color color)
        {
            return root.GetPaletteIndex(color, 0);
        }
    }
}

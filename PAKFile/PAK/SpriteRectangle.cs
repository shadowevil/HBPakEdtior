using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PAKFile
{
    public class SpriteRectangle
    {
        public short x { get; set; } = 0;
        public short y { get; set; } = 0;
        public short width { get; set; } = 0;
        public short height { get; set; } = 0;
        public short pivotX { get; set; } = 0;
        public short pivotY { get; set; } = 0;

        public Rectangle ToRectangle(int offsetX, int offsetY, float scale)
        {
            return new Rectangle(
                offsetX + (int)(x * scale),
                offsetY + (int)(y * scale),
                (int)(width * scale),
                (int)(height * scale)
            );
        }

        public RectangleF ToRectangleF(float offsetX, float offsetY, float scale)
        {
            return new RectangleF(
                offsetX + (x * scale),
                offsetY + (y * scale),
                width * scale,
                height * scale
            );
        }
    }
}

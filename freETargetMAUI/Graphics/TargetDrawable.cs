using System;
using System.Collections.Generic;
using Microsoft.Maui.Graphics;
using freETarget;
using freETarget.targets;

namespace freETargetMAUI.Graphics
{
    public class TargetDrawable : IDrawable
    {
        public Session CurrentSession { get; set; }
        public List<Shot> Shots { get; set; } = new List<Shot>();
        public aTarget Target { get; set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (Target != null && CurrentSession != null && Shots != null)
            {
                // Use the smallest dimension to ensure the target fits inside and stays a perfect circle
                int dimension = (int)Math.Min(dirtyRect.Width, dirtyRect.Height);
                
                if (dimension <= 0) return;

                // Center the drawing in the area if there is extra space
                if (dirtyRect.Width > dirtyRect.Height)
                {
                    canvas.Translate((dirtyRect.Width - dirtyRect.Height) / 2f, 0);
                }
                else if (dirtyRect.Height > dirtyRect.Width)
                {
                    canvas.Translate(0, (dirtyRect.Height - dirtyRect.Width) / 2f);
                }

                // Call our refactored method
                Target.DrawTarget(canvas, dirtyRect, dimension, Target.getTrkZoomValue(), false, CurrentSession, Shots);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;

namespace GitUI.UserControls.RevisionGrid.Graph
{
    public static class RevisionGraphLaneColor
    {
        internal static readonly IReadOnlyList<Func<Color>> PresetGraphColors = new Func<Color>[]
        {
            () => Color.FromArgb(240, 36, 117),
            () => Color.FromArgb(120, 180, 255), // light blue
            () => Color.FromArgb(46, 204, 113),
            () => Color.FromArgb(142, 68, 173),
            () => Color.FromArgb(231, 76, 60),
            () => RevisionGridRefRenderer.Lerp(SystemColors.ControlText, SystemColors.GrayText, 0.5f),
            () => Color.FromArgb(26, 188, 156),
            () => Color.FromArgb(241, 196, 15)
        };

        public static Color NonRelativeColor => RevisionGridRefRenderer.Lerp(SystemColors.GrayText, SystemColors.Window, 0.5f);

        internal static Brush NonRelativeBrush => new SolidBrush(NonRelativeColor);

        public static Brush GetBrushForLane(int laneColor)
        {
            return new SolidBrush(PresetGraphColors[Math.Abs(laneColor) % PresetGraphColors.Count]());
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows.Components.Branding
{
    /// <summary>
    /// The Novahn brand mark: a white "N" on a rounded indigo tile.
    ///
    /// The tile (indigo fill + rounded corners) is drawn by the element's own inline style so
    /// it is crisp on every Unity version. On Unity 2022.1+ the "N" strokes are drawn as
    /// resolution-independent vector geometry via UI Toolkit's Painter2D; Painter2D does not
    /// exist on the 2021.3/2020.3 floor, so there we fall back to a centred bold "N" label.
    ///
    /// Size the element via USS (width/height); the drawing scales to the resolved content box.
    /// </summary>
    public sealed class NovahnMark : VisualElement
    {
        // Indigo tile and white glyph — the Novahn accent palette.
        private static readonly Color Tile = FromHex(0x7C5CFC);
        private static readonly Color Glyph = FromHex(0xFFFFFF);

        public NovahnMark()
        {
            // The rounded indigo tile, applied inline so it never depends on an external stylesheet.
            style.backgroundColor = Tile;
            style.borderTopLeftRadius = 7;
            style.borderTopRightRadius = 7;
            style.borderBottomLeftRadius = 7;
            style.borderBottomRightRadius = 7;

#if UNITY_2022_1_OR_NEWER
            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
#else
            ApplyLabelFallback();
#endif
            pickingMode = PickingMode.Ignore; // purely decorative
        }

#if UNITY_2022_1_OR_NEWER
        // The glyph is authored in a 100-unit square.
        private const float BoxSize = 100f;

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            Rect rect = contentRect;
            float box = Mathf.Min(rect.width, rect.height);
            if (box <= 1f) return; // not laid out yet / too small to draw

            float scale = box / BoxSize;
            Vector2 P(float x, float y) => MapPoint(x, y, rect);

            Painter2D p = mgc.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            // "N": left bar up, diagonal down, right bar up — one continuous stroke.
            p.strokeColor = Glyph;
            p.lineWidth = 14f * scale;
            p.BeginPath();
            p.MoveTo(P(30, 74));
            p.LineTo(P(30, 26));
            p.LineTo(P(70, 74));
            p.LineTo(P(70, 26));
            p.Stroke();
        }

        /// <summary>Map a point from the 100-unit glyph space into the element's content rect (square, centred).</summary>
        private static Vector2 MapPoint(float gx, float gy, Rect content)
        {
            float box = Mathf.Min(content.width, content.height);
            float scale = box / BoxSize;
            float offsetX = (content.width - box) * 0.5f;
            float offsetY = (content.height - box) * 0.5f;
            return new Vector2(offsetX + gx * scale, offsetY + gy * scale);
        }
#else
        private void ApplyLabelFallback()
        {
            Label glyph = new Label("N")
            {
                pickingMode = PickingMode.Ignore,
            };
            glyph.style.flexGrow = 1;
            glyph.style.color = Glyph;
            glyph.style.unityFontStyleAndWeight = FontStyle.Bold;
            glyph.style.fontSize = 18;
            glyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            Add(glyph);
        }
#endif

        /// <summary>Convert a 0xRRGGBB literal into an opaque Color.</summary>
        private static Color FromHex(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f);
    }
}

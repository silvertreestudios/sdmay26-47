using UnityEngine;
using UnityEngine.UIElements;

namespace TacticsGame.UI
{
    [UxmlElement]
    public partial class RadialProgress : VisualElement
    {
        private float m_Progress;

        [UxmlAttribute("progress")]
        public float progress
        {
            get => m_Progress;
            set
            {
                m_Progress = Mathf.Clamp01(value);
                MarkDirtyRepaint();
            }
        }

        private Color m_ProgressColor = new Color(0.34f, 0.88f, 0.66f); // --color-accent
        public Color progressColor
        {
            get => m_ProgressColor;
            set
            {
                m_ProgressColor = value;
                MarkDirtyRepaint();
            }
        }

        public RadialProgress()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            float width = contentRect.width;
            float height = contentRect.height;
            if (width == 0 || height == 0)
                return;

            float radius = Mathf.Min(width, height) / 2f - 4f;
            Vector2 center = new Vector2(width / 2f, height / 2f);

            float angle = 360f * progress;

            var painter = ctx.painter2D;
            if (painter != null)
            {
                painter.lineWidth = 6f;
                painter.lineCap = LineCap.Round;
                painter.strokeColor = new Color(1f, 1f, 1f, 0.1f);

                // Background circle
                painter.BeginPath();
                painter.Arc(center, radius, 0, 360, ArcDirection.Clockwise);
                painter.Stroke();

                // Progress circle
                if (progress > 0)
                {
                    painter.strokeColor = progressColor;
                    painter.BeginPath();
                    painter.Arc(center, radius, -90, -90 + angle, ArcDirection.Clockwise);
                    painter.Stroke();
                }
            }
        }
    }
}

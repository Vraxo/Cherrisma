using Vortice.Mathematics;

namespace Cherris;

public partial class LineEdit
{
    private abstract class BaseText : VisualItem
    {
        protected LineEdit parentLineEdit;
        private Vector2 _textOffset = Vector2.Zero; // Local offset within LineEdit

        public BaseText(LineEdit parent)
        {
            parentLineEdit = parent;
            // Ensure these components are not processed by SceneTree independently for drawing
            // unless explicitly added as children and made visible.
            // LineEdit will call their Draw methods.
            Visible = false;
        }

        public Vector2 TextOffset
        {
            get => _textOffset;
            set => _textOffset = value;
        }

        public override void Draw(DrawingContext context)
        {
            if (!parentLineEdit.Visible || ShouldSkipDrawing() || string.IsNullOrEmpty(GetTextToDisplay()))
            {
                return;
            }

            // BaseText components are drawn relative to the parent LineEdit's content area.
            // GlobalPosition of parentLineEdit is its top-left.
            // Text is drawn within the LineEdit's bounds, considering TextOrigin.

            Rect layoutRect = GetLayoutRect();

            parentLineEdit.DrawFormattedText(
                context,
                GetTextToDisplay(),
                layoutRect,
                parentLineEdit.Styles.Current, // Text color and font from ButtonStyle
                HAlignment.Left,    // Text within LineEdit is typically left-aligned
                VAlignment.Center); // And vertically centered
        }

        protected Rect GetLayoutRect()
        {
            // TextOrigin defines the padding from the LineEdit's edges.
            // Position of LineEdit is parentLineEdit.GlobalPosition - parentLineEdit.Origin
            // For simplicity, assume Origin is Zero for LineEdit or handled by GlobalPosition.
            // GlobalPosition for Node2D is its top-left.

            Vector2 lineEditPos = parentLineEdit.GlobalPosition; // Top-left of the LineEdit
            Vector2 lineEditSize = parentLineEdit.Size;

            // Text area starts after TextOrigin.X from left, and TextOrigin.Y from top (if used).
            // Typically TextOrigin.Y might be for vertical centering alignment, handled by VAlignment.Center.
            float textRenderAreaX = lineEditPos.X + parentLineEdit.TextOrigin.X + TextOffset.X;
            float textRenderAreaY = lineEditPos.Y + TextOffset.Y; // Assuming TextOrigin.Y is for padding from top or VAlignment handles it
            float textRenderAreaWidth = lineEditSize.X - parentLineEdit.TextOrigin.X * 2; // Padding on both sides
            float textRenderAreaHeight = lineEditSize.Y;

            return new Rect(textRenderAreaX, textRenderAreaY, Math.Max(0, textRenderAreaWidth), Math.Max(0, textRenderAreaHeight));
        }

        protected abstract string GetTextToDisplay();
        protected abstract bool ShouldSkipDrawing();
    }
}
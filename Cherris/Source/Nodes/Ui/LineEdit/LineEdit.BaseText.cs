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
            // This is the visual top-left of the LineEdit box itself
            Vector2 lineEditVisualTopLeft = parentLineEdit.GlobalPosition - parentLineEdit.Origin;
            Vector2 lineEditSize = parentLineEdit.Size;

            // Text area starts after TextOrigin.X from the *visual left* of LineEdit,
            // and TextOrigin.Y from the *visual top* of LineEdit.
            float textRenderAreaX = lineEditVisualTopLeft.X + parentLineEdit.TextOrigin.X + TextOffset.X;
            // Corrected Y to be relative to the visual top of the LineEdit
            float textRenderAreaY = lineEditVisualTopLeft.Y + parentLineEdit.TextOrigin.Y + TextOffset.Y;

            // Width available for text
            float textRenderAreaWidth = lineEditSize.X - parentLineEdit.TextOrigin.X * 2; // Horizontal padding on both sides
            // Height available for text (full height of LineEdit minus vertical padding)
            // Since TextOrigin.Y is 0 for LineEdit, this is currently lineEditSize.Y
            float textRenderAreaHeight = lineEditSize.Y - parentLineEdit.TextOrigin.Y * 2;

            return new Rect(textRenderAreaX, textRenderAreaY, Math.Max(0, textRenderAreaWidth), Math.Max(0, textRenderAreaHeight));
        }

        protected abstract string GetTextToDisplay();
        protected abstract bool ShouldSkipDrawing();
    }
}
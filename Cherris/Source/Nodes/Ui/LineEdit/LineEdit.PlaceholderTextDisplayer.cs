namespace Cherris;

public partial class LineEdit
{
    private class PlaceholderTextDisplayer : BaseText
    {
        public PlaceholderTextDisplayer(LineEdit parent) : base(parent)
        {
        }

        protected override string GetTextToDisplay()
        {
            return parentLineEdit.PlaceholderText;
        }

        protected override bool ShouldSkipDrawing()
        {
            // Don't draw placeholder if there's actual text
            return !string.IsNullOrEmpty(parentLineEdit.Text);
        }
    }
}
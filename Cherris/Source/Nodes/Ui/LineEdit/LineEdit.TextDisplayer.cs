namespace Cherris;

public partial class LineEdit
{
    private class TextDisplayer : BaseText
    {
        public TextDisplayer(LineEdit parent) : base(parent)
        {
        }

        protected override string GetTextToDisplay()
        {
            if (string.IsNullOrEmpty(parentLineEdit.Text))
            {
                return "";
            }

            string textToDisplay = parentLineEdit.Text;

            if (parentLineEdit.Secret)
            {
                textToDisplay = new string(parentLineEdit.SecretCharacter, textToDisplay.Length);
            }

            // Ensure TextStartIndex is valid
            int startIndex = Math.Clamp(parentLineEdit.TextStartIndex, 0, textToDisplay.Length);

            // Calculate how many characters can actually be taken from startIndex
            int availableLengthFromStartIndex = textToDisplay.Length - startIndex;

            // Determine the number of characters to display (min of displayable count and available from start index)
            int count = Math.Min(parentLineEdit.GetDisplayableCharactersCount(), availableLengthFromStartIndex);

            if (count <= 0) return ""; // Nothing to display from this start index or no space

            return textToDisplay.Substring(startIndex, count);
        }

        protected override bool ShouldSkipDrawing()
        {
            // If placeholder is showing, actual text displayer should skip.
            // Placeholder skips if text has length. Actual text should draw if it has length.
            // This seems redundant with GetTextToDisplay returning "" if no text.
            return string.IsNullOrEmpty(parentLineEdit.Text);
        }
    }
}
using System.Globalization; // For CultureInfo if needed for measurements
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace Cherris;

public partial class LineEdit
{
    protected class Caret : VisualItem
    {
        public float MaxTime { get; set; } = 0.5f;
        private const byte MinAlphaByte = 0;
        private const byte MaxAlphaByte = 255;
        private float _timer = 0;
        private float _alpha = 1.0f; // Use float for Color4 alpha
        private LineEdit _parentLineEdit;

        private float _arrowKeyTimer = 0f;
        private const float ArrowKeyDelay = 1.0f;
        private const float ArrowKeySpeed = 0.1f; // Changed from 0.05f to 0.1f
        private bool _movingRight = false; // To track continuous movement direction

        private int _caretDisplayPositionX; // Position relative to the start of visible text
        public int CaretDisplayPositionX
        {
            get => _caretDisplayPositionX;
            set
            {
                var maxVisibleChars = Math.Max(0, Math.Min(_parentLineEdit.GetDisplayableCharactersCount(), _parentLineEdit.Text.Length - _parentLineEdit.TextStartIndex));
                _caretDisplayPositionX = Math.Clamp(value, 0, maxVisibleChars);
                _alpha = 1.0f; // Reset blink
                _timer = 0f;   // Reset blink timer
            }
        }

        public Caret(LineEdit parent)
        {
            _parentLineEdit = parent;
            Visible = false; // Drawn by LineEdit explicitly
        }

        public void UpdateLogic() // Renamed from Update to avoid confusion with Node.Process
        {
            if (!_parentLineEdit.Selected || !_parentLineEdit.Editable) return;

            HandleKeyboardInput();
            HandleMouseInput();
            UpdateAlpha();
        }

        public override void Draw(DrawingContext context)
        {
            if (!_parentLineEdit.Selected || !_parentLineEdit.Editable || !Visible || _alpha <= 0.01f)
            {
                return;
            }

            Rect layoutRect = GetCaretLayoutRect(context);
            if (layoutRect.Width <= 0 || layoutRect.Height <= 0) return;

            ButtonStyle caretStyle = new ButtonStyle
            {
                FontName = _parentLineEdit.Styles.Current.FontName,
                FontSize = _parentLineEdit.Styles.Current.FontSize,
                FontWeight = _parentLineEdit.Styles.Current.FontWeight,
                FontStyle = _parentLineEdit.Styles.Current.FontStyle,
                FontStretch = _parentLineEdit.Styles.Current.FontStretch,
                FontColor = new Color4(_parentLineEdit.Styles.Current.FontColor.R, _parentLineEdit.Styles.Current.FontColor.G, _parentLineEdit.Styles.Current.FontColor.B, _alpha),
                WordWrapping = WordWrapping.NoWrap
            };

            _parentLineEdit.DrawFormattedText(
                context,
                "|",
                layoutRect,
                caretStyle,
                HAlignment.Left,
                VAlignment.Center);
        }

        private void HandleKeyboardInput()
        {
            bool rightPressed = Input.IsKeyPressed(KeyCode.RightArrow);
            bool leftPressed = Input.IsKeyPressed(KeyCode.LeftArrow);

            if (rightPressed || leftPressed)
            {
                _movingRight = rightPressed;
                _arrowKeyTimer = 0f;
                MoveCaret(_movingRight ? 1 : -1);
            }
            else if (Input.IsKeyDown(KeyCode.RightArrow) || Input.IsKeyDown(KeyCode.LeftArrow))
            {
                // Update direction if key state changes during hold
                if (Input.IsKeyDown(KeyCode.RightArrow)) _movingRight = true;
                else if (Input.IsKeyDown(KeyCode.LeftArrow)) _movingRight = false;

                _arrowKeyTimer += Time.Delta;
                if (_arrowKeyTimer >= ArrowKeyDelay)
                {
                    // Check if it's time for a repeat based on ArrowKeySpeed
                    // This is a simplified way to handle repeat interval
                    if ((_arrowKeyTimer - ArrowKeyDelay) % ArrowKeySpeed < Time.Delta) // Ensures one move per interval window
                    {
                        MoveCaret(_movingRight ? 1 : -1);
                    }
                }
            }
            else // No arrow keys down
            {
                _arrowKeyTimer = 0f;
            }
        }

        private void HandleMouseInput()
        {
            if (Input.IsMouseButtonPressed(MouseButtonCode.Left))
            {
                Vector2 localMousePos = _parentLineEdit.GetLocalMousePosition(); // Mouse pos relative to LineEdit's window

                // Check if click is within LineEdit's bounds
                // Using visual top-left for hit testing
                Vector2 lineEditVisualTopLeft = _parentLineEdit.GlobalPosition - _parentLineEdit.Origin;
                Rect lineEditBounds = new Rect(
                    lineEditVisualTopLeft.X, lineEditVisualTopLeft.Y,
                    _parentLineEdit.Size.X, _parentLineEdit.Size.Y);

                if (lineEditBounds.Contains(localMousePos.X, localMousePos.Y))
                {
                    MoveCaretToMousePosition(localMousePos);
                }
            }
        }


        private void MoveCaret(int direction)
        {
            // This moves the logical caret position (_parentLineEdit.CaretLogicalPosition)
            // And then updates display position and start index.

            int newLogicalPos = _parentLineEdit.CaretLogicalPosition + direction;
            _parentLineEdit.CaretLogicalPosition = Math.Clamp(newLogicalPos, 0, _parentLineEdit.Text.Length);
            _parentLineEdit.UpdateCaretDisplayPositionAndStartIndex();
        }

        public void MoveCaretToMousePosition(Vector2 localMousePos) // localMousePos is relative to owning window
        {
            if (_parentLineEdit.Text.Length == 0)
            {
                _parentLineEdit.CaretLogicalPosition = 0;
                _parentLineEdit.UpdateCaretDisplayPositionAndStartIndex();
                return;
            }

            var owningWindow = _parentLineEdit.GetOwningWindow() as Direct2DAppWindow;
            if (owningWindow == null || owningWindow.DWriteFactory == null) return; // Cannot measure text
            IDWriteFactory dwriteFactory = owningWindow.DWriteFactory;

            // Calculate the visual top-left of the LineEdit's actual rendering box
            Vector2 lineEditVisualTopLeft = _parentLineEdit.GlobalPosition - _parentLineEdit.Origin;
            // Calculate the top-left of the text rendering area (inside padding/TextOrigin)
            Vector2 textRenderAreaVisualTopLeft = lineEditVisualTopLeft + _parentLineEdit.TextOrigin;

            // Mouse X relative to the start of the text rendering area within LineEdit
            float mouseXInTextRenderArea = localMousePos.X - textRenderAreaVisualTopLeft.X;


            string visibleText = _parentLineEdit.Text.Substring(
                _parentLineEdit.TextStartIndex,
                Math.Min(_parentLineEdit.GetDisplayableCharactersCount(), _parentLineEdit.Text.Length - _parentLineEdit.TextStartIndex)
            );

            if (string.IsNullOrEmpty(visibleText))
            {
                _parentLineEdit.CaretLogicalPosition = (mouseXInTextRenderArea < 0 && _parentLineEdit.TextStartIndex > 0) ? _parentLineEdit.TextStartIndex : _parentLineEdit.TextStartIndex;
                _parentLineEdit.UpdateCaretDisplayPositionAndStartIndex();
                return;
            }

            IDWriteTextFormat? textFormat = owningWindow.GetOrCreateTextFormat(_parentLineEdit.Styles.Current);
            if (textFormat == null) return;

            using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(
                visibleText,
                textFormat,
                _parentLineEdit.Size.X,
                _parentLineEdit.Size.Y
            );

            textLayout.WordWrapping = WordWrapping.NoWrap;

            textLayout.HitTestPoint(mouseXInTextRenderArea, 0, out var isTrailingHit, out var isInside, out var metrics);

            int newCaretIndexInVisibleText = (int)metrics.TextPosition;
            if (isTrailingHit) newCaretIndexInVisibleText = (int)metrics.TextPosition + (int)metrics.Length;


            _parentLineEdit.CaretLogicalPosition = _parentLineEdit.TextStartIndex + Math.Clamp(newCaretIndexInVisibleText, 0, visibleText.Length);
            _parentLineEdit.UpdateCaretDisplayPositionAndStartIndex();
        }


        private Rect GetCaretLayoutRect(DrawingContext context)
        {
            // Determine the visual top-left of the LineEdit's box
            Vector2 lineEditVisualTopLeft = _parentLineEdit.GlobalPosition - _parentLineEdit.Origin;
            // Determine the visual top-left of the text rendering area (inside TextOrigin padding)
            Vector2 textRenderAreaVisualTopLeft = lineEditVisualTopLeft + _parentLineEdit.TextOrigin;

            float caretXOffset = 0;

            if (CaretDisplayPositionX > 0 && _parentLineEdit.Text.Length > 0)
            {
                string textBeforeCaret = _parentLineEdit.Text.Substring(
                    _parentLineEdit.TextStartIndex,
                    Math.Min(CaretDisplayPositionX, _parentLineEdit.Text.Length - _parentLineEdit.TextStartIndex)
                );

                if (!string.IsNullOrEmpty(textBeforeCaret))
                {
                    var dwriteFactory = context.DWriteFactory;
                    var owningWindow = context.OwnerWindow;
                    IDWriteTextFormat? textFormat = owningWindow?.GetOrCreateTextFormat(_parentLineEdit.Styles.Current);

                    if (textFormat != null)
                    {
                        textFormat.WordWrapping = WordWrapping.NoWrap;
                        using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(
                            textBeforeCaret,
                            textFormat,
                            float.MaxValue,
                            _parentLineEdit.Size.Y);
                        caretXOffset = textLayout.Metrics.Width;
                    }
                }
            }

            float caretWidth = _parentLineEdit.MeasureSingleCharWidth(context, "|", _parentLineEdit.Styles.Current);
            if (caretWidth <= 0) caretWidth = 2;

            float caretRectX = textRenderAreaVisualTopLeft.X + caretXOffset - caretWidth / 2f;
            float caretRectY = textRenderAreaVisualTopLeft.Y;
            // Caret height should be consistent with the text layout height
            float caretRectHeight = _parentLineEdit.Size.Y - _parentLineEdit.TextOrigin.Y * 2;


            return new Rect(
                caretRectX,
                caretRectY,
                caretWidth,
                Math.Max(0, caretRectHeight)
            );
        }


        private void UpdateAlpha()
        {
            _timer += Time.Delta;
            if (_timer > MaxTime)
            {
                _alpha = (_alpha == 1.0f) ? 0.0f : 1.0f;
                _timer = 0;
            }
        }
    }
}
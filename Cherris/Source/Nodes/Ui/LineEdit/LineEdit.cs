using Cherris;
using System.Globalization;
using Vortice.DirectWrite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vortice.DirectWrite;
using SharpGen.Runtime; // Required for Result

namespace Cherris;

public partial class LineEdit : Button
{
    #region [ - - - Properties & Fields - - - ]

    public static readonly Vector2 DefaultLineEditSize = new(200, 28); // Adjusted default size

    private string _text = "";
    public new string Text // Hides Button.Text
    {
        get => _text;
        set
        {
            if (_text == value) return;

            string oldText = _text;
            _text = value ?? ""; // Ensure not null

            if (_text.Length > MaxCharacters)
            {
                _text = _text.Substring(0, MaxCharacters);
            }

            UpdateCaretDisplayPositionAndStartIndex(); // Update caret based on new text
            TextChanged?.Invoke(this, _text);
            if (oldText.Length == 0 && _text.Length > 0)
            {
                FirstCharacterEntered?.Invoke(this, EventArgs.Empty);
            }
            if (oldText.Length > 0 && _text.Length == 0)
            {
                Cleared?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string PlaceholderText { get; set; } = "";
    public Vector2 TextOrigin { get; set; } = new(6, 0); // Padding from left for text start
    public int MaxCharacters { get; set; } = int.MaxValue;
    // MinCharacters not directly enforced during typing, usually for validation on confirm
    public List<char> ValidCharacters { get; set; } = []; // If empty, all chars allowed

    public bool Selected
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            _caret.Visible = field && Editable; // Caret visible only if LineEdit is selected and editable
            if (!field) _caret.CaretDisplayPositionX = 0; // Reset caret display pos when deselected
        }
    } = false;

    public bool Editable { get; set; } = true;
    public bool ExpandWidthToText { get; set; } = false; // Requires robust DWrite measurement
    public bool Secret { get; set; } = false;
    public char SecretCharacter { get; set; } = '*';
    public bool AutoScrollToShowFullText { get; set; } = true; // New property to control auto-scroll

    // TextStartIndex: The index of the first character in `_text` that is visible.
    // This is used for horizontal scrolling.
    public int TextStartIndex { get; internal set; } = 0;

    // CaretLogicalPosition: The caret's position as an index within the full `_text` string.
    internal int CaretLogicalPosition { get; set; } = 0;


    public event EventHandler? FirstCharacterEntered;
    public event EventHandler? Cleared;
    public event EventHandler<string>? TextChanged;
    public event EventHandler<string>? Confirmed;

    protected Cherris.LineEdit.Caret _caret;
    private readonly Cherris.LineEdit.TextDisplayer _textDisplayer;
    private readonly Cherris.LineEdit.PlaceholderTextDisplayer _placeholderTextDisplayer;

    private const float BackspaceDelay = 0.5f;
    private const float BackspaceSpeed = 0.05f; // Time between repeats
    private const float UndoDelay = 0.5f;
    private const float UndoSpeed = 0.05f;

    private float _backspaceTimer = 0f;
    private bool _backspaceHeld = false;
    private float _undoTimer = 0f;
    private bool _undoHeld = false;
    private bool _backspaceCtrlHeld = false;

    private Stack<LineEditState> _undoStack = new();
    private Stack<LineEditState> _redoStack = new();
    private const int HistoryLimit = 50;

    private char? _pendingCharInput = null; // For character input

    #endregion

    public LineEdit()
    {
        // 1. Initialize core members that other parts of the constructor or property setters might depend on.
        _caret = new Cherris.LineEdit.Caret(this);
        _textDisplayer = new Cherris.LineEdit.TextDisplayer(this);
        _placeholderTextDisplayer = new Cherris.LineEdit.PlaceholderTextDisplayer(this);

        // 2. Set default values for properties. Order might matter if setters have side effects.
        Visible = true; // Explicitly set visible, though default for VisualItem is true.
        Size = DefaultLineEditSize; // Use specific default for LineEdit
        TextHAlignment = HAlignment.Left; // Text in button usually centered, LineEdit is left
        TextVAlignment = VAlignment.Center;

        // 3. Set the problematic test text. Now _caret will be initialized.
        Text = "Type here...";

        // 4. Configure styles.
        // LineEdit inherits Styles from Button (Control).
        // We can customize its default appearance here or expect it from YAML.
        Styles.Normal.BorderLength = 1;
        Styles.Focused.BorderLength = 1; // Keep border for focus
        Styles.Focused.BorderColor = DefaultTheme.FocusBorder; // Standard focus color

        // 5. Subscribe to events.
        // Subscribe to events from Control/Button
        FocusChanged += OnFocusChangedHandler;
        LeftClicked += OnLeftClickedHandler; // From Button
        ClickedOutside += OnClickedOutsideHandler; // From Control
        LayerChanged += OnLayerChangedHandler; // From VisualItem
        SizeChanged += OnSizeChangedHandler; // From Node2D
    }

    public override void Process()
    {
        base.Process(); // Handles Control/Button related processing (focus, hover, style updates)

        CaptureCharInput(); // Poll for character input

        if (Editable && Selected)
        {
            HandleCharacterInput();
            HandleBackspace();
            HandleDelete();
            HandleHomeEndKeys();
            HandleClipboardPaste();
            HandleUndoRedo();
            ConfirmOnEnter();
        }
        _caret.UpdateLogic(); // Update caret logic (input, blinking)
        // Text displayers don't need a process, their GetTextToDisplay is called in Draw
        UpdateSizeToFitTextIfEnabled();
    }

    public override void Draw(DrawingContext context)
    {
        base.Draw(context); // Draws the button background/border

        // Placeholder and TextDisplayers draw text *inside* the button area
        _placeholderTextDisplayer.Draw(context);
        _textDisplayer.Draw(context);

        if (Selected && Editable)
        {
            _caret.Visible = true; // Ensure caret is marked visible before its draw call
            _caret.Draw(context);
        }
        else
        {
            _caret.Visible = false;
        }
    }

    private void CaptureCharInput()
    {
        // This is a placeholder. Proper implementation requires WM_CHAR handling in MainAppWindow
        // and a way for Input class to provide these characters.
        // For now, we'll simulate it for a few keys if they are pressed.
        if (_pendingCharInput == null) // Process one char per frame for simplicity
        {
            _pendingCharInput = Input.ConsumeNextTypedChar(); // ASSUMED METHOD on Input class
        }
    }

    protected override void OnEnterPressed() // Called by Button base class if Enter is pressed while focused
    {
        if (Editable)
        {
            ConfirmAction();
        }
        // Don't call base.OnEnterPressed if we handle enter for confirmation,
        // as button's default might be to invoke LeftClicked.
    }

    private void OnFocusChangedHandler(Control control) // From Control
    {
        Selected = control.Focused;
    }

    private void OnLeftClickedHandler() // From Button
    {
        if (Editable)
        {
            Selected = true;
            // Caret position is handled by Caret's mouse input logic
        }
    }

    private void OnClickedOutsideHandler(Control control) // From Control
    {
        if (Selected)
        {
            Selected = false;
            // Optionally, confirm text here if needed: ConfirmAction();
        }
    }

    private void OnLayerChangedHandler(VisualItem sender, int layer)
    {
        // If sub-components need layer awareness, set their Layer property.
        // _caret.Layer = layer + 1; // Example, if caret draws independently.
        // For now, LineEdit controls their drawing order.
    }

    private void OnSizeChangedHandler(object? sender, Vector2 newSize)
    {
        // When size changes, re-evaluate visible text range and caret
        TextStartIndex = 0; // Could be smarter, but reset is safe
        UpdateCaretDisplayPositionAndStartIndex();
    }

    private void UpdateSizeToFitTextIfEnabled()
    {
        if (!ExpandWidthToText || !Visible) return;

        var owningWindow = GetOwningWindow() as Direct2DAppWindow;
        if (owningWindow == null || owningWindow.DWriteFactory == null) return;
        IDWriteFactory dwriteFactory = owningWindow.DWriteFactory;


        string textToMeasure = string.IsNullOrEmpty(Text) ? PlaceholderText : Text;
        if (string.IsNullOrEmpty(textToMeasure))
        {
            Size = new Vector2(TextOrigin.X * 2 + 20, Size.Y); // Minimum size
            return;
        }

        float measuredWidth = MeasureTextWidth(dwriteFactory, textToMeasure, Styles.Current);
        Size = new Vector2(measuredWidth + TextOrigin.X * 2, Size.Y);
    }

    internal float MeasureTextWidth(IDWriteFactory dwriteFactory, string text, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(text)) return 0f;

        using IDWriteTextFormat textFormat = dwriteFactory.CreateTextFormat(
            style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, CultureInfo.CurrentCulture.Name);
        textFormat.WordWrapping = WordWrapping.NoWrap;

        using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(text, textFormat, float.MaxValue, float.MaxValue);
        return textLayout.Metrics.WidthIncludingTrailingWhitespace;
    }

    internal float MeasureSingleCharWidth(DrawingContext context, string character, ButtonStyle style)
    {
        if (string.IsNullOrEmpty(character)) return 0f;
        IDWriteFactory dwriteFactory = context.DWriteFactory;
        if (dwriteFactory == null) return 0f; // Cannot measure

        using IDWriteTextFormat textFormat = dwriteFactory.CreateTextFormat(
            style.FontName, null, style.FontWeight, style.FontStyle, style.FontStretch, style.FontSize, CultureInfo.CurrentCulture.Name);
        textFormat.WordWrapping = WordWrapping.NoWrap;

        using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(character, textFormat, float.MaxValue, float.MaxValue);
        return textLayout.Metrics.Width;
    }

    public void Insert(string textToInsert)
    {
        if (!Editable || string.IsNullOrEmpty(textToInsert)) return;

        PushStateForUndo();
        foreach (char c in textToInsert)
        {
            InsertCharacterLogic(c);
        }
    }

    private void HandleCharacterInput()
    {
        if (_pendingCharInput.HasValue)
        {
            char typedChar = _pendingCharInput.Value;
            _pendingCharInput = null; // Consume it

            if (Text.Length < MaxCharacters)
            {
                if (ValidCharacters.Count == 0 || ValidCharacters.Contains(typedChar))
                {
                    PushStateForUndo();
                    InsertCharacterLogic(typedChar);
                }
            }
        }
    }

    private void InsertCharacterLogic(char c)
    {
        if (Text.Length >= MaxCharacters) return;

        Text = Text.Insert(CaretLogicalPosition, c.ToString());
        CaretLogicalPosition++;
        UpdateCaretDisplayPositionAndStartIndex();
    }

    private void HandleBackspace()
    {
        bool ctrlHeld = Input.IsKeyDown(KeyCode.LeftControl) || Input.IsKeyDown(KeyCode.RightControl);

        if (Input.IsKeyPressed(KeyCode.Backspace))
        {
            _backspaceHeld = true;
            _backspaceTimer = 0f;
            _backspaceCtrlHeld = ctrlHeld; // Capture ctrl state at initial press
            PerformBackspaceAction(_backspaceCtrlHeld);
        }
        else if (Input.IsKeyDown(KeyCode.Backspace) && _backspaceHeld)
        {
            _backspaceTimer += Time.Delta;
            if (_backspaceTimer >= BackspaceDelay)
            {
                // Simplified repeat logic
                if ((_backspaceTimer - BackspaceDelay) % BackspaceSpeed < Time.Delta)
                {
                    PerformBackspaceAction(_backspaceCtrlHeld);
                }
            }
        }
        else if (Input.IsKeyReleased(KeyCode.Backspace))
        {
            _backspaceHeld = false;
            _backspaceTimer = 0f;
        }
    }

    private void PerformBackspaceAction(bool isCtrlHeld)
    {
        if (Text.Length == 0 || CaretLogicalPosition == 0) return;

        PushStateForUndo();
        if (isCtrlHeld) // Delete previous word
        {
            int originalCaretPos = CaretLogicalPosition;
            int wordStart = FindPreviousWordStart(Text, originalCaretPos);
            Text = Text.Remove(wordStart, originalCaretPos - wordStart);
            CaretLogicalPosition = wordStart;
        }
        else // Delete single character
        {
            Text = Text.Remove(CaretLogicalPosition - 1, 1);
            CaretLogicalPosition--;
        }
        UpdateCaretDisplayPositionAndStartIndex();
    }

    private int FindPreviousWordStart(string text, int currentPos)
    {
        if (currentPos == 0) return 0;
        int pos = currentPos - 1;
        // Skip trailing spaces before the word
        while (pos > 0 && char.IsWhiteSpace(text[pos])) pos--;
        // Find start of the word (non-space)
        while (pos > 0 && !char.IsWhiteSpace(text[pos - 1])) pos--;
        return pos;
    }

    private void HandleDelete()
    {
        if (Input.IsKeyPressed(KeyCode.Delete))
        {
            if (CaretLogicalPosition < Text.Length)
            {
                PushStateForUndo();
                Text = Text.Remove(CaretLogicalPosition, 1);
                // CaretLogicalPosition doesn't change, but display might if text scrolls
                UpdateCaretDisplayPositionAndStartIndex();
            }
        }
    }

    private void HandleHomeEndKeys()
    {
        if (Input.IsKeyPressed(KeyCode.Home))
        {
            CaretLogicalPosition = 0;
            UpdateCaretDisplayPositionAndStartIndex();
        }
        else if (Input.IsKeyPressed(KeyCode.End))
        {
            CaretLogicalPosition = Text.Length;
            UpdateCaretDisplayPositionAndStartIndex();
        }
    }

    private void HandleClipboardPaste()
    {
        if ((Input.IsKeyDown(KeyCode.LeftControl) || Input.IsKeyDown(KeyCode.RightControl)) && Input.IsKeyPressed(KeyCode.V))
        {
            try
            {
                // TODO: Enable System.Windows.Forms in the project file (.csproj) to use Clipboard.
                // For .NET Core/5+, add: <FrameworkReference Include="Microsoft.WindowsDesktop.App.WindowsForms" />
                // For .NET Framework, add a reference to System.Windows.Forms.dll.
                // string clipboardText = Clipboard.GetText(); 
                string clipboardText = ""; // Placeholder
                Log.Warning("Clipboard.GetText() is currently disabled. Project setup required for System.Windows.Forms.");

                if (!string.IsNullOrEmpty(clipboardText))
                {
                    // Sanitize clipboard text (e.g., remove newlines)
                    clipboardText = clipboardText.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                    Insert(clipboardText);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error pasting from clipboard: {ex.Message}");
            }
        }
    }

    private void HandleUndoRedo()
    {
        bool ctrlHeld = Input.IsKeyDown(KeyCode.LeftControl) || Input.IsKeyDown(KeyCode.RightControl);

        if (ctrlHeld && Input.IsKeyPressed(KeyCode.Z))
        {
            _undoHeld = true;
            _undoTimer = 0f;
            Undo();
        }
        else if (ctrlHeld && Input.IsKeyDown(KeyCode.Z) && _undoHeld)
        {
            _undoTimer += Time.Delta;
            if (_undoTimer >= UndoDelay)
            {
                if ((_undoTimer - UndoDelay) % UndoSpeed < Time.Delta)
                {
                    Undo();
                }
            }
        }
        else if (Input.IsKeyReleased(KeyCode.Z))
        {
            _undoHeld = false;
            _undoTimer = 0f;
        }


        if (ctrlHeld && Input.IsKeyPressed(KeyCode.Y))
        {
            Redo();
        }
    }

    private void ConfirmOnEnter()
    {
        if (Input.IsKeyPressed(KeyCode.Enter))
        {
            ConfirmAction();
        }
    }

    private void ConfirmAction()
    {
        Selected = false; // Deselect on confirm
        Confirmed?.Invoke(this, Text);
    }

    internal IDWriteFactory? GetDWriteFactory()
    {
        var owningWindow = GetOwningWindow() as Direct2DAppWindow;
        return owningWindow?.DWriteFactory;
    }

    internal int GetDisplayableCharactersCount()
    {
        if (Size.X <= TextOrigin.X * 2) return 0;

        float availableWidth = Size.X - TextOrigin.X * 2;
        if (availableWidth <= 0) return 0;

        IDWriteFactory? dwriteFactory = GetDWriteFactory();
        if (dwriteFactory == null)
        {
            Log.Warning("LineEdit.GetDisplayableCharactersCount: DWriteFactory not available. Falling back to rough estimate.");
            return (int)(availableWidth / 8); // Rough fallback
        }

        if (TextStartIndex >= Text.Length && Text.Length > 0)
        {
            return 0;
        }
        if (Text.Length == 0) return 0;

        string textToMeasure = Text.Substring(TextStartIndex);
        if (string.IsNullOrEmpty(textToMeasure)) return 0;

        IDWriteTextFormat? textFormat = (GetOwningWindow() as Direct2DAppWindow)?.GetOrCreateTextFormat(Styles.Current);
        if (textFormat == null)
        {
            Log.Warning("LineEdit.GetDisplayableCharactersCount: Could not get TextFormat. Falling back to rough estimate.");
            return (int)(availableWidth / 8);
        }
        textFormat.WordWrapping = WordWrapping.NoWrap;

        using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(
            textToMeasure,
            textFormat,
            float.MaxValue,
            Size.Y
        );

        ClusterMetrics[] clusterMetricsBuffer = new ClusterMetrics[textToMeasure.Length];
        Result result = textLayout.GetClusterMetrics(clusterMetricsBuffer, out uint actualClusterCount);

        if (result.Failure)
        {
            Log.Error($"LineEdit.GetDisplayableCharactersCount: GetClusterMetrics failed with HRESULT {result.Code}");
            return (int)(availableWidth / 8); // Fallback on error
        }

        if (actualClusterCount == 0) return 0;

        float currentCumulativeWidth = 0;
        int displayableCharacterLengthInSubstring = 0;

        for (int i = 0; i < actualClusterCount; i++)
        {
            ClusterMetrics cluster = clusterMetricsBuffer[i];
            if (currentCumulativeWidth + cluster.Width <= availableWidth)
            {
                currentCumulativeWidth += cluster.Width;
                displayableCharacterLengthInSubstring += (int)cluster.Length;
            }
            else
            {
                break;
            }
        }
        return displayableCharacterLengthInSubstring;
    }


    internal void UpdateCaretDisplayPositionAndStartIndex()
    {
        if (Text.Length == 0)
        {
            TextStartIndex = 0;
            _caret.CaretDisplayPositionX = 0;
            CaretLogicalPosition = 0;
            return;
        }

        float availableWidth = Size.X - TextOrigin.X * 2;
        if (availableWidth <= 0)
        {
            TextStartIndex = 0;
            _caret.CaretDisplayPositionX = 0;
            return;
        }

        // Auto-scroll to show full text when enabled and text fits
        if (AutoScrollToShowFullText)
        {
            IDWriteFactory? dwriteFactory = GetDWriteFactory();
            if (dwriteFactory != null)
            {
                float fullTextWidth = MeasureTextWidth(dwriteFactory, Text, Styles.Current);
                if (fullTextWidth <= availableWidth)
                {
                    TextStartIndex = 0;
                    _caret.CaretDisplayPositionX = CaretLogicalPosition;
                    return;
                }
            }
        }

        int displayableChars = GetDisplayableCharactersCount();
        if (displayableChars <= 0 && Text.Length > 0) // If no chars fit but text exists
        {
            TextStartIndex = CaretLogicalPosition;
            _caret.CaretDisplayPositionX = 0;
            TextStartIndex = Math.Clamp(TextStartIndex, 0, Text.Length);
            return;
        }

        // Auto-scroll to show more characters when possible
        if (AutoScrollToShowFullText)
        {
            // Calculate how many additional characters we can show at the beginning
            int maxVisibleChars = GetDisplayableCharactersCount(0);
            if (maxVisibleChars > displayableChars && TextStartIndex > 0)
            {
                int charactersToShow = maxVisibleChars - displayableChars;
                TextStartIndex = Math.Max(0, TextStartIndex - charactersToShow);
                displayableChars = GetDisplayableCharactersCount(); // Recalculate after adjusting start index
            }
        }

        if (CaretLogicalPosition < TextStartIndex)
        {
            TextStartIndex = CaretLogicalPosition;
        }
        else if (CaretLogicalPosition >= TextStartIndex + displayableChars)
        {
            TextStartIndex = CaretLogicalPosition - displayableChars + 1;
        }

        TextStartIndex = Math.Max(0, TextStartIndex);
        if (TextStartIndex + displayableChars > Text.Length && displayableChars > 0)
        {
            TextStartIndex = Math.Max(0, Text.Length - displayableChars);
        }
        TextStartIndex = Math.Clamp(TextStartIndex, 0, Math.Max(0, Text.Length - 1));

        _caret.CaretDisplayPositionX = CaretLogicalPosition - TextStartIndex;
        _caret.CaretDisplayPositionX = Math.Clamp(_caret.CaretDisplayPositionX, 0, displayableChars > 0 ? displayableChars : 0);
    }

    // Overload to measure from specific start index
    private int GetDisplayableCharactersCount(int startIndex)
    {
        if (Size.X <= TextOrigin.X * 2) return 0;

        float availableWidth = Size.X - TextOrigin.X * 2;
        if (availableWidth <= 0) return 0;

        IDWriteFactory? dwriteFactory = GetDWriteFactory();
        if (dwriteFactory == null)
        {
            Log.Warning("LineEdit.GetDisplayableCharactersCount: DWriteFactory not available. Falling back to rough estimate.");
            return (int)(availableWidth / 8); // Rough fallback
        }

        if (startIndex >= Text.Length && Text.Length > 0) return 0;
        if (Text.Length == 0) return 0;

        string textToMeasure = Text.Substring(startIndex);
        if (string.IsNullOrEmpty(textToMeasure)) return 0;

        IDWriteTextFormat? textFormat = (GetOwningWindow() as Direct2DAppWindow)?.GetOrCreateTextFormat(Styles.Current);
        if (textFormat == null)
        {
            Log.Warning("LineEdit.GetDisplayableCharactersCount: Could not get TextFormat. Falling back to rough estimate.");
            return (int)(availableWidth / 8);
        }
        textFormat.WordWrapping = WordWrapping.NoWrap;

        using IDWriteTextLayout textLayout = dwriteFactory.CreateTextLayout(
            textToMeasure,
            textFormat,
            float.MaxValue,
            Size.Y
        );

        ClusterMetrics[] clusterMetricsBuffer = new ClusterMetrics[textToMeasure.Length];
        Result result = textLayout.GetClusterMetrics(clusterMetricsBuffer, out uint actualClusterCount);

        if (result.Failure)
        {
            Log.Error($"LineEdit.GetDisplayableCharactersCount: GetClusterMetrics failed with HRESULT {result.Code}");
            return (int)(availableWidth / 8); // Fallback on error
        }

        if (actualClusterCount == 0) return 0;

        float currentCumulativeWidth = 0;
        int displayableCharacterLengthInSubstring = 0;

        for (int i = 0; i < actualClusterCount; i++)
        {
            ClusterMetrics cluster = clusterMetricsBuffer[i];
            if (currentCumulativeWidth + cluster.Width <= availableWidth)
            {
                currentCumulativeWidth += cluster.Width;
                displayableCharacterLengthInSubstring += (int)cluster.Length;
            }
            else
            {
                break;
            }
        }
        return displayableCharacterLengthInSubstring;
    }

    private void PushStateForUndo()
    {
        if (_undoStack.Count > 0 && _undoStack.Peek().Text == _text && _undoStack.Peek().CaretPosition == CaretLogicalPosition)
        {
            return; // Don't push identical state
        }
        if (_undoStack.Count >= HistoryLimit)
        {
            var tempList = _undoStack.ToList();
            tempList.RemoveAt(0); // Remove oldest
            _undoStack = new Stack<LineEditState>(tempList.AsEnumerable().Reverse());
        }
        _undoStack.Push(new LineEditState(_text, CaretLogicalPosition, TextStartIndex));
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_undoStack.Count > 0)
        {
            LineEditState currentState = new LineEditState(_text, CaretLogicalPosition, TextStartIndex);
            _redoStack.Push(currentState);
            if (_redoStack.Count > HistoryLimit)
            {
                var tempList = _redoStack.ToList();
                tempList.RemoveAt(0);
                _redoStack = new Stack<LineEditState>(tempList.AsEnumerable().Reverse());
            }

            LineEditState previousState = _undoStack.Pop();
            _text = previousState.Text;
            CaretLogicalPosition = previousState.CaretPosition;
            TextStartIndex = previousState.TextStartIndex;

            TextChanged?.Invoke(this, _text);
            UpdateCaretDisplayPositionAndStartIndex();
        }
    }

    public void Redo()
    {
        if (_redoStack.Count > 0)
        {
            LineEditState currentState = new LineEditState(_text, CaretLogicalPosition, TextStartIndex);
            _undoStack.Push(currentState);
            if (_undoStack.Count > HistoryLimit)
            {
                var tempList = _undoStack.ToList();
                tempList.RemoveAt(0);
                _undoStack = new Stack<LineEditState>(tempList.AsEnumerable().Reverse());
            }

            LineEditState nextState = _redoStack.Pop();
            _text = nextState.Text;
            CaretLogicalPosition = nextState.CaretPosition;
            TextStartIndex = nextState.TextStartIndex;

            TextChanged?.Invoke(this, _text);
            UpdateCaretDisplayPositionAndStartIndex();
        }
    }

    protected Vector2 GetLocalMousePosition()
    {
        var owningWindowNode = GetOwningWindowNode();
        if (owningWindowNode != null)
        {
            return owningWindowNode.LocalMousePosition;
        }

        var mainAppWindow = ApplicationServer.Instance.GetMainAppWindow();
        if (mainAppWindow != null)
        {
            return mainAppWindow.GetLocalMousePosition();
        }

        Log.Warning($"LineEdit '{Name}': Could not determine owning window for local mouse position. Using global Input.MousePosition.");
        return Input.MousePosition;
    }

    // Helper class for Undo/Redo state
    protected class LineEditState
    {
        public string Text { get; }
        public int CaretPosition { get; } // Logical caret position
        public int TextStartIndex { get; }

        public LineEditState(string text, int caretPosition, int textStartIndex)
        {
            Text = text;
            CaretPosition = caretPosition;
            TextStartIndex = textStartIndex;
        }
    }
}
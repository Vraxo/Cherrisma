﻿using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using LibVLCSharp.Shared;
using SharpGen.Runtime;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace Cherris;

public class VideoPlayer : Node2D, IDisposable
{
    private static bool _isLibVlcInitialized = false;
    private static readonly object _initLock = new();
    private static readonly object _frameLock = new();

    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;
    private bool _isDisposed = false;

    private ID2D1Bitmap? _videoBitmap;
    private byte[]? _latestFrameDataRaw;
    private uint _videoWidth = 0;
    private uint _videoHeight = 0;
    private bool _newFrameAvailable = false;
    private bool _formatConfigured = false;

    private uint _receivedChroma = 0;
    private uint _receivedPitch = 0;
    private uint _bufferSize = 0;

    private byte[]? _conversionBufferBGRA32;

    private static readonly uint FourCC_RV32 = CalculateFourCC("RV32");
    private static readonly uint FourCC_RV24 = CalculateFourCC("RV24");

    private MediaPlayer.LibVLCVideoFormatCb? _videoFormatCallbackDelegate;
    private MediaPlayer.LibVLCVideoLockCb? _videoLockCallbackDelegate;
    private MediaPlayer.LibVLCVideoUnlockCb? _videoUnlockCallbackDelegate;
    private MediaPlayer.LibVLCVideoDisplayCb? _videoDisplayCallbackDelegate;
    private MediaPlayer.LibVLCVideoCleanupCb? _videoCleanupCallbackDelegate;
    
    public bool AutoPlay { get; set; } = false;
    public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;
    public long DurationMilliseconds => _mediaPlayer?.Length ?? 0;
    public bool SuppressPositionRatioChangedEvent { get; set; } = false;

    public override Vector2 Size
    {
        get
        {
            lock (_frameLock)
            {
                // Return actual video size if available, otherwise fallback to base
                return _videoWidth > 0 && _videoHeight > 0
                    ? new(_videoWidth, _videoHeight)
                    : base.Size; // You might want to set a default size here if needed
            }
        }
        set
        {
            base.Size = value;
        }
    }

    private bool _loop = false;
    public bool Loop
    {
        get => _loop;
        set
        {
            if (_loop == value) return;
            _loop = value;
        }
    }

    private float _volume = 100f;
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 100f);

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = (int)_volume;
            }
        }
    }

    public float PositionRatio
    {
        get => _mediaPlayer?.Position ?? 0f;

        set
        {
            if (_mediaPlayer is null)
            {
                return;
            }

            _mediaPlayer.Position = float.Clamp(value, 0f, 1f);
        }
    }

    private string _filePath = "";
    public string FilePath
    {
        get => _filePath;

        set
        {
            if (_filePath == value)
            {
                return;
            }

            _filePath = value;

            if (_mediaPlayer is not null && _libVLC is not null)
            {
                LoadMedia();
            }

            // Invalidate any existing bitmap if path changes
            lock (_frameLock)
            {
                _videoBitmap?.Dispose();
                _videoBitmap = null;
                _videoWidth = 0;
                _videoHeight = 0;
                _receivedChroma = 0; // Reset format info
                _receivedPitch = 0;
                _bufferSize = 0;
                _latestFrameDataRaw = null;
                _conversionBufferBGRA32 = null;
                _newFrameAvailable = false;
                _formatConfigured = false;
            }
        }
    }

    public float PlaybackSpeed
    {
        get;
        set
        {
            // You might want to clamp the value to a reasonable range, e.g., 0.25f to 4.0f
            // float newSpeed = Math.Max(0.1f, Math.Min(value, 16.0f)); // Example clamping
            float newSpeed = float.Clamp(value, 0.25f, 4f); // Using raw value for now
            
            if (float.Abs(field - newSpeed) <= float.Epsilon) // Check for actual change
            {
                return;
            }

            field = newSpeed;
            _mediaPlayer?.SetRate(PlaybackSpeed);
        }
    } = 1.0f;

    // - - - - - - - - - - - - - - -  Events - - - - - - - - - - - - - - - 

    public event EventHandler? PlaybackStarted;
    public event EventHandler? PlaybackPaused;
    public event EventHandler? PlaybackStopped;
    public event EventHandler? PlaybackEnded;
    public event EventHandler<string>? PlaybackError;
    public event Action<float>? PositionRatioChanged;

    // - - - - - - - - - - - - - - - Main - - - - - - - - - - - - - - - 

    public override void Make()
    {
        base.Make();
        InitializeLibVLC();
    }

    public override void Ready()
    {
        base.Ready();

        if (!AutoPlay || _mediaPlayer == null || _media == null || _mediaPlayer.IsPlaying)
        {
            return;
        }

        Play();
    }

    public override void Free()
    {
        Dispose();
        base.Free();
    }

    public override void Draw(DrawingContext context)
    {
        if (!Visible || context.RenderTarget == null || _isDisposed) return;

        bool bitmapNeedsRecreation = false;
        bool frameNeedsProcessing = false; // Flag if new frame needs conversion/copy
        uint currentWidth = 0;
        uint currentHeight = 0;
        uint currentChroma = 0;
        uint currentPitch = 0;
        bool isNewFrameAvailableInLock = false;

        // --- Step 1: Check state under lock ---
        lock (_frameLock)
        {
            currentWidth = _videoWidth;
            currentHeight = _videoHeight;
            currentChroma = _receivedChroma; // Get the format code
            currentPitch = _receivedPitch;   // Get the pitch for the raw data
            isNewFrameAvailableInLock = _newFrameAvailable;

            // Check if D2D BGRA bitmap needs recreation (size changed OR format wasn't configured before)
            // Only attempt recreation if the format IS configured now.
            if ((_videoBitmap == null || _videoBitmap.PixelSize.Width != currentWidth || _videoBitmap.PixelSize.Height != currentHeight)
                && currentWidth > 0 && currentHeight > 0)
            {
                bitmapNeedsRecreation = true;
            }

            // Check if a new frame needs processing (conversion/copy)
            // Can only process if format is configured AND bitmap exists AND raw data exists
            if (isNewFrameAvailableInLock && _formatConfigured && _videoBitmap != null && _latestFrameDataRaw != null && currentChroma != 0)
            {
                // Only process if bitmap dimensions match current video dimensions
                if (_videoBitmap.PixelSize.Width == currentWidth && _videoBitmap.PixelSize.Height == currentHeight)
                {
                    frameNeedsProcessing = true;
                }
                else // Mismatch, force recreation instead
                {
                    Log.Warning($"Draw: Bitmap dimensions ({_videoBitmap?.PixelSize.Width}x{_videoBitmap?.PixelSize.Height}) mismatch current video dimensions ({currentWidth}x{currentHeight}). Forcing recreation.");

                    if (currentWidth > 0 && currentHeight > 0)
                    {
                        bitmapNeedsRecreation = true; // Force recreation
                    }

                    frameNeedsProcessing = false; // Don't process this frame if dims mismatch
                }
            }
        } // --- End Lock for Step 1 ---

        // --- Step 2: Recreate D2D Bitmap if needed (outside lock) ---
        if (bitmapNeedsRecreation)
        {
            ID2D1Bitmap? oldBitmap = null;
            lock (_frameLock) { oldBitmap = _videoBitmap; _videoBitmap = null; }
            oldBitmap?.Dispose();

            // Double-check dimensions and configured status before creating
            lock (_frameLock)
            {
                if (!_formatConfigured || _videoWidth == 0 || _videoHeight == 0)
                {
                    Log.Warning($"Draw: Cannot recreate bitmap. Format Configured: {_formatConfigured}, Dimensions: {_videoWidth}x{_videoHeight}."); return;
                }
            }

            try
            {
                // D2D Bitmap is always BGRA32
                var bitmapProperties = new BitmapProperties(new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Ignore));
                var newBitmap = context.RenderTarget.CreateBitmap(new SizeI((int)currentWidth, (int)currentHeight), bitmapProperties);

                lock (_frameLock) { _videoBitmap = newBitmap; }
                Log.Info($"Draw: BGRA32 Bitmap recreated successfully ({currentWidth}x{currentHeight}).");

                // Check if the pending frame can be processed *now* after recreation
                lock (_frameLock)
                {
                    // Re-check all conditions
                    if (_formatConfigured && isNewFrameAvailableInLock && _latestFrameDataRaw != null && currentChroma != 0 &&
                       _videoBitmap != null && _videoBitmap.PixelSize.Width == currentWidth && _videoBitmap.PixelSize.Height == currentHeight)
                    {
                        frameNeedsProcessing = true; // Process frame now
                    }
                    else { frameNeedsProcessing = false; /*Log.Info("Draw: Frame processing not ready after bitmap recreation.");*/ }
                }
            }
            catch (SharpGenException sgex) when (sgex.ResultCode.Code == Vortice.Direct2D1.ResultCode.RecreateTarget.Code)
            { Log.Warning($"Draw: Render target needs recreation during Bitmap creation."); lock (_frameLock) { _videoBitmap = null; } return; }
            catch (Exception ex) { Log.Error($"Failed to create ID2D1Bitmap: {ex.Message}"); lock (_frameLock) { _videoBitmap = null; } return; }
        }

        // --- Step 3: Process Frame (Convert/Copy) if needed ---
        if (frameNeedsProcessing)
        {
            byte[]? rawData = null;
            byte[]? bgraData = null; // Target for conversion/copy
            ID2D1Bitmap? bitmapForCopy = null;
            uint widthForUpdate = 0;
            uint heightForUpdate = 0;
            uint chromaForUpdate = 0;
            uint pitchForUpdate = 0; // Pitch of the raw data

            // Get data and refs under lock
            lock (_frameLock)
            {
                // Re-check all conditions *inside* the lock
                // Crucially, check _formatConfigured!
                if (_formatConfigured && _videoBitmap != null && _latestFrameDataRaw != null && _newFrameAvailable && _receivedChroma != 0 &&
                    _videoBitmap.PixelSize.Width == currentWidth && _videoBitmap.PixelSize.Height == currentHeight)
                {
                    widthForUpdate = currentWidth;
                    heightForUpdate = currentHeight;
                    // *** IMPORTANT: Use the CHROMA from the raw data buffer, NOT the one we forced (_receivedChroma) ***
                    // This assumes the Lock/Unlock callback gives us the *actual* format being delivered.
                    // If Lock/Unlock doesn't give format info, we might need another way or assume it matches our request.
                    // For now, let's assume we need to handle the format delivered in rawData, whose chroma might be implicitly RV32 or RV24 etc.
                    // We will use the _receivedChroma as the *assumed* format of the locked buffer for now.
                    chromaForUpdate = _receivedChroma; // <-- Revisit this if Lock/Unlock gives explicit format info
                    pitchForUpdate = _receivedPitch;   // Use the pitch associated with the buffer we locked (should match our forced pitch)
                    rawData = _latestFrameDataRaw;
                    bitmapForCopy = _videoBitmap;

                    // Ensure BGRA conversion buffer exists if needed (or for direct copy target)
                    uint bgraSize = widthForUpdate * heightForUpdate * 4;
                    if (_conversionBufferBGRA32 == null || _conversionBufferBGRA32.Length != bgraSize)
                    {
                        try { _conversionBufferBGRA32 = new byte[bgraSize]; }
                        catch (Exception ex) { Log.Error($"Failed to allocate BGRA buffer: {ex.Message}"); _conversionBufferBGRA32 = null; }
                    }
                    bgraData = _conversionBufferBGRA32;
                }
                else { /* Conditions no longer met */ rawData = null; bgraData = null; bitmapForCopy = null; }
            } // --- End Lock ---

            bool processedSuccessfully = false;
            if (rawData != null && bgraData != null && bitmapForCopy != null && widthForUpdate > 0 && heightForUpdate > 0)
            {
                try
                {
                    // *** FORMAT HANDLING ***
                    // Here we process based on 'chromaForUpdate', which currently is assumed to be the RV32 we requested.
                    if (chromaForUpdate == FourCC_RV32) // BGRA32
                    {
                        uint expectedBgraPitch = widthForUpdate * 4;
                        if (pitchForUpdate == expectedBgraPitch)
                        {
                            // Pitches match, direct copy
                            bitmapForCopy.CopyFromMemory(new Rectangle(0, 0, (int)widthForUpdate, (int)heightForUpdate), rawData, pitchForUpdate);
                            processedSuccessfully = true;
                        }
                        else
                        {
                            // This case *shouldn't* happen if we forced RV32 with the correct pitch, but handle defensively.
                            Log.Warning($"RV32 pitch mismatch (Expected {expectedBgraPitch}, Got {pitchForUpdate}). Using intermediate buffer copy.");
                            CopyMemoryWithPitch(rawData, pitchForUpdate, bgraData, expectedBgraPitch, widthForUpdate * 4, heightForUpdate);
                            bitmapForCopy.CopyFromMemory(new Rectangle(0, 0, (int)widthForUpdate, (int)heightForUpdate), bgraData, expectedBgraPitch);
                            processedSuccessfully = true;
                        }
                    }
                    // LibVLC might ignore our RV32 request and send something else like RV24.
                    // We need to be prepared for this possibility if the above doesn't work.
                    // For now, we assume RV32 is delivered if the setup succeeded.
                    // else if (chromaForUpdate == FourCC_RV24) // BGR24 - Keep this conversion just in case
                    // {
                    //     ConvertBGR24ToBGRA32(rawData, bgraData, widthForUpdate, heightForUpdate, pitchForUpdate);
                    //     uint bgraPitch = widthForUpdate * 4;
                    //     bitmapForCopy.CopyFromMemory(new Rectangle(0, 0, (int)widthForUpdate, (int)heightForUpdate), bgraData, bgraPitch);
                    //     processedSuccessfully = true;
                    // }
                    else
                    {
                        // This implies the format delivered doesn't match RV32, which we requested.
                        Log.Error($"Draw: Skipping frame processing. Unexpected video format received in buffer (Expected RV32): {FourCCToString(chromaForUpdate)} (0x{chromaForUpdate:X8})");
                    }

                    if (processedSuccessfully)
                    {
                        lock (_frameLock) { _newFrameAvailable = false; }
                    }

                }
                catch (SharpGenException sgex) when (sgex.ResultCode.Code == Vortice.Direct2D1.ResultCode.RecreateTarget.Code)
                { Log.Warning($"Draw: Render target needs recreation during CopyFromMemory."); lock (_frameLock) { _videoBitmap?.Dispose(); _videoBitmap = null; _newFrameAvailable = false; } return; }
                catch (Exception ex) { Log.Error($"Failed during frame processing/copy: {ex.Message}"); lock (_frameLock) { _videoBitmap?.Dispose(); _videoBitmap = null; _newFrameAvailable = false; } }
            }
            else if (frameNeedsProcessing) { Log.Warning($"Draw: Skipped frame processing. HasBitmap={bitmapForCopy != null}, HasRawData={rawData != null}, HasBGRAData={bgraData != null}, Chroma={chromaForUpdate}, W={widthForUpdate}, H={heightForUpdate}"); }
        }


        // --- Step 4: Draw D2D Bitmap ---
        ID2D1Bitmap? bitmapToDraw = null;
        lock (_frameLock) { bitmapToDraw = _videoBitmap; } // Get current bitmap ref


        if (bitmapToDraw != null && currentWidth > 0 && currentHeight > 0) // Use originally captured dimensions
        {
            try
            {
                var position = GlobalPosition - Origin;
                var size = ScaledSize;
                var destRect = new RectangleF(position.X, position.Y, size.X, size.Y);
                var sourceRect = new RectangleF(0, 0, bitmapToDraw.PixelSize.Width, bitmapToDraw.PixelSize.Height); // Use bitmap's actual size

                context.RenderTarget.DrawBitmap(bitmapToDraw, destRect, 1.0f, BitmapInterpolationMode.Linear, sourceRect);
            }
            catch (SharpGenException sgex) when (sgex.ResultCode.Code == Vortice.Direct2D1.ResultCode.RecreateTarget.Code)
            { Log.Warning($"Draw: Render target needs recreation during DrawBitmap."); lock (_frameLock) { _videoBitmap?.Dispose(); _videoBitmap = null; } return; }
            catch (ObjectDisposedException) { Log.Warning($"Draw: Bitmap was disposed before DrawBitmap."); lock (_frameLock) { _videoBitmap = null; } }
            catch (Exception ex) { Log.Error($"Failed to draw video bitmap: {ex.Message}"); lock (_frameLock) { _videoBitmap?.Dispose(); _videoBitmap = null; } }
        }
        else if (Visible) { DrawPlaceholder(context, GlobalPosition - Origin, base.Size); } // Draw placeholder if visible but no bitmap
    }

    private void InitializeLibVLC()
    {
        lock (_initLock)
        {
            if (!_isLibVlcInitialized)
            {
                try
                {
                    Core.Initialize();
                    _isLibVlcInitialized = true;
                    Log.Info("LibVLC Core initialized.");
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to initialize LibVLC Core: {ex.Message}");
                    PlaybackError?.Invoke(this, $"Failed to initialize LibVLC Core: {ex.Message}");
                    return;
                }
            }
        }

        try
        {
            List<string> libvlcOptions =
            [
                "--no-osd",
                "--avcodec-hw=none"
                // "--verbose=2" // Uncomment for more detailed VLC logs if needed
            ];

            _libVLC = new(libvlcOptions.ToArray());

            Log.Info($"LibVLC instance created with options: {string.Join(" ", libvlcOptions)}");

            _mediaPlayer = new MediaPlayer(_libVLC);

            _videoFormatCallbackDelegate = new(VideoFormatCallback);
            _videoCleanupCallbackDelegate = new(VideoCleanupCallback);
            _videoLockCallbackDelegate = new(VideoLockCallback);
            _videoUnlockCallbackDelegate = new(VideoUnlockCallback);
            _videoDisplayCallbackDelegate = new(VideoDisplayCallback);

            _mediaPlayer.SetVideoFormatCallbacks(_videoFormatCallbackDelegate, _videoCleanupCallbackDelegate);
            _mediaPlayer.SetVideoCallbacks(_videoLockCallbackDelegate, _videoUnlockCallbackDelegate, _videoDisplayCallbackDelegate);

            _mediaPlayer.Playing += OnPlaying;
            _mediaPlayer.Paused += OnPaused;
            _mediaPlayer.Stopped += OnStopped;
            _mediaPlayer.EndReached += OnEndReached;
            _mediaPlayer.EncounteredError += OnEncounteredError;
            _mediaPlayer.PositionChanged += OnPositionChanged;

            _mediaPlayer.Volume = (int)Volume;
            // Set initial playback speed if mediaplayer is already created
            // This is mostly redundant if LoadMedia also sets it, but good for safety.
            _mediaPlayer.SetRate(PlaybackSpeed);


            if (!string.IsNullOrEmpty(FilePath))
            {
                LoadMedia();
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to create LibVLC instance or MediaPlayer: {ex.Message}");
            PlaybackError?.Invoke(this, $"Failed to create LibVLC/MediaPlayer: {ex.Message}");
            DisposeVlcResources();
        }
    }

    private static uint CalculateFourCC(string code)
    {
        if (code is null || code.Length != 4)
        {
            throw new ArgumentException("FourCC code must be 4 characters long.", nameof(code));
        }

        byte[] bytes = Encoding.ASCII.GetBytes(code);

        if (BitConverter.IsLittleEndian)
        {
            return BitConverter.ToUInt32(bytes, 0);
        }

        Array.Reverse(bytes);

        return BitConverter.ToUInt32(bytes, 0);
    }

    private static string FourCCToString(uint fourCC)
    {
        byte[] bytes = BitConverter.GetBytes(fourCC);

        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        bool isAsciiPrintable = bytes.All(b => b >= 32 && b <= 126);

        return isAsciiPrintable
            ? Encoding.ASCII.GetString(bytes)
            : $"0x{fourCC:X8}";
    }

    private uint VideoFormatCallback(ref nint opaque, nint chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        // This callback might be called multiple times.
        // We need valid width, height, *and* pitches/lines to fully configure.
        uint currentChromaInt = 0;
        try
        {
            if (IntPtr.Size == 4) currentChromaInt = (uint)chroma.ToInt32();
            else currentChromaInt = (uint)chroma.ToInt64();
        }
        catch (OverflowException ex)
        {
            Log.Error($"VideoFormatCallback: Chroma IntPtr value overflows uint32. {ex.Message}");
            return 0; // Cannot process
        }

        string currentChromaStr = FourCCToString(currentChromaInt);

        Log.Info($"VideoFormatCallback: Received Format='{currentChromaStr}' (0x{currentChromaInt:X8}), Dimensions={width}x{height}, Pitch={pitches}, Lines={lines}");

        // *** Reject if dimensions are fundamentally invalid ***
        if (width == 0 || height == 0)
        {
            Log.Error($"VideoFormatCallback: Rejecting format due to invalid dimensions (W:{width}, H:{height}).");
            // Reset state if we previously had valid info
            lock (_frameLock)
            {
                ResetFormatState_Locked();
            }
            return 0; // Indicate failure
        }

        // *** Force BGRA32 (RV32) format for Direct2D compatibility ***
        uint targetChroma = FourCC_RV32;
        uint targetPitch = width * 4; // BGRA = 4 bytes per pixel
        uint targetLines = height;
        string targetChromaStr = FourCCToString(targetChroma);

        lock (_frameLock)
        {
            // Check if anything crucial changed OR if we haven't configured yet
            bool dimensionsChanged = (_videoWidth != width || _videoHeight != height);
            // We are forcing RV32, so we don't check _receivedChroma vs targetChroma for change here

            if (dimensionsChanged || !_formatConfigured)
            {
                Log.Info($"VideoFormatCallback: Configuring/Updating format - Size: {width}x{height}. Forcing Chroma: {targetChromaStr}, Pitch: {targetPitch}, Lines: {targetLines}. Previously Configured: {_formatConfigured}");

                // Clear potentially outdated resources if dimensions changed
                if (dimensionsChanged)
                {
                    _videoBitmap?.Dispose(); _videoBitmap = null;
                    _latestFrameDataRaw = null; _conversionBufferBGRA32 = null;
                    _newFrameAvailable = false;
                }

                // Store the *target* format details
                _receivedChroma = targetChroma; // Store what we requested
                _videoWidth = width;
                _videoHeight = height;
                _receivedPitch = targetPitch; // Store calculated pitch for BGRA32
                _bufferSize = targetPitch * targetLines; // Calculate buffer size for BGRA32

                _formatConfigured = true; // Mark as successfully configured
                Log.Info($"Calculated buffer size for forced BGRA32: {_bufferSize} bytes");
            }
            // Update the parameters passed by reference to tell LibVLC we want RV32
            Marshal.WriteInt32(chroma, (int)targetChroma); // Write the FourCC code
            pitches = targetPitch;
            lines = targetLines;
        }

        return targetLines; // Return the height (number of lines) to indicate success
    }

    private void VideoCleanupCallback(ref nint opaque)
    {
        Log.Info("VideoCleanupCallback called.");
    }

    private nint VideoLockCallback(nint opaque, nint planes)
    {
        lock (_frameLock)
        {
            // Check if format is FULLY configured (valid pitch/lines received)
            if (!_formatConfigured || _bufferSize == 0 || _receivedPitch == 0)
            {
                Log.Warning($"VideoLockCallback: Cannot lock, format not fully configured yet (Configured={_formatConfigured}, Size={_bufferSize}, Pitch={_receivedPitch}).");
                return nint.Zero;
            }

            // Allocate/Reallocate raw frame buffer if needed
            if (_latestFrameDataRaw == null || _latestFrameDataRaw.Length != _bufferSize)
            {
                try
                {
                    _latestFrameDataRaw = new byte[_bufferSize];
                    Log.Info($"Allocated raw frame buffer: {_bufferSize} bytes for format {FourCCToString(_receivedChroma)}");
                }
                catch (Exception ex)
                {
                    Log.Error($"VideoLockCallback: Exception allocating raw buffer of size {_bufferSize}. {ex.Message}");
                    _latestFrameDataRaw = null;
                    ResetFormatState_Locked(); // Mark format as unconfigured
                    return nint.Zero;
                }
            }

            // We should have a buffer here.
            if (_latestFrameDataRaw == null)
            {
                Log.Error("VideoLockCallback: Raw Frame buffer is null after allocation check.");
                return nint.Zero;
            }

            // Pin the buffer and provide pointer to VLC
            var handle = GCHandle.Alloc(_latestFrameDataRaw, GCHandleType.Pinned);
            var bufferPtr = handle.AddrOfPinnedObject();

            if (planes == IntPtr.Zero)
            {
                Log.Error("VideoLockCallback: Received null planes pointer.");
                handle.Free();
                return nint.Zero;
            }
            // For packed formats (like RV32, RV24), VLC expects a single plane pointer.
            // For planar formats (like I420), it expects pointers for Y, U, V planes.
            // We assume planes[0] is the target for packed formats.
            // TODO: Handle planar formats correctly if needed (write multiple pointers).
            Marshal.WriteIntPtr(planes, 0, bufferPtr); // Write pointer to planes[0]

            return GCHandle.ToIntPtr(handle); // Return handle to be freed in Unlock
        }
    }

    private void VideoUnlockCallback(nint opaque, nint picture, nint planes)
    {
        lock (_frameLock)
        {
            if (picture == IntPtr.Zero)
            {
                Log.Warning("VideoUnlockCallback: Received null picture handle.");
                return;
            }

            _newFrameAvailable = true; // Signal that a raw frame is ready

            try
            {
                var handle = GCHandle.FromIntPtr(picture);
                if (handle.IsAllocated) handle.Free();
                else Log.Warning("VideoUnlockCallback: GCHandle was not allocated?");
            }
            catch (Exception ex)
            {
                Log.Error($"VideoUnlockCallback: Exception freeing GCHandle {picture}. {ex.Message}");
            }
        }
    }

    private void VideoDisplayCallback(nint opaque, nint picture)
    {
        // Not strictly needed for our rendering logic which polls _newFrameAvailable.
    }

    private void LoadMedia()
    {
        if (_libVLC == null || _mediaPlayer == null || string.IsNullOrEmpty(FilePath))
        {
            Log.Warning($"Cannot load media: LibVLC/MediaPlayer not ready or FilePath is empty for node '{Name}'.");
            return;
        }

        if (_mediaPlayer.IsPlaying) _mediaPlayer.Stop();

        lock (_frameLock)
        {
            _media?.Dispose(); _media = null;
            _videoBitmap?.Dispose(); _videoBitmap = null;
            _latestFrameDataRaw = null;
            _conversionBufferBGRA32 = null;
            _videoWidth = 0; _videoHeight = 0;
            _receivedChroma = 0; _receivedPitch = 0; _bufferSize = 0;
            _newFrameAvailable = false;
            Log.Info($"Cleared previous media state for node '{Name}'.");
            _formatConfigured = false;
        }


        string absolutePath;
        try { absolutePath = Path.GetFullPath(FilePath); }
        catch (Exception ex) { Log.Error($"Error getting full path '{FilePath}': {ex.Message}"); PlaybackError?.Invoke(this, $"Invalid path: {ex.Message}"); return; }

        if (!File.Exists(absolutePath)) { Log.Error($"Video file not found: {absolutePath}"); PlaybackError?.Invoke(this, $"Video file not found: {absolutePath}"); return; }

        // No specific options needed here now regarding chroma
        List<string> mediaOptions = new List<string> { ":no-video-title-show" };

        try
        {
            _media = new Media(_libVLC, new Uri(absolutePath), mediaOptions.ToArray());
            _mediaPlayer.Media = _media;
            Log.Info($"Loaded media: {absolutePath} for node '{Name}'.");

            // Apply the current playback speed to the new media
            if (_mediaPlayer != null)
            {
                _mediaPlayer.SetRate(PlaybackSpeed);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error loading media '{absolutePath}': {ex.Message}");
            PlaybackError?.Invoke(this, $"Error loading media: {ex.Message}");
            _media?.Dispose(); _media = null;
        }
        // Reset format flag on new media load
        lock (_frameLock) { _formatConfigured = false; }
    }

    // - - - - - - - - - - - - - - - Usage - - - - - - - - - - - - - - - 

    public void Play()
    {
        if (_mediaPlayer != null && _media != null)
        {
            if (_mediaPlayer.State == VLCState.Error) { Log.Warning($"Cannot play node '{Name}', player state is Error."); PlaybackError?.Invoke(this, $"Player state is Error"); return; }

            // Ensure playback speed is set before playing, in case it was changed while stopped/paused.
            _mediaPlayer.SetRate(PlaybackSpeed);

            if (!_mediaPlayer.IsPlaying)
            {
                Log.Info($"Playing media: {FilePath} for node '{Name}' at speed: {PlaybackSpeed}x.");
                if (!_mediaPlayer.Play())
                {
                    Log.Error($"MediaPlayer.Play() returned false for node '{Name}'. State: {_mediaPlayer.State}");
                    PlaybackError?.Invoke(this, "Play() failed.");
                }
            }
        }
        else { Log.Warning($"Cannot play: MediaPlayer or Media not ready for node '{Name}'."); }
    }

    public void Pause()
    {
        if (_mediaPlayer?.CanPause ?? false) { _mediaPlayer.Pause(); }
        else { Log.Warning($"Cannot pause node '{Name}'."); }
    }

    public void Stop()
    {
        if (_mediaPlayer != null) { _mediaPlayer.Stop(); }
        else { Log.Warning($"Cannot stop node '{Name}'."); }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    // - - - - - - - - - - - - - - - Static - - - - - - - - - - - - - - - 

    private static void ConvertBGR24ToBGRA32(byte[] bgr24Data, byte[] bgra32Data, uint width, uint height, uint bgrPitch)
    {
        uint bgraPitch = width * 4;
        int numPixelsWidth = (int)width;

        ulong requiredBgraSize = (ulong)height * bgraPitch;
        ulong requiredBgrSize = (ulong)height * bgrPitch;

        if ((ulong)bgra32Data.LongLength < requiredBgraSize || (ulong)bgr24Data.LongLength < requiredBgrSize)
        {
            Log.Error($"Buffer size mismatch in ConvertBGR24ToBGRA32 (Pitched). BGR:{bgr24Data.Length}, BGRA:{bgra32Data.Length}, Expected BGR:{requiredBgrSize}, Expected BGRA:{requiredBgraSize}");
            return;
        }

        for (int y = 0; y < height; y++)
        {
            int bgrRowStart = (int)(y * bgrPitch);
            int bgraRowStart = (int)(y * bgraPitch);

            for (int x = 0; x < numPixelsWidth; x++)
            {
                int bgrIndex = bgrRowStart + x * 3;
                int bgraIndex = bgraRowStart + x * 4;

                if (bgrIndex + 2 >= bgr24Data.Length || bgraIndex + 3 >= bgra32Data.Length)
                {
                    Log.Error($"Index out of bounds during BGR->BGRA conversion at x={x}, y={y}.");
                    return;
                }

                byte b = bgr24Data[bgrIndex + 0];
                byte g = bgr24Data[bgrIndex + 1];
                byte r = bgr24Data[bgrIndex + 2];

                bgra32Data[bgraIndex + 0] = b;
                bgra32Data[bgraIndex + 1] = g;
                bgra32Data[bgraIndex + 2] = r;
                bgra32Data[bgraIndex + 3] = 255;
            }
        }
    }

    private static unsafe void CopyMemoryWithPitch(byte[] source, uint sourcePitch, byte[] destination, uint destPitch, uint bytesPerRow, uint rowCount)
    {
        if (source is null || destination is null || bytesPerRow == 0 || rowCount == 0)
        {
            return;
        }

        var requiredSourceSize = (ulong)(rowCount - 1) * sourcePitch + bytesPerRow;
        var requiredDestSize = (ulong)(rowCount - 1) * destPitch + bytesPerRow;

        if (bytesPerRow > sourcePitch || bytesPerRow > destPitch)
        {
            Log.Error("CopyMemoryWithPitch: bytesPerRow exceeds pitch.");
            return;
        }

        if (requiredSourceSize > (ulong)source.LongLength || requiredDestSize > (ulong)destination.LongLength)
        {
            Log.Error($"CopyMemoryWithPitch: Calculated size exceeds buffer length. Source Required: " +
                $"{requiredSourceSize}" +
                $"vs Actual: {source.LongLength}." +
                $"Dest Required: {requiredDestSize}" +
                $"vs Actual: {destination.LongLength}");

            return;
        }

        fixed (byte* pSource = source, pDest = destination)
        {
            byte* pSrcRow = pSource;
            byte* pDstRow = pDest;

            for (uint i = 0; i < rowCount; i++)
            {
                Buffer.MemoryCopy(pSrcRow, pDstRow, bytesPerRow, bytesPerRow);

                pSrcRow += sourcePitch;
                pDstRow += destPitch;
            }
        }
    }

    private static void DrawPlaceholder(DrawingContext context, Vector2 position, Vector2 size)
    {
        Rect placeholderRect = new(position.X, position.Y, size.X, size.Y);
        ID2D1SolidColorBrush? brush = context.OwnerWindow?.GetOrCreateBrush(Colors.DarkGray);

        if (brush == null || context.RenderTarget == null)
        {
            return;
        }

        context.RenderTarget.FillRectangle(placeholderRect, brush);
    }

    // - - - - - - - - - - - - - - - Event handlers - - - - - - - - - - - - - - - 

    private void OnPlaying(object? sender, EventArgs e)
    {
        PlaybackStarted?.Invoke(this, EventArgs.Empty);
    }

    private void OnPaused(object? sender, EventArgs e)
    {
        PlaybackPaused?.Invoke(this, EventArgs.Empty);
    }

    private void OnStopped(object? sender, EventArgs e)
    {
        PlaybackStopped?.Invoke(this, EventArgs.Empty);

        lock (_frameLock)
        {
            _newFrameAvailable = false;
        }
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        PlaybackEnded?.Invoke(this, EventArgs.Empty); lock (_frameLock) { _newFrameAvailable = false; }
        if (Loop && _mediaPlayer != null && _media != null && !_isDisposed) { _mediaPlayer.Stop(); _mediaPlayer.Play(); }
    }

    private void OnEncounteredError(object? sender, EventArgs e)
    {
        Log.Error($"LibVLCSharp error for node '{Name}'. State: {_mediaPlayer?.State}");
        PlaybackError?.Invoke(this, "LibVLCSharp error. Check logs.");

        lock (_frameLock)
        {
            _newFrameAvailable = false;
        }
    }

    // Handler for MediaPlayer.PositionChanged
    private void OnPositionChanged(object? sender, MediaPlayerPositionChangedEventArgs e)
    {
        if (SuppressPositionRatioChangedEvent) // Check the flag
        {
            return;
        }

        PositionRatioChanged?.Invoke(e.Position);
    }

    private void ResetFormatState_Locked()
    {
        _videoBitmap?.Dispose(); _videoBitmap = null;
        _latestFrameDataRaw = null; _conversionBufferBGRA32 = null;
        _newFrameAvailable = false; _videoWidth = 0; _videoHeight = 0;
        _receivedChroma = 0; _receivedPitch = 0; _bufferSize = 0;
        _formatConfigured = false; // Mark as unconfigured
    }

    // - - - - - - - - - - - - - - - Diospose - - - - - - - - - - - - - - - 

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        Log.Info($"Disposing VideoPlayer '{Name}' (disposing={disposing})...");

        try
        {
            if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Stop();
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Exception stopping MediaPlayer during dispose: {ex.Message}");
        }

        if (disposing)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Playing -= OnPlaying;
                _mediaPlayer.Paused -= OnPaused;
                _mediaPlayer.Stopped -= OnStopped;
                _mediaPlayer.EndReached -= OnEndReached;
                _mediaPlayer.EncounteredError -= OnEncounteredError;
                _mediaPlayer.PositionChanged -= OnPositionChanged;

                try
                {
                    _mediaPlayer.SetVideoFormatCallbacks(null, null);
                    _mediaPlayer.SetVideoCallbacks(null, null, null);
                }
                catch (Exception ex)
                {
                    Log.Warning($"Exception detaching callbacks: {ex.Message}");
                }
            }

            try
            {
                _media?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning($"Exception disposing Media: {ex.Message}");
            }
            finally
            {
                _media = null;
            }

            try
            {
                _mediaPlayer?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning($"Exception disposing MediaPlayer: {ex.Message}");
            }
            finally
            {
                _mediaPlayer = null;
            }

            try
            {
                _libVLC?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning($"Exception disposing LibVLC: {ex.Message}");
            }
            finally
            {
                _libVLC = null;
            }

            _videoFormatCallbackDelegate = null;
            _videoCleanupCallbackDelegate = null;
            _videoLockCallbackDelegate = null;
            _videoUnlockCallbackDelegate = null;
            _videoDisplayCallbackDelegate = null;
        }

        ID2D1Bitmap? bitmapToDispose = null;

        lock (_frameLock)
        {
            bitmapToDispose = _videoBitmap; _videoBitmap = null;
            _latestFrameDataRaw = null; // Release raw buffer
            _conversionBufferBGRA32 = null; // Release conversion buffer
            _newFrameAvailable = false;
            _videoWidth = 0;
            _videoHeight = 0;
            _receivedChroma = 0;
            _receivedPitch = 0;
            _bufferSize = 0;
        }

        _formatConfigured = false; // Ensure flag is reset
        bitmapToDispose?.Dispose();

        Log.Info($"VideoPlayer '{Name}' disposed.");
    }

    private void DisposeVlcResources()
    {
        Log.Warning($"DisposeVlcResources called directly for '{Name}'. Use Dispose().");
        Dispose(true);
    }

    ~VideoPlayer()
    {
        Dispose(false);
    }
}
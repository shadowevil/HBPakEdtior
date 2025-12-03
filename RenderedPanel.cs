using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace HBPakEditor
{
    public class RenderedPanel : Panel
    {
        private Bitmap? _currentBmp = null;
        private List<SpriteReference> _rectangles = new List<SpriteReference>();
        private SpriteReference? _currentRectangle = null;
        private ToolStripStatusLabel? _zoomStatusLabel;
        private ToolStripStatusLabel? _dimensionsStatusLabel;
        private ToolStripStatusLabel? _rectangleDimensionsStatusLabel;
        private ToolStripStatusLabel? _imageInfoStatusLabel;

        // Zoom and pan state
        private float _zoomLevel = 1.0f;
        private PointF _panOffset = PointF.Empty;
        private bool _isPanning = false;
        private Point _lastMousePosition;
        private bool _isAltHeld = false;
        private bool _isSpaceHeld = false;
        private bool _isCtrlHeld = false;
        private bool _isShiftHeld = false;

        // Rectangle drawing state
        private bool _isDrawingRectangle = false;
        private Point _rectangleStartPoint;
        private Rectangle _drawingRectangle = Rectangle.Empty;

        // Auto-pan state
        private Timer? _autoPanTimer;
        private Point _currentMousePosition;
        private const int AUTO_PAN_SPEED = 5;
        private const int AUTO_PAN_EDGE_THRESHOLD = 20;

        // Pixel grid preview
        private const int PIXEL_GRID_SIZE = 3;
        private const int PIXEL_GRID_CELL_SIZE = 20;
        private const float PIXEL_GRID_ZOOM_THRESHOLD = 3.0f;

        // Hotkey legend fonts and brushes
        private Font _legendFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        private Font _legendFontBold = new Font("Segoe UI", 9f, FontStyle.Bold);
        private SolidBrush _legendBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
        private SolidBrush _legendBrushActive = new SolidBrush(Color.FromArgb(255, 100, 200, 255));
        private SolidBrush _legendBackgroundBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0));

        // Rectangle drawing callback
        public Func<Rectangle, bool>? OnRectangleDrawn { get; set; }

        // Rectangle interaction callback
        public Action<SpriteReference, MouseButtons, Point>? OnRectangleClicked { get; set; }

        public Bitmap? CurrentBitmap
        {
            get => _currentBmp;
            set
            {
                _currentBmp?.Dispose();
                _currentBmp = value;
                ResetViewTransform();
                Invalidate();
            }
        }

        public float ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                _zoomLevel = Math.Max(0.1f, Math.Min(10.0f, value));
                UpdateZoomLabel();
                Invalidate();
            }
        }

        public PointF PanOffset
        {
            get => _panOffset;
            set
            {
                _panOffset = value;
                Invalidate();
            }
        }

        public List<SpriteReference> Rectangles
        {
            get => _rectangles;
            set
            {
                _rectangles = value;
                Invalidate();
            }
        }

        public SpriteReference? CurrentRectangle
        {
            get => _currentRectangle;
            set
            {
                _currentRectangle = value;
                Invalidate();
            }
        }

        public ToolStripStatusLabel? ZoomStatusLabel
        {
            get => _zoomStatusLabel;
            set => _zoomStatusLabel = value;
        }

        public ToolStripStatusLabel? DimensionsStatusLabel
        {
            get => _dimensionsStatusLabel;
            set => _dimensionsStatusLabel = value;
        }

        public ToolStripStatusLabel? RectangleDimensionsStatusLabel
        {
            get => _rectangleDimensionsStatusLabel;
            set => _rectangleDimensionsStatusLabel = value;
        }

        public ToolStripStatusLabel? ImageInfoStatusLabel
        {
            get => _imageInfoStatusLabel;
            set => _imageInfoStatusLabel = value;
        }

        public RenderedPanel()
        {
            DoubleBuffered = true;
            TabStop = true;

            _autoPanTimer = new Timer();
            _autoPanTimer.Interval = 16; // ~60 FPS
            _autoPanTimer.Tick += OnAutoPanTick;

            this.LostFocus += (s, e) =>
            {
                if (_isAltHeld)
                {
                    _isAltHeld = false;
                    UpdateCursor();
                    Invalidate();
                }
                if (_isCtrlHeld)
                {
                    _isCtrlHeld = false;
                    Invalidate();
                }
                if (_isShiftHeld)
                {
                    _isShiftHeld = false;
                    StopAutoPan();
                    UpdateCursor();
                    Invalidate();
                }
                if (_isSpaceHeld)
                {
                    _isSpaceHeld = false;
                    _isPanning = false;
                    UpdateCursor();
                    Invalidate();
                }
            };
        }

        // In RenderedPanel.cs, update WndProc:

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSKEYDOWN = 0x104;
            const int WM_SYSKEYUP = 0x105;

            // Intercept Alt key system messages to prevent menu activation
            if (m.Msg == WM_SYSKEYDOWN || m.Msg == WM_SYSKEYUP)
            {
                Keys key = (Keys)m.WParam.ToInt32();
                if (key == Keys.Menu) // Alt key
                {
                    // Handle it as a regular key instead
                    if (m.Msg == WM_SYSKEYDOWN)
                    {
                        KeyEventArgs e = new KeyEventArgs(Keys.Alt);
                        OnKeyDown(e);
                    }
                    else if (m.Msg == WM_SYSKEYUP)
                    {
                        KeyEventArgs e = new KeyEventArgs(Keys.Alt);
                        OnKeyUp(e);
                    }
                    return; // Don't pass to base, preventing menu activation
                }
            }

            base.WndProc(ref m);
        }

        private void FitImageToPanel(float padding = 0.85f)
        {
            if (_currentBmp == null)
                return;

            float scaleX = (float)ClientSize.Width / _currentBmp.Width;
            float scaleY = (float)ClientSize.Height / _currentBmp.Height;

            _zoomLevel = Math.Min(scaleX, scaleY) * padding;
            _zoomLevel = Math.Max(0.1f, Math.Min(10.0f, _zoomLevel));
            _panOffset = PointF.Empty;
        }

        private void ResetViewTransform()
        {
            FitImageToPanel();
            UpdateZoomLabel();
        }

        private void UpdateZoomLabel()
        {
            if (_zoomStatusLabel != null)
                _zoomStatusLabel.Text = $"{(int)(_zoomLevel * 100)}%";
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        private void UpdateCursor()
        {
            if (_isShiftHeld && !_isDrawingRectangle)
            {
                Cursor = Cursors.Cross;
            }
            else if (_isAltHeld)
            {
                Cursor = Cursors.Cross;
            }
            else if (_isSpaceHeld)
            {
                Cursor = _isPanning ? Cursors.SizeAll : Cursors.Hand;
            }
            else
            {
                Cursor = Cursors.Default;
            }
        }

        private PointF ScreenToImageSpace(Point screenPoint)
        {
            if (_currentBmp == null)
                return PointF.Empty;

            float centerX = ClientSize.Width / 2f;
            float centerY = ClientSize.Height / 2f;

            float width = _currentBmp.Width * _zoomLevel;
            float height = _currentBmp.Height * _zoomLevel;

            float imageX = centerX - (width / 2f) + _panOffset.X;
            float imageY = centerY - (height / 2f) + _panOffset.Y;

            // Convert screen coordinates to image space coordinates
            float imageSpaceX = (screenPoint.X - imageX) / _zoomLevel;
            float imageSpaceY = (screenPoint.Y - imageY) / _zoomLevel;

            return new PointF(imageSpaceX, imageSpaceY);
        }

        private Point ClampToImageBounds(PointF imagePoint)
        {
            if (_currentBmp == null)
                return Point.Empty;

            int x = Math.Max(0, Math.Min(_currentBmp.Width, (int)Math.Round(imagePoint.X)));
            int y = Math.Max(0, Math.Min(_currentBmp.Height, (int)Math.Round(imagePoint.Y)));
            return new Point(x, y);
        }

        private Rectangle GetRectangleBounds(SpriteReference spriteRef, PAKLib.PAK? pak)
        {
            if (pak?.Data != null &&
                spriteRef.SpriteIndex >= 0 &&
                spriteRef.SpriteIndex < pak.Data.Sprites.Count &&
                spriteRef.RectangleIndex >= 0)
            {
                var sprite = pak.Data.Sprites[spriteRef.SpriteIndex];
                if (spriteRef.RectangleIndex < sprite.Rectangles.Count)
                {
                    var rect = sprite.Rectangles[spriteRef.RectangleIndex];
                    return new Rectangle(rect.x, rect.y, rect.width, rect.height);
                }
            }
            return Rectangle.Empty;
        }

        private SpriteReference? GetRectangleAtPoint(Point imageSpacePoint, PAKLib.PAK? pak)
        {
            // Check rectangles in reverse order (top to bottom in drawing order)
            for (int i = _rectangles.Count - 1; i >= 0; i--)
            {
                Rectangle bounds = GetRectangleBounds(_rectangles[i], pak);
                if (bounds.Contains(imageSpacePoint))
                {
                    return _rectangles[i];
                }
            }
            return null;
        }

        private void StartAutoPan()
        {
            if (_autoPanTimer != null && !_autoPanTimer.Enabled)
            {
                _autoPanTimer.Start();
            }
        }

        private void StopAutoPan()
        {
            if (_autoPanTimer != null && _autoPanTimer.Enabled)
            {
                _autoPanTimer.Stop();
            }
        }

        private void OnAutoPanTick(object? sender, EventArgs e)
        {
            if (!_isDrawingRectangle)
            {
                StopAutoPan();
                return;
            }

            // Get client point from screen coordinates
            Point clientPoint = PointToClient(Cursor.Position);

            float panX = 0;
            float panY = 0;

            // Check if cursor is near edges
            if (clientPoint.X < AUTO_PAN_EDGE_THRESHOLD)
            {
                panX = AUTO_PAN_SPEED;
            }
            else if (clientPoint.X > ClientSize.Width - AUTO_PAN_EDGE_THRESHOLD)
            {
                panX = -AUTO_PAN_SPEED;
            }

            if (clientPoint.Y < AUTO_PAN_EDGE_THRESHOLD)
            {
                panY = AUTO_PAN_SPEED;
            }
            else if (clientPoint.Y > ClientSize.Height - AUTO_PAN_EDGE_THRESHOLD)
            {
                panY = -AUTO_PAN_SPEED;
            }

            if (panX != 0 || panY != 0)
            {
                _panOffset.X += panX;
                _panOffset.Y += panY;

                // Update the drawing rectangle based on current cursor position
                PointF imagePoint = ScreenToImageSpace(clientPoint);
                Point currentPoint = ClampToImageBounds(imagePoint);

                int x = Math.Min(_rectangleStartPoint.X, currentPoint.X);
                int y = Math.Min(_rectangleStartPoint.Y, currentPoint.Y);
                int width = Math.Abs(currentPoint.X - _rectangleStartPoint.X);
                int height = Math.Abs(currentPoint.Y - _rectangleStartPoint.Y);

                _drawingRectangle = new Rectangle(x, y, width, height);
                Invalidate();
            }
            else
            {
                StopAutoPan();
            }
        }

        // Public property to get PAK data for rectangle hit testing
        public PAKLib.PAK? PAKData { get; set; }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (_currentBmp == null || !_isAltHeld)
                return;

            float oldZoom = _zoomLevel;

            // Update zoom level
            float zoomDelta = e.Delta > 0 ? 1.1f : 0.9f;
            _zoomLevel = Math.Max(0.1f, Math.Min(10.0f, _zoomLevel * zoomDelta));

            if (_isCtrlHeld)
            {
                // Zoom from center of image - no pan adjustment needed
                // The pan offset stays the same, zoom happens naturally from center
            }
            else
            {
                // Zoom from mouse position
                // Get mouse position relative to panel
                PointF mousePos = e.Location;

                // Calculate center position
                float centerX = ClientSize.Width / 2f;
                float centerY = ClientSize.Height / 2f;

                // Calculate old image position
                float oldWidth = _currentBmp.Width * oldZoom;
                float oldHeight = _currentBmp.Height * oldZoom;
                float oldX = centerX - (oldWidth / 2f) + _panOffset.X;
                float oldY = centerY - (oldHeight / 2f) + _panOffset.Y;

                // Calculate mouse position in image space (0..1 normalized)
                float mouseInImageX = (mousePos.X - oldX) / oldWidth;
                float mouseInImageY = (mousePos.Y - oldY) / oldHeight;

                // Calculate new image position
                float newWidth = _currentBmp.Width * _zoomLevel;
                float newHeight = _currentBmp.Height * _zoomLevel;

                // Calculate where we want the mouse to be after zoom
                float desiredX = mousePos.X - (mouseInImageX * newWidth);
                float desiredY = mousePos.Y - (mouseInImageY * newHeight);

                // Calculate required pan offset
                _panOffset.X = desiredX - (centerX - (newWidth / 2f));
                _panOffset.Y = desiredY - (centerY - (newHeight / 2f));
            }

            UpdateZoomLabel();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            // Check for rectangle click first (only when no modifier keys are held)
            if ((e.Button == MouseButtons.Left || e.Button == MouseButtons.Right) &&
                !_isShiftHeld && !_isSpaceHeld && !_isAltHeld && !_isCtrlHeld &&
                _currentBmp != null)
            {
                PointF imagePoint = ScreenToImageSpace(e.Location);
                Point imageSpacePoint = new Point((int)Math.Floor(imagePoint.X), (int)Math.Floor(imagePoint.Y));

                SpriteReference? rectangleRef = GetRectangleAtPoint(imageSpacePoint, PAKData);

                if (rectangleRef.HasValue)
                {
                    OnRectangleClicked?.Invoke(rectangleRef.Value, e.Button, imageSpacePoint);
                    return; // Don't process other mouse actions
                }
            }

            if (e.Button == MouseButtons.Left && _isShiftHeld && _currentBmp != null)
            {
                // Start drawing rectangle
                PointF imagePoint = ScreenToImageSpace(e.Location);
                _rectangleStartPoint = ClampToImageBounds(imagePoint);
                _isDrawingRectangle = true;
                _drawingRectangle = new Rectangle(_rectangleStartPoint.X, _rectangleStartPoint.Y, 0, 0);
                _currentMousePosition = e.Location;
                Invalidate();
            }
            else if (e.Button == MouseButtons.Left && _isSpaceHeld)
            {
                _isPanning = true;
                _lastMousePosition = e.Location;
                UpdateCursor();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            _currentMousePosition = e.Location;

            // Check actual key states to resync if needed
            bool altActuallyPressed = (Control.ModifierKeys & Keys.Alt) == Keys.Alt;
            if (_isAltHeld != altActuallyPressed)
            {
                _isAltHeld = altActuallyPressed;
                UpdateCursor();
                Invalidate();
            }

            bool ctrlActuallyPressed = (Control.ModifierKeys & Keys.Control) == Keys.Control;
            if (_isCtrlHeld != ctrlActuallyPressed)
            {
                _isCtrlHeld = ctrlActuallyPressed;
                Invalidate();
            }

            bool shiftActuallyPressed = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;
            if (_isShiftHeld != shiftActuallyPressed)
            {
                _isShiftHeld = shiftActuallyPressed;
                if (!_isShiftHeld)
                    StopAutoPan();
                UpdateCursor();
                Invalidate();
            }

            if (_isDrawingRectangle)
            {
                // Check if we need to start auto-panning
                bool needsAutoPan = e.X < AUTO_PAN_EDGE_THRESHOLD ||
                                   e.X > ClientSize.Width - AUTO_PAN_EDGE_THRESHOLD ||
                                   e.Y < AUTO_PAN_EDGE_THRESHOLD ||
                                   e.Y > ClientSize.Height - AUTO_PAN_EDGE_THRESHOLD;

                if (needsAutoPan)
                {
                    StartAutoPan();
                }
                else
                {
                    StopAutoPan();
                }

                PointF imagePoint = ScreenToImageSpace(e.Location);
                Point currentPoint = ClampToImageBounds(imagePoint);

                // Calculate rectangle in image space
                int x = Math.Min(_rectangleStartPoint.X, currentPoint.X);
                int y = Math.Min(_rectangleStartPoint.Y, currentPoint.Y);
                int width = Math.Abs(currentPoint.X - _rectangleStartPoint.X);
                int height = Math.Abs(currentPoint.Y - _rectangleStartPoint.Y);

                _drawingRectangle = new Rectangle(x, y, width, height);
                Invalidate();
            }
            else if (_isPanning)
            {
                int deltaX = e.X - _lastMousePosition.X;
                int deltaY = e.Y - _lastMousePosition.Y;

                _panOffset.X += deltaX;
                _panOffset.Y += deltaY;

                _lastMousePosition = e.Location;
                Invalidate();
            }
            else if (_isShiftHeld && _zoomLevel < PIXEL_GRID_ZOOM_THRESHOLD)
            {
                // Redraw to update pixel grid
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Left && _isDrawingRectangle)
            {
                _isDrawingRectangle = false;
                StopAutoPan();

                // Only process if the rectangle has size
                if (_drawingRectangle.Width > 0 && _drawingRectangle.Height > 0)
                {
                    // Call the callback function
                    bool keepRectangle = OnRectangleDrawn?.Invoke(_drawingRectangle) ?? false;

                    if (!keepRectangle)
                    {
                        _drawingRectangle = Rectangle.Empty;
                    }
                }
                else
                {
                    _drawingRectangle = Rectangle.Empty;
                }

                Invalidate();
            }
            else if (e.Button == MouseButtons.Left)
            {
                _isPanning = false;
                UpdateCursor();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            bool needsRedraw = false;

            if (e.Shift && !_isShiftHeld)
            {
                _isShiftHeld = true;
                UpdateCursor();
                needsRedraw = true;
            }

            if (e.Alt && !_isAltHeld)
            {
                _isAltHeld = true;
                UpdateCursor();
                needsRedraw = true;
            }

            if (e.Control && !_isCtrlHeld)
            {
                _isCtrlHeld = true;
                needsRedraw = true;
            }

            if (e.KeyCode == Keys.Space && !_isSpaceHeld)
            {
                _isSpaceHeld = true;
                UpdateCursor();
                needsRedraw = true;
            }

            if (needsRedraw)
                Invalidate();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            bool needsRedraw = false;

            if (!e.Shift && _isShiftHeld)
            {
                _isShiftHeld = false;
                StopAutoPan();
                UpdateCursor();
                needsRedraw = true;
            }

            if (!e.Alt && _isAltHeld)
            {
                _isAltHeld = false;
                UpdateCursor();
                needsRedraw = true;
            }

            if (!e.Control && _isCtrlHeld)
            {
                _isCtrlHeld = false;
                needsRedraw = true;
            }

            if (e.KeyCode == Keys.Space && _isSpaceHeld)
            {
                _isSpaceHeld = false;
                _isPanning = false;
                UpdateCursor();
                needsRedraw = true;
            }

            if (needsRedraw)
                Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isPanning = false;
            UpdateCursor();

            // Keep auto-pan active if we're drawing a rectangle
            if (!_isDrawingRectangle)
            {
                StopAutoPan();
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Space || keyData == Keys.Alt || (keyData & Keys.Alt) == Keys.Alt)
                return true;
            return base.IsInputKey(keyData);
        }

        private int GetStatusBarHeight()
        {
            // Find the StatusStrip in the controls
            foreach (Control control in Controls)
            {
                if (control is StatusStrip statusStrip)
                {
                    return statusStrip.Height;
                }
            }
            return 0;
        }

        private void DrawPixelGrid(Graphics g)
        {
            if (_currentBmp == null || !_isShiftHeld || _zoomLevel >= PIXEL_GRID_ZOOM_THRESHOLD)
                return;

            PointF imagePoint = ScreenToImageSpace(_currentMousePosition);
            int centerPixelX = (int)Math.Floor(imagePoint.X);
            int centerPixelY = (int)Math.Floor(imagePoint.Y);

            // Check if mouse is within image bounds
            if (centerPixelX < 0 || centerPixelX >= _currentBmp.Width ||
                centerPixelY < 0 || centerPixelY >= _currentBmp.Height)
                return;

            int gridSize = PIXEL_GRID_SIZE * PIXEL_GRID_CELL_SIZE;
            int gridX = _currentMousePosition.X + 20; // Offset from cursor
            int gridY = _currentMousePosition.Y + 20;

            // Keep grid on screen
            if (gridX + gridSize > ClientSize.Width)
                gridX = _currentMousePosition.X - gridSize - 20;
            if (gridY + gridSize > ClientSize.Height)
                gridY = _currentMousePosition.Y - gridSize - 20;

            // Draw background
            g.FillRectangle(Brushes.Black, gridX, gridY, gridSize, gridSize);

            // Draw pixels
            for (int dy = 0; dy < PIXEL_GRID_SIZE; dy++)
            {
                for (int dx = 0; dx < PIXEL_GRID_SIZE; dx++)
                {
                    int pixelX = centerPixelX - 1 + dx;
                    int pixelY = centerPixelY - 1 + dy;

                    if (pixelX >= 0 && pixelX < _currentBmp.Width &&
                        pixelY >= 0 && pixelY < _currentBmp.Height)
                    {
                        Color pixelColor = _currentBmp.GetPixel(pixelX, pixelY);
                        using (SolidBrush brush = new SolidBrush(pixelColor))
                        {
                            g.FillRectangle(brush,
                                gridX + dx * PIXEL_GRID_CELL_SIZE,
                                gridY + dy * PIXEL_GRID_CELL_SIZE,
                                PIXEL_GRID_CELL_SIZE,
                                PIXEL_GRID_CELL_SIZE);
                        }
                    }
                }
            }

            // Draw grid lines
            using (Pen gridPen = new Pen(Color.Gray, 1))
            {
                for (int i = 0; i <= PIXEL_GRID_SIZE; i++)
                {
                    // Vertical lines
                    g.DrawLine(gridPen,
                        gridX + i * PIXEL_GRID_CELL_SIZE, gridY,
                        gridX + i * PIXEL_GRID_CELL_SIZE, gridY + gridSize);
                    // Horizontal lines
                    g.DrawLine(gridPen,
                        gridX, gridY + i * PIXEL_GRID_CELL_SIZE,
                        gridX + gridSize, gridY + i * PIXEL_GRID_CELL_SIZE);
                }
            }

            // Highlight center pixel
            using (Pen highlightPen = new Pen(Color.White, 2))
            {
                g.DrawRectangle(highlightPen,
                    gridX + PIXEL_GRID_CELL_SIZE,
                    gridY + PIXEL_GRID_CELL_SIZE,
                    PIXEL_GRID_CELL_SIZE,
                    PIXEL_GRID_CELL_SIZE);
            }

            // Draw border around entire grid
            using (Pen borderPen = new Pen(Color.White, 2))
            {
                g.DrawRectangle(borderPen, gridX, gridY, gridSize, gridSize);
            }
        }

        private void DrawHotkeyLegend(Graphics g)
        {
            string[] lines = new[]
            {
                "Alt + Scroll: Zoom at cursor",
                "Ctrl + Alt + Scroll: Zoom at center",
                "Space + Drag: Pan",
                "Shift + Drag: Draw rectangle"
            };

            float padding = 10f;
            float lineHeight = 20f;
            float maxWidth = 0f;

            // Measure text to get max width
            foreach (var line in lines)
            {
                SizeF size = g.MeasureString(line, _legendFont);
                if (size.Width > maxWidth)
                    maxWidth = size.Width;
            }

            float boxWidth = maxWidth + (padding * 2);
            float boxHeight = (lineHeight * lines.Length) + (padding * 2);
            float boxX = ClientSize.Width - boxWidth - 10;

            // Offset by status bar height
            int statusBarHeight = GetStatusBarHeight();
            float boxY = ClientSize.Height - boxHeight - 10 - statusBarHeight;

            // Draw background
            g.FillRectangle(_legendBackgroundBrush, boxX, boxY, boxWidth, boxHeight);

            // Draw legend text
            float textY = boxY + padding;

            // Line 1: Alt + Scroll
            DrawLegendLine(g, "Alt", _isAltHeld, boxX + padding, textY);
            DrawLegendLine(g, " + Scroll: Zoom at cursor", false, boxX + padding + 25, textY, false);
            textY += lineHeight;

            // Line 2: Ctrl + Alt + Scroll
            DrawLegendLine(g, "Ctrl", _isCtrlHeld, boxX + padding, textY);
            DrawLegendLine(g, " + ", false, boxX + padding + 30, textY, false);
            DrawLegendLine(g, "Alt", _isAltHeld, boxX + padding + 50, textY);
            DrawLegendLine(g, " + Scroll: Zoom at center", false, boxX + padding + 75, textY, false);
            textY += lineHeight;

            // Line 3: Space + Drag
            DrawLegendLine(g, "Space", _isSpaceHeld, boxX + padding, textY);
            DrawLegendLine(g, " + Drag: Pan", false, boxX + padding + 45, textY, false);
            textY += lineHeight;

            // Line 4: Shift + Drag
            DrawLegendLine(g, "Shift", _isShiftHeld, boxX + padding, textY);
            DrawLegendLine(g, " + Drag: Draw rectangle", false, boxX + padding + 40, textY, false);
        }

        private void DrawLegendLine(Graphics g, string text, bool isActive, float x, float y, bool useKeyFont = true)
        {
            Font font = (isActive && useKeyFont) ? _legendFontBold : _legendFont;
            Brush brush = (isActive && useKeyFont) ? _legendBrushActive : _legendBrush;
            g.DrawString(text, font, brush, x, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_currentBmp == null)
            {
                e.Graphics.Clear(BackColor);
                DrawHotkeyLegend(e.Graphics);
                return;
            }

            e.Graphics.Clear(BackColor);
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

            // Calculate center position
            float centerX = ClientSize.Width / 2f;
            float centerY = ClientSize.Height / 2f;

            // Calculate image size with zoom
            float width = _currentBmp.Width * _zoomLevel;
            float height = _currentBmp.Height * _zoomLevel;

            // Calculate position with pan offset
            float x = centerX - (width / 2f) + _panOffset.X;
            float y = centerY - (height / 2f) + _panOffset.Y;

            e.Graphics.DrawImage(_currentBmp, x, y, width, height);

            // Draw rectangles
            if (_rectangles.Count > 0 && PAKData?.Data != null)
            {
                foreach (var spriteRef in _rectangles)
                {
                    Rectangle bounds = GetRectangleBounds(spriteRef, PAKData);
                    if (bounds != Rectangle.Empty)
                    {
                        e.Graphics.DrawRectangle(Pens.Red,
                            bounds.X * _zoomLevel + x,
                            bounds.Y * _zoomLevel + y,
                            bounds.Width * _zoomLevel,
                            bounds.Height * _zoomLevel);
                    }
                }
            }

            if (_currentRectangle.HasValue && PAKData?.Data != null)
            {
                Rectangle bounds = GetRectangleBounds(_currentRectangle.Value, PAKData);
                if (bounds != Rectangle.Empty)
                {
                    e.Graphics.DrawRectangle(Pens.Lime,
                        bounds.X * _zoomLevel + x,
                        bounds.Y * _zoomLevel + y,
                        bounds.Width * _zoomLevel,
                        bounds.Height * _zoomLevel);
                }
            }

            // Draw the rectangle being drawn
            if (_isDrawingRectangle && _drawingRectangle != Rectangle.Empty)
            {
                using (Pen drawPen = new Pen(Color.Yellow, 2))
                {
                    e.Graphics.DrawRectangle(drawPen,
                        _drawingRectangle.X * _zoomLevel + x,
                        _drawingRectangle.Y * _zoomLevel + y,
                        _drawingRectangle.Width * _zoomLevel,
                        _drawingRectangle.Height * _zoomLevel
                    );
                }
            }

            // Draw pixel grid preview
            DrawPixelGrid(e.Graphics);

            // Draw hotkey legend on top
            DrawHotkeyLegend(e.Graphics);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoPanTimer?.Stop();
                _autoPanTimer?.Dispose();
                _currentBmp?.Dispose();
                _legendFont?.Dispose();
                _legendFontBold?.Dispose();
                _legendBrush?.Dispose();
                _legendBrushActive?.Dispose();
                _legendBackgroundBrush?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
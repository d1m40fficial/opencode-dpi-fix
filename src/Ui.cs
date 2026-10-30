using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpencodeDpiFix
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(14, 16, 21);
        public static readonly Color Surface = Color.FromArgb(22, 26, 34);
        public static readonly Color SurfaceHover = Color.FromArgb(31, 36, 47);
        public static readonly Color SurfacePressed = Color.FromArgb(26, 30, 40);
        public static readonly Color Border = Color.FromArgb(38, 44, 58);
        public static readonly Color Text = Color.FromArgb(232, 236, 244);
        public static readonly Color Muted = Color.FromArgb(138, 147, 166);
        public static readonly Color Faint = Color.FromArgb(96, 104, 122);

        public static readonly Color Accent = Color.FromArgb(59, 130, 246);
        public static readonly Color AccentHover = Color.FromArgb(80, 147, 255);
        public static readonly Color AccentPressed = Color.FromArgb(44, 106, 212);

        public static readonly Color OkBack = Color.FromArgb(18, 38, 27);
        public static readonly Color OkText = Color.FromArgb(74, 222, 128);
        public static readonly Color OkDot = Color.FromArgb(34, 197, 94);

        public static readonly Color WarnBack = Color.FromArgb(42, 33, 19);
        public static readonly Color WarnText = Color.FromArgb(251, 191, 36);
        public static readonly Color WarnDot = Color.FromArgb(245, 158, 11);

        public static readonly Color LogBack = Color.FromArgb(10, 12, 17);
        public static readonly Color LogText = Color.FromArgb(196, 203, 216);

        public static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            int d = radius * 2;
            if (d > bounds.Width) d = bounds.Width;
            if (d > bounds.Height) d = bounds.Height;

            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.X + bounds.Width - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.X + bounds.Width - d, bounds.Y + bounds.Height - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Y + bounds.Height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>Плоская карточка со скруглёнными углами и тонкой рамкой.</summary>
    internal class Card : Panel
    {
        public int Radius = 10;
        public Color Fill = Theme.Surface;
        public Color BorderColor = Color.Transparent;

        public Card()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Background);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Rounded(rect, Radius))
            {
                using (SolidBrush brush = new SolidBrush(Fill))
                    g.FillPath(brush, path);

                if (BorderColor.A > 0)
                {
                    using (Pen pen = new Pen(BorderColor))
                        g.DrawPath(pen, path);
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // фон рисуется в OnPaint
        }
    }

    /// <summary>Кнопка со скруглёнными углами и собственными состояниями.</summary>
    internal sealed class FlatButton : Button    {
        public int Radius = 8;
        public Color NormalBack = Theme.Surface;
        public Color HoverBack = Theme.SurfaceHover;
        public Color PressedBack = Theme.SurfacePressed;
        public Color BorderColor = Theme.Border;

        private bool _hover;
        private bool _pressed;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Background);

            Color back;
            if (!Enabled) back = Theme.SurfacePressed;
            else if (_pressed) back = PressedBack;
            else if (_hover) back = HoverBack;
            else back = NormalBack;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Rounded(rect, Radius))
            {
                using (SolidBrush brush = new SolidBrush(back))
                    g.FillPath(brush, path);

                if (BorderColor.A > 0)
                {
                    using (Pen pen = new Pen(BorderColor))
                        g.DrawPath(pen, path);
                }
            }

            Color fore = Enabled ? ForeColor : Theme.Faint;
            TextRenderer.DrawText(g, Text, Font, rect, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Галочка в тёмной теме: свой квадрат вместо системного.</summary>
    internal sealed class CheckBoxDark : Control
    {
        private bool _checked;

        public CheckBoxDark()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            SetStyle(ControlStyles.StandardClick, true);
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            _checked = !_checked;
            Invalidate();
            base.OnClick(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Space) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyData == Keys.Space)
            {
                _checked = !_checked;
                Invalidate();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int boxSize = 18;
            Rectangle box = new Rectangle(1, (Height - boxSize) / 2, boxSize, boxSize);

            using (GraphicsPath path = Theme.Rounded(box, 5))
            {
                if (_checked)
                {
                    using (SolidBrush brush = new SolidBrush(Theme.Accent))
                        g.FillPath(brush, path);

                    using (Pen pen = new Pen(Color.White, 1.9f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        pen.LineJoin = LineJoin.Round;
                        g.DrawLines(pen, new PointF[]
                        {
                            new PointF(box.X + 5.0f, box.Y + 9.0f),
                            new PointF(box.X + 8.0f, box.Y + 12.0f),
                            new PointF(box.X + 13.0f, box.Y + 6.2f)
                        });
                    }
                }
                else
                {
                    using (SolidBrush brush = new SolidBrush(Theme.LogBack))
                        g.FillPath(brush, path);

                    using (Pen pen = new Pen(Focused ? Theme.Muted : Theme.Border))
                        g.DrawPath(pen, path);
                }
            }

            Rectangle textRect = new Rectangle(boxSize + 10, 0, Width - boxSize - 10, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}

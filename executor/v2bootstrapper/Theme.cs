using System.Drawing;

namespace v2bootstrapper
{
    static class Theme
    {
        public static bool IsDark = true;
        public static Color Background => IsDark ? Color.FromArgb(15, 15, 20) : Color.FromArgb(242, 242, 248);
        public static Color Surface => IsDark ? Color.FromArgb(24, 24, 32) : Color.FromArgb(255, 255, 255);
        public static Color SurfaceAlt => IsDark ? Color.FromArgb(34, 34, 46) : Color.FromArgb(230, 230, 240);
        public static Color Accent => Color.FromArgb(99, 102, 241);
        public static Color AccentHover => Color.FromArgb(79, 82, 221);
        public static Color Success => Color.FromArgb(34, 197, 94);
        public static Color SuccessHover => Color.FromArgb(22, 163, 74);
        public static Color Danger => Color.FromArgb(220, 60, 80);
        public static Color DangerHover => Color.FromArgb(180, 40, 60);
        public static Color TextPrimary => IsDark ? Color.FromArgb(235, 235, 255) : Color.FromArgb(20, 20, 35);
        public static Color TextMuted => IsDark ? Color.FromArgb(100, 100, 130) : Color.FromArgb(140, 140, 170);
        public static Color Border => IsDark ? Color.FromArgb(45, 45, 65) : Color.FromArgb(205, 205, 222);
        public static Color EditorBg => IsDark ? Color.FromArgb(20, 20, 28) : Color.FromArgb(252, 252, 255);
    }
}

using Android.Content;
using Android.Graphics;
using XPlatUtils;

namespace Toggl.Joey.UI.Utils
{
    public sealed class Font
    {
        private readonly string path;
        private readonly TypefaceStyle style;
        private Typeface typeface;

        private Font (string path, TypefaceStyle style)
        {
            this.path = path;
            this.style = style;
        }

        public string Path
        {
            get { return path; }
        }

        public Typeface Typeface
        {
            get {
                if (typeface == null) {
                    var ctx = ServiceContainer.Resolve<Context> ();
                    typeface = Typeface.CreateFromAsset (ctx.Assets, Path);
                }
                return typeface;
            }
        }

        public TypefaceStyle Style
        {
            get { return style; }
        }

        public static readonly Font Roboto = new Font ("fonts/Roboto-Regular.ttf", TypefaceStyle.Normal);
        public static readonly Font RobotoLight = new Font ("fonts/Roboto-Light.ttf", TypefaceStyle.Normal);
        public static readonly Font RobotoMedium = new Font ("fonts/Roboto-Medium.ttf", TypefaceStyle.Normal);
        public static readonly Font DINMedium = new Font ("fonts/DIN-Medium.ttf", TypefaceStyle.Normal);
    }
}


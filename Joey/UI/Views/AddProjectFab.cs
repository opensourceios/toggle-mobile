using System;
using Android.Animation;
using Android.Content;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Util;
using Android.Views.Animations;

namespace Toggl.Joey.UI.Views
{
    public class AddProjectFab : FloatingActionButton
    {
        private Drawable createNewDraw;

        private ColorStateList backgroundTintNew;

        public AddProjectFab (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
        {
        }

        public AddProjectFab (Context context) : this (context, null)
        {
            Initialize (context, null);
        }

        public AddProjectFab (Context context, IAttributeSet attrs) : base (context, attrs)
        {
            Initialize (context, attrs);
        }

        public AddProjectFab (Context context, IAttributeSet attrs, int defStyle) : base (context, attrs, defStyle)
        {
            Initialize (context, attrs);
        }

        private void Initialize (Context context, IAttributeSet attrs)
        {
            createNewDraw = context.Resources.GetDrawable (Resource.Drawable.IcAdd);


            var states = new int[][] { new int[]{ } };
            var createNewColor = new int[] {context.Resources.GetColor (Resource.Color.material_green)};

            backgroundTintNew = new ColorStateList (states, createNewColor);

            BackgroundTintList = backgroundTintNew;
            SetImageDrawable (createNewDraw);
        }
    }
}


using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Toggl.Joey.UI.Activities
{
    [Activity (
         ScreenOrientation = ScreenOrientation.Portrait,
         Name = "toggl.joey.ui.activities.SplashActivity",
         Label = "@string/EntryName",
         Theme = "@style/Theme.Toggl.Splash",
         MainLauncher = true,
         NoHistory = true)]
    public class SplashActivity : Activity
    {
        protected override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            StartActivity (typeof (MainDrawerActivity));
        }
    }
}


using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Net;
using XPlatUtils;
using Fragment = Android.Support.V4.App.Fragment;
using FragmentManager = Android.Support.V4.App.FragmentManager;

namespace Toggl.Joey.UI.Fragments
{
    public class CreateUserFragment : Fragment
    {
        private EditText emailEditText;
        private EditText passwordEditText;
        private Button submitButton;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.CreateUserFragment, container, false);
            emailEditText = view.FindViewById<EditText> (Resource.Id.CreateUserEmailEditText);
            passwordEditText = view.FindViewById<EditText> (Resource.Id.FeedbackNeutralButton);

            submitButton = view.FindViewById<Button> (Resource.Id.CreateUserButton).SetFont (Font.Roboto);
            submitButton.Click += OnRegisterClick;

            return view;
        }

        public override void OnCreate (Bundle state)
        {
            base.OnCreate (state);
            RetainInstance = true;
        }

        public override void OnStart ()
        {
            base.OnStart ();
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "RegisterUser";
        }

        private async void OnRegisterClick (object sender, EventArgs e)
        {
        }
    }
}

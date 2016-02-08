using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Activities;
using Toggl.Joey.UI.Adapters;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Net;
using XPlatUtils;
using Fragment = Android.Support.V4.App.Fragment;
using FragmentManager = Android.Support.V4.App.FragmentManager;

namespace Toggl.Joey.UI.Fragments
{
    public class FeedbackFragment : Fragment
    {
        private ImageButton feedbackPositiveButton;
        private ImageButton feedbackNeutralButton;
        private ImageButton feedbackNegativeButton;
        private Button submitFeedbackButton;
        private EditText feedbackMessageEditText;
        private LinearLayout feedbackContainer;
        private LinearLayout disclaimerContainer;
        private Button noUserRegisterButton;
        private int userRating;
        private String userMessage;
        private bool isSendingFeedback;
        private static readonly int ratingNotSet = 0;
        private static readonly int ratingPositive = 1;
        private static readonly int ratingNeutral = 2;
        private static readonly int ratingNegative = 3;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.FeedbackFragment, container, false);
            feedbackPositiveButton = view.FindViewById<ImageButton> (Resource.Id.FeedbackPositiveButton);
            feedbackNeutralButton = view.FindViewById<ImageButton> (Resource.Id.FeedbackNeutralButton);
            feedbackNegativeButton = view.FindViewById<ImageButton> (Resource.Id.FeedbackNegativeButton);

            feedbackPositiveButton.Click += (sender, e) => SetRating (ratingPositive);
            feedbackNeutralButton.Click += (sender, e) => SetRating (ratingNeutral);
            feedbackNegativeButton.Click += (sender, e) => SetRating (ratingNegative);

            feedbackMessageEditText = view.FindViewById<EditText> (Resource.Id.FeedbackMessageText).SetFont (Font.Roboto);
            feedbackMessageEditText.AfterTextChanged += OnEdit;

            submitFeedbackButton = view.FindViewById<Button> (Resource.Id.SendFeedbackButton).SetFont (Font.Roboto);
            submitFeedbackButton.Click += OnSendClick;

            feedbackContainer = view.FindViewById<LinearLayout> (Resource.Id.FeedbackContainer);
            disclaimerContainer = view.FindViewById<LinearLayout> (Resource.Id.FeedbackDisclaimer);
            noUserRegisterButton = view.FindViewById<Button> (Resource.Id.FeedbackRegisterButton);

            bool offline = ServiceContainer.Resolve<AuthManager> ().OfflineMode;
            disclaimerContainer.Visibility = offline ? ViewStates.Visible : ViewStates.Gone;
            feedbackContainer.Visibility = offline ? ViewStates.Gone : ViewStates.Visible;

            noUserRegisterButton.Click += (sender, e) => ((MainDrawerActivity)Activity).OpenPage (DrawerListAdapter.RegisterUserPageId);

            SetRating (userRating);

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
            ServiceContainer.Resolve<ITracker> ().CurrentScreen = "Feedback";
        }

        private async void OnSendClick (object sender, EventArgs e)
        {
            IsSendingFeedback = true;

            var mood = FeedbackMessage.Mood.Neutral;
            if (UserRating == ratingPositive) {
                mood = FeedbackMessage.Mood.Positive;
            } else if (UserRating == ratingNegative) {
                mood = FeedbackMessage.Mood.Negative;
            }

            var msg = new FeedbackMessage () {
                CurrentMood = mood,
                Message = UserMessage,
            };
            var sent = await msg.Send ();

            if (sent) {
                if (userRating == ratingPositive) {
                    AskPublishToAppStore.Show (UserMessage, FragmentManager);
                } else {
                    ThankForFeedbackDialog.Show (FragmentManager);
                }
                ResetForm ();
            } else {
                var ctx = ServiceContainer.Resolve<Context> ();
                var toast = Toast.MakeText (ctx, Resource.String.FeedbackSendFailedText, ToastLength.Long);
                toast.Show ();
            }
            IsSendingFeedback = false;
        }

        private void SetRating (int rating)
        {
            UserRating = rating;
        }

        private void OnEdit (object sender, EventArgs e)
        {
            UserMessage = feedbackMessageEditText.Text;
        }

        private void ResetForm ()
        {
            SetRating (ratingNotSet);
            feedbackMessageEditText.Text = String.Empty;
        }

        private void SyncItems ()
        {
            submitFeedbackButton.SetText (isSendingFeedback ? Resource.String.SendFeedbackButtonActiveText : Resource.String.SendFeedbackButtonText);
            submitFeedbackButton.Enabled = UserMessage != String.Empty && !isSendingFeedback;
            feedbackMessageEditText.Enabled = !isSendingFeedback;
            feedbackPositiveButton.Enabled = !isSendingFeedback;
            feedbackNeutralButton.Enabled = !isSendingFeedback;
            feedbackNegativeButton.Enabled = !isSendingFeedback;

            ResetRatingButtonImages ();
            if (UserRating == ratingPositive) {
                feedbackPositiveButton.SetImageResource (Resource.Drawable.IcFeedbackPositiveActive);
            } else if (UserRating == ratingNeutral) {
                feedbackNeutralButton.SetImageResource (Resource.Drawable.IcFeedbackNeutralActive);
            } else if (UserRating == ratingNegative) {
                feedbackNegativeButton.SetImageResource (Resource.Drawable.IcFeedbackNegativeActive);
            }
        }

        private void ResetRatingButtonImages ()
        {
            feedbackPositiveButton.SetImageResource (Resource.Drawable.IcFeedbackPositive);
            feedbackNeutralButton.SetImageResource (Resource.Drawable.IcFeedbackNeutral);
            feedbackNegativeButton.SetImageResource (Resource.Drawable.IcFeedbackNegative);
        }

        private bool IsSendingFeedback
        {
            set {
                if (isSendingFeedback == value) {
                    return;
                }
                isSendingFeedback = value;
                SyncItems ();
            }
        }

        private String UserMessage
        {
            set {
                userMessage = value;
                SyncItems ();
            }
            get {
                userMessage = feedbackMessageEditText.Text;
                return userMessage;
            }
        }

        private int UserRating
        {
            set {
                userRating = value;
                SyncItems ();
            }
            get {
                return userRating;
            }
        }
    }

    public class ThankForFeedbackDialog : BaseDialogFragment
    {

        public ThankForFeedbackDialog ()
        {
        }

        public ThankForFeedbackDialog (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        public static void Show (FragmentManager fragmentManager)
        {
            new ThankForFeedbackDialog ().Show (fragmentManager, "thankforfeedback_dialog");
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            return new AlertDialog.Builder (Activity)
                   .SetTitle (Resource.String.FeedbackThankYouTitle)
                   .SetMessage (Resource.String.FeedbackThankYouMessage)
                   .SetCancelable (true)
                   .SetPositiveButton (Resource.String.FeedbackThankYouOK, (IDialogInterfaceOnClickListener)null)
                   .Create ();
        }
    }

    public class AskPublishToAppStore : BaseDialogFragment
    {

        private static readonly string UserMessageArgument = "com.toggl.timer.user_message";

        public AskPublishToAppStore ()
        {
        }

        public AskPublishToAppStore (IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base (jref, xfer)
        {
        }

        public AskPublishToAppStore (String userMessage)
        {
            var args = new Bundle ();
            args.PutString (UserMessageArgument, userMessage);
            Arguments = args;
        }

        public static void Show (String userMessage, FragmentManager fragmentManager)
        {
            new AskPublishToAppStore (userMessage).Show (fragmentManager, "askpublishtoappstore_dialog");
        }

        private String UserMessage
        {
            get {
                if (Arguments != null) {
                    return Arguments.GetString (UserMessageArgument);
                }
                return null;
            }
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            return new AlertDialog.Builder (Activity)
                   .SetTitle (Resource.String.FeedbackAskPublishTitle)
                   .SetMessage (Resource.String.FeedbackAskPublishMessage)
                   .SetCancelable (true)
                   .SetNegativeButton (Resource.String.FeedbackAskPublishCancel, (IDialogInterfaceOnClickListener)null)
                   .SetPositiveButton (Resource.String.FeedbackAskPublishOK, OnPositiveClick)
                   .Create ();
        }

        private void OnPositiveClick (object sender, DialogClickEventArgs e)
        {
            var ctx = ServiceContainer.Resolve<Context> ();
            var clipboard = (ClipboardManager)ctx.GetSystemService (Context.ClipboardService);
            var clip = ClipData.NewPlainText (Resources.GetString (Resource.String.AppName), UserMessage);
            clipboard.PrimaryClip = clip;

            var toast = Toast.MakeText (ctx, Resource.String.FeedbackCopiedToClipboardToast, ToastLength.Short);
            toast.Show ();

            StartActivity (new Intent (
                               Intent.ActionView,
                               Android.Net.Uri.Parse (Toggl.Phoebe.Build.GooglePlayUrl)
                           ));
        }
    }
}

using System;
using UIKit;

namespace Toggl.Ross.Theme
{
    public static partial class Style
    {
        public static class TimeEntryCell
        {
            public static void ContentView (UIView v)
            {
                v.Opaque = true;
                v.BackgroundColor = Color.White;
            }

            public static void SwipeActionButton (UIButton v)
            {
                v.SetTitleColor (Color.White, UIControlState.Normal);
                v.Font = UIFont.FromName ("HelveticaNeue", 18f);
                v.TitleLabel.TextAlignment = UITextAlignment.Center;
            }

            public static void ProjectLabel (UILabel v)
            {
                v.Font = UIFont.FromName ("HelveticaNeue-Medium", 18f);
            }

            public static void ClientLabel (UILabel v)
            {
                v.Font = UIFont.FromName ("HelveticaNeue", 15f);
                v.TextColor = UIColor.Gray;
            }

            public static void TaskLabel (UILabel v)
            {
                v.Font = UIFont.FromName ("HelveticaNeue-Bold", 14f);
            }

            public static void DescriptionLabel (UILabel v)
            {
                v.Font = UIFont.FromName ("HelveticaNeue", 14f);
            }

            public static void DurationLabel (UILabel v)
            {
                v.TextAlignment = UITextAlignment.Right;
                v.Font = UIFont.FromName ("HelveticaNeue-Light", 18f);
            }

            public static void TaskDescriptionSeparator (UIImageView v)
            {
                v.Image = Image.IconArrowRight;
                v.ContentMode = UIViewContentMode.Center;
            }

            public static void RunningIndicator (UIImageView v)
            {
                v.ContentMode = UIViewContentMode.Center;
                v.Image = Image.IconRunning;
            }

            public static void ErrorIndicator (UIImageView v)
            {
                v.ContentMode = UIViewContentMode.Center;
                v.Image = Image.IconError;
            }

            public static void ContinueState (UIView v)
            {
                v.BackgroundColor = Color.Green;
            }

            public static void NoSwipeState (UIView v)
            {
                v.BackgroundColor = UIColor.Clear;
            }
        }
    }
}

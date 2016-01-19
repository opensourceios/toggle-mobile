using System;
using UIKit;

namespace Toggl.Ross.Theme
{
    public static partial class Style
    {
        public static class Log
        {
            public static void HeaderBackgroundView (UIView v)
            {
                v.BackgroundColor = Color.LightestGray;
            }

            public static void HeaderDateLabel (UILabel v)
            {
                v.TextColor = Color.Gray;
                v.TextAlignment = UITextAlignment.Left;
                v.Font = UIFont.FromName ("HelveticaNeue-Medium", 14f);
            }

            public static void HeaderDurationLabel (UILabel v)
            {
                v.TextColor = Color.Gray;
                v.TextAlignment = UITextAlignment.Right;
                v.Font = UIFont.FromName ("HelveticaNeue", 14f);
            }

            public static void CellContentView (UIView v)
            {
                v.Apply (TimeEntryCell.ContentView);
            }

            public static void CellProjectLabel (UILabel v)
            {
                v.Apply (TimeEntryCell.ProjectLabel);
            }

            public static void CellClientLabel (UILabel v)
            {
                v.Apply (TimeEntryCell.ClientLabel);
            }

            public static void CellTaskLabel (UILabel v)
            {
                v.Apply (TimeEntryCell.TaskLabel);
            }

            public static void CellDescriptionLabel (UILabel v)
            {
                v.Apply (TimeEntryCell.DescriptionLabel);
            }

            public static void CellDurationLabel (UILabel v)
            {
                v.Apply (TimeEntryCell.DurationLabel);
            }

            public static void CellTaskDescriptionSeparator (UIImageView v)
            {
                v.Apply (TimeEntryCell.TaskDescriptionSeparator);
            }

            public static void CellRunningIndicator (UIImageView v)
            {
                v.Apply (TimeEntryCell.RunningIndicator);
            }

            public static void CellErrorIndicator (UIImageView v)
            {
                v.Apply (TimeEntryCell.ErrorIndicator);
            }

            public static void BillableAndTaggedEntry (UIImageView v)
            {
                v.ContentMode = UIViewContentMode.Center;
                v.Image = Image.IconTagBillable;
            }

            public static void TaggedEntry (UIImageView v)
            {
                v.ContentMode = UIViewContentMode.Center;
                v.Image = Image.IconTag;
            }

            public static void BillableEntry (UIImageView v)
            {
                v.ContentMode = UIViewContentMode.Center;
                v.Image = Image.IconBillable;
            }

            public static void PlainEntry (UIImageView v)
            {
                v.Image = null;
            }
        }
    }
}

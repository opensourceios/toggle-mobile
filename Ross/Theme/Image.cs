using UIKit;

namespace Toggl.Ross.Theme
{
    public static class Image
    {
        public static UIImage LoginBackground
        {
            get { return UIImage.FromBundle ("bg"); }
        }

        public static UIImage TagBackground
        {
            get {
                return UIImage.FromBundle ("bg-tag").CreateResizableImage (
                           new UIEdgeInsets (5f, 5f, 5f, 5f), UIImageResizingMode.Tile);
            }
        }

        public static UIImage CircleStart
        {
            get { return UIImage.FromBundle ("circle-start"); }
        }

        public static UIImage CircleStartPressed
        {
            get { return UIImage.FromBundle ("circle-start-pressed"); }
        }

        public static UIImage CircleStop
        {
            get { return UIImage.FromBundle ("circle-stop"); }
        }

        public static UIImage CircleStopPressed
        {
            get { return UIImage.FromBundle ("circle-stop-pressed"); }
        }

        public static UIImage IconArrowRight
        {
            get { return UIImage.FromBundle ("icon-arrow-right"); }
        }

        public static UIImage IconBack
        {
            get { return UIImage.FromBundle ("icon-back"); }
        }

        public static UIImage IconBillable
        {
            get { return UIImage.FromBundle ("icon-billable"); }
        }

        public static UIImage IconCancel
        {
            get { return UIImage.FromBundle ("icon-cancel"); }
        }

        public static UIImage IconDurationArrow
        {
            get { return UIImage.FromBundle ("icon-duration-arrow"); }
        }

        public static UIImage IconNav
        {
            get { return UIImage.FromBundle ("icon-nav"); }
        }

        public static UIImage IconNegative
        {
            get { return UIImage.FromBundle ("icon-negative"); }
        }

        public static UIImage IconNegativeFilled
        {
            get { return UIImage.FromBundle ("icon-negative-filled"); }
        }

        public static UIImage IconNeutral
        {
            get { return UIImage.FromBundle ("icon-neutral"); }
        }

        public static UIImage IconNeutralFilled
        {
            get { return UIImage.FromBundle ("icon-neutral-filled"); }
        }

        public static UIImage IconPositive
        {
            get { return UIImage.FromBundle ("icon-positive"); }
        }

        public static UIImage IconPositiveFilled
        {
            get { return UIImage.FromBundle ("icon-positive-filled"); }
        }

        public static UIImage IconRetry
        {
            get { return UIImage.FromBundle ("icon-retry"); }
        }

        public static UIImage IconRunning
        {
            get { return UIImage.FromBundle ("icon-running"); }
        }

        public static UIImage IconError
        {
            get { return UIImage.FromBundle ("icon-error"); }
        }

        public static UIImage IconTag
        {
            get { return UIImage.FromBundle ("icon-tag"); }
        }

        public static UIImage IconTagBillable
        {
            get { return UIImage.FromBundle ("icon-tag-billable"); }
        }

        public static UIImage Logo
        {
            get { return UIImage.FromBundle ("logo"); }
        }

        public static UIImage ArrowEmptyState
        {
            get { return UIImage.FromBundle ("arrowUp"); }
        }
    }
}

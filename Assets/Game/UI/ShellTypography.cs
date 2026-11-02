namespace LogistiCore.UI
{
    /// <summary>
    /// Portrait type and spacing in canvas units of the 1080×1920 shell reference.
    /// CanvasScaler turns these into screen pixels. They are not physical-pixel caps.
    /// On a 390-wide phone the rendered sizes land near the point-size targets:
    /// brand ~15, heading ~26, section ~18, body ~15, button ~17, caption ~13.
    /// </summary>
    public static class ShellTypography
    {
        public const int Brand = 38;
        public const int Heading = 70;
        public const int Section = 52;
        public const int Body = 38;
        public const int Button = 44;
        public const int Caption = 34;

        public const float Padding = 52f;
        public const float HeaderGap = 14f;
        public const float BlockGap = 28f;
        public const float MapCard = 480f;
        public const float CompanyCard = 340f;
        public const float NoticeCard = 136f;
        public const float ActionHeight = 136f;
        public const float Motif = 112f;
        public const float CardPad = 22f;
        public const float Rule = 2f;
        public const float OverviewMin = 280f;
        public const float OverviewMax = 840f;

        public static float TitleBlock =>
            Brand + HeaderGap + Heading + HeaderGap + Rule;

        public static float HeaderBlock =>
            TitleBlock + HeaderGap + Body;
    }
}

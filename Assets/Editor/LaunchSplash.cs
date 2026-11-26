namespace Frogs.EditorTools
{
    /// <summary>Puts Connor's splash picture into the build (#482). Not yet.</summary>
    public static class LaunchSplash
    {
        public const string PicturePath = "Assets/Art/Splash/splash-1920x1200.png";

        public static void ApplyToBuild()
        {
            ApplyFrom(PicturePath);
        }

        public static void ApplyFrom(string path)
        {
        }
    }
}

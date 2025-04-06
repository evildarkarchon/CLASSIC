// Constants.cs

namespace CLASSIC
{
    public static class Constants
    {
        public const string NullVersion = "0.0.0.0";
        public const string OgVersion = "1.10.163.0";
        public const string NgVersion = "1.10.984.0";
        public const string VrVersion = "1.2.72.0";
        public const string Ogf4SeVersion = "0.6.23";
        public const string Ngf4SeVersion = "0.7.2";
    }

    public enum YamlStore
    {
        Main,
        Settings,
        Ignore,
        Game,
        GameLocal,
        Test
    }

    public enum GameId
    {
        Fallout4,
        Fallout4Vr,
        Skyrim,
        Starfield
    }

    public enum BackupOperation
    {
        Backup,
        Restore,
        Remove
    }
}
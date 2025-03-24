// Constants.cs
namespace CLASSIC
{
    public static class Constants
    {
        public const string NullVersion = "0.0.0.0";
        public const string OGVersion = "1.10.163.0";
        public const string NGVersion = "1.10.984.0";
        public const string VRVersion = "1.2.72.0";
        public const string OGF4SEVersion = "0.6.23";
        public const string NGF4SEVersion = "0.7.2";
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
        Fallout4VR,
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
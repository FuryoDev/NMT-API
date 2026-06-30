using Library_Common;
using Library_Common.SharedObjects.Monitoring;

namespace NMT_api
{
    internal static class Config
    {
        internal static ApplicationInfo Application = new(
            "AI",
            "NMT API",
            SharedGetter.GetInstance_API(SharedGetter.GetHost()), // The Primary Key of the application is the couple [Name + Instance]
            "Neural machine translation API (text, files, and SRT subtitles).",
            "v1.0.1");
        internal static class Related
        {
            internal static readonly List<Application> Applications =
                [
                    new Application("Pacemaker API", SharedConfig.Pacemaker_API.BaseAddress().ToString())
                ];
            internal static readonly List<Database> Databases =
            [
                SharedConfig.SQLServer.Logs.Database()
            ];
            internal static readonly List<Storage> Storages = [];
        }
        internal static class Security
        {
            internal static readonly string Issuer = Application.Name;
            private static readonly string SecretKey_Application =
                Environment.GetEnvironmentVariable("NMT_API_SECRET_KEY")
                ?? throw new InvalidOperationException("Missing required environment variable: NMT_API_SECRET_KEY");
            internal static readonly string SecretKey_Instance = SharedGetter.GetUniqueSecretKey(SecretKey_Application, Issuer);
        }
    }
}

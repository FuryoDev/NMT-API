using Library_Common;
using Library_Common.SharedObjects.Monitoring;

namespace NMT_api
{
    internal static class Config
    {
        internal static ApplicationInfo Application = new(
            "TODOX - The group to which belongs the application (ESB, Video, Audio, AI, Monitoring, etc.)",
            "TODOX - The application name",
            SharedGetter.GetInstance_API(SharedGetter.GetHost()), // TODOX - The Primary Key of the application is the couple [Name + Instance]
            "TODOX - The description of the application",
            "v1.0.1"); // TODOX - The version (not the same as the Assembly Version)
        internal static class Related
        {
            internal static readonly List<Application> Applications =
                [
                    new Application("Pacemaker API", SharedConfig.Pacemaker_API.BaseAddress().ToString()),
                    new Application("TODOX - Any Related API", "TODOX - Its endpoint")
                ];
            internal static readonly List<Database> Databases = // TODOX - The related Databases
            [
                SharedConfig.SQLServer.Logs.Database()
            ];
            internal static readonly List<Storage> Storages = []; // TODOX - The related Storages
        }
        internal static class Security
        {
            internal static readonly string Issuer = Application.Name;
            private const string SecretKey_Application = "TODOX_SecretKey";
            internal static readonly string SecretKey_Instance = SharedGetter.GetUniqueSecretKey(SecretKey_Application, Issuer);
        }
    }
}
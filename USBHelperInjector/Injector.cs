﻿using Harmony;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;
using USBHelperInjector.Pipes;
using USBHelperInjector.Properties;

namespace USBHelperInjector
{
    public class Injector
    {
        private static X509Certificate2 CaCert { get; set; }

        private static PipeServerListener server;

        public static void Init()
        {
            var harmony = HarmonyInstance.Create("me.failedshack.usbhelperinjector");

            var assembly = Assembly.Load("WiiU_USB_Helper");
            var settingsType = assembly.GetType("WIIU_Downloader.Properties.Settings");
            var donationKey = settingsType.GetProperty("DonationKey");

            harmony.Patch(donationKey.GetGetMethod(), prefix: new HarmonyMethod(Overrides.GetMethod("GetDonationKey", typeof(string).MakeByRefType())));
            harmony.Patch(donationKey.GetSetMethod(), prefix: new HarmonyMethod(Overrides.GetMethod("SetDonationKey", typeof(string).MakeByRefType())));

            // Finds the Proxy property inside the NusGrabberForm (which name is obfuscated)
            var proxy = (from type in assembly.GetTypes()
                         where typeof(Form).IsAssignableFrom(type)
                         from prop in type.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                         where prop.Name == "Proxy"
                         select prop).FirstOrDefault();

            harmony.Patch(proxy.GetGetMethod(true), prefix: new HarmonyMethod(Overrides.GetMethod("GetProxy", typeof(WebProxy).MakeByRefType())));
            harmony.Patch(proxy.GetSetMethod(true), prefix: new HarmonyMethod(Overrides.GetMethod("SetProxy", typeof(WebProxy).MakeByRefType())));

            // Finds the method called by the search engine to find matching strings,
            // we patch it in order to fix cases such as 'Pokemon' vs 'Pokémon'.
            var compare = (from type in proxy.DeclaringType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance)
                           where type.GetFields().Length == 1 && type.GetFields()[0].FieldType == typeof(string)
                           from method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                           where method.ReturnType == typeof(bool)
                           && method.GetParameters().Length == 1
                           && method.GetParameters()[0].ParameterType == typeof(string)
                           select method).FirstOrDefault();


            harmony.Patch(compare, prefix: new HarmonyMethod(Overrides.GetMethod("CompareStrings", typeof(bool).MakeByRefType(), typeof(string), typeof(string))));

            harmony.PatchAll();

            Overrides.MessageBoxPatch.Replace("tp9+kFO7LOSD0AZ5zUBHrA==", Resources.Disclaimer);

            server = new PipeServerListener();
            server.Listen();
        }

        public static void TerminateServer()
        {
            server.Shutdown();
        }

        // Should make the given CA certificate be trusted (currently only disables HTTPs validation)
        public static void TrustCertificateAuthority(X509Certificate2 cert)
        {
            CaCert = cert;
            ServicePointManager.ServerCertificateValidationCallback += ValidateServerCertificate;
        }

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Disable HTTPs validation
            return true;
        }
    }
}

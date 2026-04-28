using System;
using System.Net;
using System.Windows.Forms;

namespace OpenSurge
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Enable TLS 1.0, 1.1, 1.2 (Tls11=768, Tls12=3072 not named in .NET 4.0 enum)
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Ssl3 |
                SecurityProtocolType.Tls  |
                (SecurityProtocolType)768 |
                (SecurityProtocolType)3072;

            // Trust all certs - dev/test servers may use self-signed certificates
            ServicePointManager.ServerCertificateValidationCallback = (s, c, ch, e) => true;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace QRCodeScanner
{
    internal static class Program
    {
        internal const string AppName = "QR Code Scanner";

        private static Mutex _instanceLock;

        [STAThread]
        private static int Main(string[] args)
        {
            // Doit etre branche avant que le JIT ne compile une methode qui touche
            // a ZXing : les DLL sont embarquees dans l'exe (voir le .csproj).
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;

            if (args != null && args.Length > 0)
                return RunCommandLine(args);

            bool isFirstInstance;
            _instanceLock = new Mutex(true, @"Local\QRCodeScanner.SingleInstance", out isFirstInstance);
            if (!isFirstInstance)
            {
                MessageBox.Show(
                    AppName + " tourne déjà.\n\nRegarde l'icône dans la zone de notification, en bas "
                    + "à droite (flèche « Afficher les icônes cachées »).",
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => ReportCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception);

            RunTray();
            return 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunTray()
        {
            using (var tray = new TrayContext())
                Application.Run(tray);
        }

        /// <summary>Modes non interactifs, pratiques pour tester ou scripter.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int RunCommandLine(string[] args)
        {
            AttachConsole(-1);

            try
            {
                switch (args[0].ToLowerInvariant())
                {
                    case "--scan-file":
                    case "-f":
                        if (args.Length < 2) return Fail("Usage : QRCodeScanner.exe --scan-file <image>");
                        return Print(QrDecoder.ScanFile(args[1]));

                    case "--scan-now":
                    case "-s":
                        using (var shot = ScreenCapture.CaptureAllScreens())
                            return Print(QrDecoder.Scan(shot));

                    case "--make-qr":
                        if (args.Length < 3) return Fail("Usage : QRCodeScanner.exe --make-qr <texte> <sortie.png>");
                        QrDecoder.WriteQrPng(args[1], args[2], 480);
                        Console.WriteLine(args[2]);
                        return 0;

                    case "--diag":
                        Console.WriteLine("scale curseur = " + Dpi.ScaleForPoint(Cursor.Position));
                        Console.WriteLine("scale bureau  = " + Dpi.DesktopScale);
                        Console.WriteLine("curseur       = " + Cursor.Position);
                        Console.WriteLine("virtual screen= " + SystemInformation.VirtualScreen);
                        return 0;

                    case "--version":
                        Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Version.ToString());
                        return 0;

                    default:
                        Console.WriteLine(AppName);
                        Console.WriteLine("  (sans argument)              lance l'app dans la zone de notification");
                        Console.WriteLine("  --scan-now                   capture l'écran, affiche les QR trouvés");
                        Console.WriteLine("  --scan-file <image>          décode les QR d'un fichier image");
                        Console.WriteLine("  --make-qr <texte> <out.png>  génère un QR code (pour tester)");
                        return 0;
                }
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        private static int Print(IList<string> results)
        {
            foreach (var result in results)
                Console.WriteLine(result);
            return results.Count > 0 ? 0 : 1;
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine(message);
            return 2;
        }

        private static void ReportCrash(Exception ex)
        {
            MessageBox.Show(
                "Une erreur inattendue est survenue :\n\n" + (ex == null ? "(inconnue)" : ex.ToString()),
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static Assembly ResolveEmbedded(object sender, ResolveEventArgs args)
        {
            var resourceName = new AssemblyName(args.Name).Name + ".dll";
            var self = Assembly.GetExecutingAssembly();

            using (var stream = self.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;

                var buffer = new byte[stream.Length];
                var read = 0;
                while (read < buffer.Length)
                {
                    var chunk = stream.Read(buffer, read, buffer.Length - read);
                    if (chunk == 0) break;
                    read += chunk;
                }
                return Assembly.Load(buffer);
            }
        }

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);
    }
}

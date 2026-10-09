using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Keutmann.SharePointManager.Forms;
using Keutmann.SharePointManager.Library;
using SPM2.Framework;
using SPM2.Framework.Validation;
using SPM2.SharePoint;
using SPM2.SharePoint.Validation;
using Autofac;
using SPM2.Framework.IoC;
using System.Reflection;
using SPM2.Framework.Configuration;

namespace Keutmann.SharePointManager
{
    static class Program
    {
        public class Win32
        {
            [DllImport("kernel32.dll")]
            public static extern Boolean AllocConsole();
            [DllImport("kernel32.dll")]
            public static extern Boolean FreeConsole();
        }

        private static Autofac.IContainer autoFacContainer = null;
        public static IContainerAdapter IoCContainer { get; set; }
 
        public static MainWindow Window = null;

        public static Stopwatch Watch = new Stopwatch();
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
#if DEBUG
            Win32.AllocConsole();
#endif
            Trace.WriteLine("Application started");
            Watch.Start();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
           
            try
            {
                var result = SplashScreen.ShowSplashScreen(Setup);
                if (result == ValidationResult.Success)
                {
                    Application.Run(Window);
                }
                Trace.WriteLine("Application ended");
            }
            catch (Exception ex)
            {
                // Do not fail silently. This used to Trace and return, so any startup
                // failure closed the process with no window, no message and no event log
                // entry - a native DLL sitting beside the executable was enough to do it.
                Trace.WriteLine(ex.ToString());
                MessageBox.Show(
                    ex.Message + Environment.NewLine + Environment.NewLine + ex.StackTrace,
                    SPMEnvironment.Version.Title + " - startup failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (autoFacContainer != null)
                    autoFacContainer.Dispose();
            }
        }

        static ValidationResult Setup(SplashScreen splashScreen)
        {
            var validator = new SPFInstalledValidator();

            if (validator.RunValidator() == ValidationResult.Error)
            {
                MessageBox.Show(validator.ErrorString+Environment.NewLine+Environment.NewLine+validator.QuestionString, SPMEnvironment.Version.Title + " Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return ValidationResult.Error;
            }


            var builder = new ContainerBuilder();

            // Find all the assemblies for this application
            builder.RegisterModule(new AutoLoadAssemblies());

            try
            {
                // Build the container now!
                autoFacContainer = builder.Build();
                //CompositionProvider.LoadAssemblies();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                foreach (var loaderEx in ex.LoaderExceptions)
                {
                    // Hier sehen Sie im Debugger genau, welche DLL fehlt (z.B. FileNotFoundException)
                    System.Diagnostics.Debug.WriteLine(loaderEx?.Message);
                }

                /*throw*/;
            }

            IoCContainer = autoFacContainer.Resolve<IContainerAdapter>();


            var provider = IoCContainer.Resolve<SettingsProvider>();
            provider.Load();

            var engine = new PreflightController(splashScreen, IoCContainer);
            if (!engine.Validate())
            {
                return ValidationResult.Error;
            }

            Window = IoCContainer.Resolve<MainWindow>();
            Window.SplashScreenLoad(splashScreen);


            return ValidationResult.Success;
        }


       

        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Cursor.Current = Cursors.Default;
#if DEBUG
            MessageBox.Show(e.Exception.Message+ " : " +e.Exception.StackTrace, SPMLocalization.GetString("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
#else
            MessageBox.Show(e.Exception.Message, SPMLocalization.GetString("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
#endif
        }

    }
}
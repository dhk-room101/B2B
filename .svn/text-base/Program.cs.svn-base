namespace TESVSnip
{
    using System;
    using System.Windows.Forms;
    using System.Diagnostics;

    using TESVSnip.Domain.Services;
    using TESVSnip.Framework.Services;
    using TESVSnip.Properties;
    using TESVSnip.UI.Forms;

    using Settings = TESVSnip.Properties.Settings;

    internal static class Program
    {
        public static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception ex = (Exception)e.ExceptionObject;

                // Since we can't prevent the app from terminating, log this to the event log. 
                if (!EventLog.SourceExists("ThreadException"))
                {
                    EventLog.CreateEventSource("ThreadException", "Application");
                }

                string errMsg =
                  "Message: " + ex.Message +
                  Environment.NewLine +
                  Environment.NewLine +
                  "StackTrace: " + ex.StackTrace +
                  Environment.NewLine +
                  Environment.NewLine +
                  "Source: " + ex.Source +
                  Environment.NewLine +
                  Environment.NewLine +
                  "GetType: " + ex.GetType().ToString();

                Clipboard.SetDataObject(errMsg, true);

                // Create an EventLog instance and assign its source.
                EventLog myLog = new EventLog();
                myLog.Source = "ThreadException";
                myLog.WriteEntry(errMsg );

                MessageBox.Show(errMsg, "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            finally
            {
                Application.Exit();
            }
        }

        public static void ApplicationThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            DialogResult result = DialogResult.Abort;
            try
            {
                string errMsg =
                  "Message: " + e.Exception.Message +
                  Environment.NewLine +
                  Environment.NewLine +
                  "StackTrace: " + e.Exception.StackTrace +
                  Environment.NewLine +
                  Environment.NewLine +
                  "Source: " + e.Exception.Source +
                  Environment.NewLine +
                  Environment.NewLine +
                  "GetType: " + e.Exception.GetType().ToString();

                // Since we can't prevent the app from terminating, log this to the event log. 
                if (!EventLog.SourceExists("ThreadException"))
                {
                    EventLog.CreateEventSource("ThreadException", "Application");
                }

                Clipboard.SetDataObject(errMsg, true);

                // Create an EventLog instance and assign its source.
                EventLog myLog = new EventLog();
                myLog.Source = "ThreadException";
                myLog.WriteEntry(errMsg);

                result = MessageBox.Show(errMsg, "Application Error", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Stop);
            }
            finally
            {
                if (result == DialogResult.Abort)
                {
                    Application.Exit();
                }
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">
        /// The args.
        /// </param>
        [STAThread]
        private static void Main(string[] args)
        {
            Options.Initialize(args);
            Encoding.Initalize(Settings.Default.UseUTF8);
            ZLib.Initialize();

            try
            {
                //AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                // Add an event handler for unhandled exception
                AppDomain.CurrentDomain.UnhandledException += Program.CurrentDomainUnhandledException;

                // Add an event handler for handling UI thread exceptions to the event
                Application.ThreadException += Program.ApplicationThreadException;

                Settings.Default.Reload();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var main = new MainView();
                foreach (string arg in Options.Value.Plugins)
                {
                    main.LoadPlugin(arg);
                }

                try
                {
                    Application.Run(main);
                    Settings.Default.Save();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error running main window: \n" + ex, Resources.ErrorText);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing main view: \n" + ex, Resources.ErrorText);
            }
        }

        //private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
        //{
        //    if (eventArgs.IsTerminating)
        //    {
        //        MessageBox.Show("Fatal Unhandled Exception:\n" + eventArgs.ExceptionObject.ToString(), Resources.ErrorText, MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }
        //    else
        //    {
        //        MessageBox.Show("Unhandled Exception:\n" + eventArgs.ExceptionObject.ToString(), Resources.ErrorText, MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }
        //}

    }
}

using System;
using System.Windows.Forms;

namespace ChatClientGUICS
{
    internal static class Program
    {
        /// <summary>  
        ///  The main entry point for the application.  
        /// </summary>  
        [STAThread]
        static void Main()
        {
            // Replace ApplicationConfiguration.Initialize() with Application.EnableVisualStyles() and Application.SetCompatibleTextRenderingDefault()  
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new XZChat());
        }
    }
}
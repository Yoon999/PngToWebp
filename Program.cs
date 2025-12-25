using System;
using System.Windows.Forms;

namespace PngToWebp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainFormBase());
        }
    }
}
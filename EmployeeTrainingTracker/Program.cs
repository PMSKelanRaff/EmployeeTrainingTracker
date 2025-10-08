using OfficeOpenXml;

namespace EmployeeTrainingTracker
{
    internal static class Program
    {
       

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Set the EPPlus license for non-commercial use
            ExcelPackage.License.SetNonCommercialPersonal("Kelan Rafferty");

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new LoginForm());
        }
    }
}
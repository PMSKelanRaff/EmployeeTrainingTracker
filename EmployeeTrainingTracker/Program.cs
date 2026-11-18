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


            // TEST THE CONNECTION FIRST
            if (DatabaseHelper.TestConnection())
            {
                ApplicationConfiguration.Initialize();

                // 1. Create the login form
                LoginForm loginForm = new LoginForm();

                // 2. Show it as a dialog. The code will pause here.
                if (loginForm.ShowDialog() == DialogResult.OK)
                {
                    // 3. If login was successful, run the *actual* main form
                    Application.Run(loginForm.MainFormToRun);
                }
                // 4. If ShowDialog returns anything else (like Cancel or closing with 'X'),
                //    the Main() method simply ends, and the application exits.
            }
        }
    }
    
}
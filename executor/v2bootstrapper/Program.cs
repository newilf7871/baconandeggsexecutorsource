namespace v2bootstrapper
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                ApplicationConfiguration.Initialize();
                Bootstrapper.Initialize();

                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Bootstrapper Error");
            }
        }
    }
}

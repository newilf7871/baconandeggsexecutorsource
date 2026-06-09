// © BNE Softworks (github.com/newilf7871)
// chrome webview tabs from https://github.com/adamschwartz/chrome-tabs credit to him
// licensed under GNU GPLv3 (see LICENSE)
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

// © BNE Softworks (github.com/newilf7871)
// chrome webview tabs from https://github.com/adamschwartz/chrome-tabs credit to him
// licensed under GNU GPLv3 (see LICENSE)
namespace v2executor
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Startup Error");
            }
        }
    }
}

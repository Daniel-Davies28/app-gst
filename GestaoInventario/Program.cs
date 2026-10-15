namespace GestaoInventario;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new FormularioInventario());
        }
        catch (Exception erro)
        {
            MessageBox.Show($"A aplicação não conseguiu iniciar.\r\n\r\n{erro.Message}", "Erro ao iniciar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

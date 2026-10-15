namespace GestaoInventario.Models;

public sealed class Equipamento
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Categoria { get; set; } = "Desktop";
    public string Marca { get; set; } = "";
    public string Modelo { get; set; } = "";
    public string NumeroSerie { get; set; } = "";
    public DateTime DataEntrada { get; set; } = DateTime.Today;
    public string Estado { get; set; } = "Por avaliar";
    public string Notas { get; set; } = "";
    public decimal PrecoCompra { get; set; }
    public decimal PrecoVenda { get; set; }
    public decimal MargemPrevista => PrecoVenda - PrecoCompra;
}

public sealed record AlteracaoEstado(string Estado, DateTime Data, string Nota);

public sealed record ResumoInventario(int Total, decimal ValorEmStock, decimal MargemPrevista,
    int PorAvaliar, int EmReparacao, int ProntoVenda, int Vendidos, int Descartados);

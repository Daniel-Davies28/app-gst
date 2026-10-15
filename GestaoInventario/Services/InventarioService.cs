using System.Globalization;
using System.Text;
using GestaoInventario.Data;
using GestaoInventario.Models;

namespace GestaoInventario.Services;

public sealed record RelatorioImportacao(int Importados, IReadOnlyList<string> Erros);

public sealed class InventarioService(InventarioRepository repositorio)
{
    public List<Equipamento> Pesquisar(string termo, string categoria, string estado, string ordenarPor, bool descendente)
    {
        IEnumerable<Equipamento> resultado = repositorio.ObterEquipamentos();
        if (!string.IsNullOrWhiteSpace(termo))
        {
            var pesquisa = termo.Trim();
            resultado = resultado.Where(e => new[] { e.Codigo, e.Nome, e.Marca, e.Modelo, e.NumeroSerie }
                .Any(campo => campo.Contains(pesquisa, StringComparison.CurrentCultureIgnoreCase)));
        }
        if (categoria != "Todas") resultado = resultado.Where(e => e.Categoria == categoria);
        if (estado != "Todos") resultado = resultado.Where(e => e.Estado == estado);
        Func<Equipamento, object> chave = ordenarPor switch
        {
            "Código" => e => e.Codigo,
            "Categoria" => e => e.Categoria,
            "Marca" => e => e.Marca,
            "Modelo" => e => e.Modelo,
            "Estado" => e => e.Estado,
            "Preço de compra" => e => e.PrecoCompra,
            "Preço de venda" => e => e.PrecoVenda,
            "Margem" => e => e.MargemPrevista,
            "Data de entrada" => e => e.DataEntrada,
            _ => e => e.Nome
        };
        resultado = descendente ? resultado.OrderByDescending(chave) : resultado.OrderBy(chave);
        return resultado.ToList();
    }

    public RelatorioImportacao ImportarCsv(string caminho, IReadOnlyList<string> categorias, IReadOnlyList<string> estados)
    {
        var linhas = File.ReadAllLines(caminho, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (linhas.Count == 0) return new RelatorioImportacao(0, new[] { "O ficheiro está vazio." });
        var delimitador = linhas[0].Count(c => c == ';') > linhas[0].Count(c => c == ',') ? ';' : ',';
        var cabecalhos = SepararCsv(linhas[0], delimitador).Select(NormalizarCabecalho).ToList();
        var importados = 0;
        var erros = new List<string>();
        for (var i = 1; i < linhas.Count; i++)
        {
            try
            {
                var valores = SepararCsv(linhas[i], delimitador);
                string Obter(string nome)
                {
                    var desejado = NormalizarCabecalho(nome);
                    var indice = cabecalhos.IndexOf(desejado);
                    if (indice < 0 && desejado == "nome") indice = cabecalhos.IndexOf("name");
                    if (indice < 0 && desejado == "numero_serie") indice = cabecalhos.FindIndex(c => c is "serial_number" or "numero_de_serie" or "n_serie");
                    if (indice < 0 && desejado == "preco_compra") indice = cabecalhos.IndexOf("purchase_price");
                    if (indice < 0 && desejado == "preco_venda") indice = cabecalhos.IndexOf("sale_price");
                    if (indice < 0 && desejado == "data_entrada") indice = cabecalhos.IndexOf("entry_date");
                    return indice >= 0 && indice < valores.Count ? valores[indice].Trim() : "";
                }
                var nome = Obter("nome");
                var categoria = Obter("categoria");
                var estado = Obter("estado");
                if (nome.Length == 0) throw new FormatException("Nome obrigatório.");
                if (!categorias.Contains(categoria)) throw new FormatException($"Categoria desconhecida: {categoria}.");
                if (!estados.Contains(estado)) throw new FormatException($"Estado desconhecido: {estado}.");
                var compraTexto = Obter("preco_compra");
                var vendaTexto = Obter("preco_venda");
                var antigoPreco = Obter("preco");
                var compra = LerPreco(string.IsNullOrWhiteSpace(compraTexto) ? antigoPreco : compraTexto);
                var venda = LerPreco(vendaTexto);
                DateTime.TryParse(Obter("data_entrada"), CultureInfo.GetCultureInfo("pt-PT"), DateTimeStyles.None, out var entrada);
                var equipamento = new Equipamento
                {
                    Nome = nome, Categoria = categoria, Estado = estado,
                    Marca = Obter("marca"), Modelo = Obter("modelo"), NumeroSerie = Obter("numero_serie"),
                    Notas = Obter("notas"), DataEntrada = entrada == default ? DateTime.Today : entrada,
                    PrecoCompra = compra, PrecoVenda = venda
                };
                repositorio.Guardar(equipamento);
                importados++;
            }
            catch (Exception erro) when (erro is FormatException || erro is InvalidOperationException)
            {
                erros.Add($"Linha {i + 1}: {erro.Message}");
            }
        }
        return new RelatorioImportacao(importados, erros);
    }

    public static void ExportarCsv(IEnumerable<Equipamento> equipamentos, string caminho)
    {
        using var writer = new StreamWriter(caminho, false, new UTF8Encoding(true));
        writer.WriteLine("codigo;nome;categoria;marca;modelo;numero_serie;data_entrada;estado;notas;preco_compra;preco_venda;margem_prevista");
        foreach (var e in equipamentos)
            writer.WriteLine(string.Join(';', new[] { e.Codigo, e.Nome, e.Categoria, e.Marca, e.Modelo, e.NumeroSerie,
                e.DataEntrada.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), e.Estado, e.Notas,
                e.PrecoCompra.ToString("0.00", CultureInfo.GetCultureInfo("pt-PT")), e.PrecoVenda.ToString("0.00", CultureInfo.GetCultureInfo("pt-PT")), e.MargemPrevista.ToString("0.00", CultureInfo.GetCultureInfo("pt-PT")) }.Select(CitarCsv)));
    }

    private static decimal LerPreco(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return 0;
        var normalizado = valor.Trim().Replace("€", "").Replace(" ", "").Replace("\u00A0", "");
        if (decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-PT"), out var pt)) return pt;
        if (decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant)) return invariant;
        throw new FormatException($"Preço inválido: {valor}.");
    }

    private static string NormalizarCabecalho(string texto) => texto.Trim().Trim('\uFEFF').Trim('"').ToLowerInvariant().Replace(' ', '_').Replace("ç", "c").Replace("ã", "a").Replace("é", "e").Replace("ó", "o").Replace("í", "i").Replace("ú", "u");
    private static string CitarCsv(string valor) => "\"" + valor.Replace("\"", "\"\"") + "\"";

    private static List<string> SepararCsv(string linha, char delimitador)
    {
        var valores = new List<string>(); var campo = new StringBuilder(); var aspas = false;
        for (var i = 0; i < linha.Length; i++)
        {
            var caractere = linha[i];
            if (caractere == '"')
            {
                if (aspas && i + 1 < linha.Length && linha[i + 1] == '"') { campo.Append('"'); i++; }
                else aspas = !aspas;
            }
            else if (caractere == delimitador && !aspas) { valores.Add(campo.ToString()); campo.Clear(); }
            else campo.Append(caractere);
        }
        valores.Add(campo.ToString());
        return valores;
    }
}

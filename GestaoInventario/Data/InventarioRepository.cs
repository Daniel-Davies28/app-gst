using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using GestaoInventario.Models;

namespace GestaoInventario.Data;

// Repositório SQLite: o ecrã não conhece SQL nem o formato do ficheiro.
public sealed class InventarioRepository : IDisposable
{
    private const int SqliteOk = 0;
    private const int SqliteRow = 100;
    private const int SqliteDone = 101;
    private IntPtr ligacao;

    public string Caminho { get; }

    public InventarioRepository(string caminho)
    {
        Caminho = caminho;
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        Verificar(sqlite3_open(caminho, out ligacao));
        InicializarEsquema();
        MigrarJsonAntigo();
    }

    private void InicializarEsquema()
    {
        Executar("PRAGMA foreign_keys=ON; PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL;");
        Executar("CREATE TABLE IF NOT EXISTS settings (chave TEXT PRIMARY KEY, valor TEXT NOT NULL);");
        Executar("CREATE TABLE IF NOT EXISTS categories (nome TEXT PRIMARY KEY, ativa INTEGER NOT NULL DEFAULT 1);");
        Executar("CREATE TABLE IF NOT EXISTS statuses (nome TEXT PRIMARY KEY, ordem INTEGER NOT NULL, cor TEXT NOT NULL DEFAULT '');");
        Executar("CREATE TABLE IF NOT EXISTS equipment (id INTEGER PRIMARY KEY AUTOINCREMENT, code TEXT NOT NULL UNIQUE, name TEXT NOT NULL, category TEXT NOT NULL, brand TEXT NOT NULL DEFAULT '', model TEXT NOT NULL DEFAULT '', serial_number TEXT NOT NULL DEFAULT '', entry_date TEXT NOT NULL, status TEXT NOT NULL, notes TEXT NOT NULL DEFAULT '', purchase_price TEXT NOT NULL DEFAULT '0', sale_price TEXT NOT NULL DEFAULT '0', deleted_at TEXT NULL);");
        Executar("CREATE INDEX IF NOT EXISTS ix_equipment_status ON equipment(status); CREATE INDEX IF NOT EXISTS ix_equipment_category ON equipment(category);");
        Executar("CREATE TABLE IF NOT EXISTS status_history (id INTEGER PRIMARY KEY AUTOINCREMENT, equipment_id INTEGER NOT NULL REFERENCES equipment(id), old_status TEXT NOT NULL, new_status TEXT NOT NULL, changed_at TEXT NOT NULL, note TEXT NOT NULL DEFAULT '');");
        // Estrutura reservada para a Fase C, sem ecrãs ou fluxos ativos nesta versão.
        Executar("CREATE TABLE IF NOT EXISTS equipment_photos (id INTEGER PRIMARY KEY AUTOINCREMENT, equipment_id INTEGER NOT NULL REFERENCES equipment(id), file_path TEXT NOT NULL, caption TEXT NOT NULL DEFAULT '');");
        Executar("CREATE TABLE IF NOT EXISTS repair_checklist_items (id INTEGER PRIMARY KEY AUTOINCREMENT, equipment_id INTEGER NOT NULL REFERENCES equipment(id), item_name TEXT NOT NULL, completed INTEGER NOT NULL DEFAULT 0, note TEXT NOT NULL DEFAULT '');");
        Executar("CREATE TABLE IF NOT EXISTS suppliers (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, contact TEXT NOT NULL DEFAULT '');");
        Executar("CREATE TABLE IF NOT EXISTS customers (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, contact TEXT NOT NULL DEFAULT '');");
        Executar("CREATE TABLE IF NOT EXISTS app_users (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, role TEXT NOT NULL DEFAULT '');");
        Executar("CREATE TABLE IF NOT EXISTS sales (id INTEGER PRIMARY KEY AUTOINCREMENT, equipment_id INTEGER NOT NULL REFERENCES equipment(id), customer_id INTEGER NULL REFERENCES customers(id), sold_at TEXT NOT NULL, amount TEXT NOT NULL);");
        Executar("INSERT OR IGNORE INTO categories(nome) VALUES ('Desktop'), ('Portátil');");
        Executar("INSERT OR IGNORE INTO statuses(nome, ordem, cor) VALUES ('Por avaliar',1,'#B88A4A'), ('Em reparação',2,'#6486A8'), ('Pronto para venda',3,'#558B72'), ('Vendido',4,'#778579'), ('Descartado',5,'#B76C67');");
    }

    public List<string> ObterCategorias() => Consulta("SELECT nome FROM categories WHERE ativa=1 ORDER BY nome;").Select(l => l[0]).ToList();
    public List<string> ObterEstados() => Consulta("SELECT nome FROM statuses ORDER BY ordem;").Select(l => l[0]).ToList();

    public List<Equipamento> ObterEquipamentos(bool incluirRemovidos = false)
    {
        var where = incluirRemovidos ? "" : "WHERE deleted_at IS NULL";
        return Consulta($"SELECT id,code,name,category,brand,model,serial_number,entry_date,status,notes,purchase_price,sale_price FROM equipment {where};")
            .Select(l => new Equipamento
            {
                Id = Int(l[0]), Codigo = l[1], Nome = l[2], Categoria = l[3], Marca = l[4], Modelo = l[5],
                NumeroSerie = l[6], DataEntrada = DateTime.Parse(l[7], CultureInfo.InvariantCulture), Estado = l[8],
                Notas = l[9], PrecoCompra = Dec(l[10]), PrecoVenda = Dec(l[11])
            }).ToList();
    }

    public Equipamento Guardar(Equipamento equipamento, string notaEstado = "")
    {
        if (equipamento.Id == 0)
        {
            var proximo = Int(Consulta("SELECT COALESCE(MAX(id),0)+1 FROM equipment;")[0][0]);
            equipamento.Codigo = $"EQ-{proximo:0000}";
            Executar($"INSERT INTO equipment(code,name,category,brand,model,serial_number,entry_date,status,notes,purchase_price,sale_price) VALUES ({Q(equipamento.Codigo)},{Q(equipamento.Nome)},{Q(equipamento.Categoria)},{Q(equipamento.Marca)},{Q(equipamento.Modelo)},{Q(equipamento.NumeroSerie)},{Q(equipamento.DataEntrada.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))},{Q(equipamento.Estado)},{Q(equipamento.Notas)},{Q(equipamento.PrecoCompra.ToString(CultureInfo.InvariantCulture))},{Q(equipamento.PrecoVenda.ToString(CultureInfo.InvariantCulture))});");
            equipamento.Id = Int(Consulta("SELECT last_insert_rowid();")[0][0]);
            GuardarHistorico(equipamento.Id, "", equipamento.Estado, string.IsNullOrWhiteSpace(notaEstado) ? "Registo inicial" : notaEstado);
        }
        else
        {
            var estadoAnterior = Consulta($"SELECT status FROM equipment WHERE id={equipamento.Id};").FirstOrDefault()?.FirstOrDefault() ?? equipamento.Estado;
            Executar($"UPDATE equipment SET name={Q(equipamento.Nome)},category={Q(equipamento.Categoria)},brand={Q(equipamento.Marca)},model={Q(equipamento.Modelo)},serial_number={Q(equipamento.NumeroSerie)},entry_date={Q(equipamento.DataEntrada.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))},status={Q(equipamento.Estado)},notes={Q(equipamento.Notas)},purchase_price={Q(equipamento.PrecoCompra.ToString(CultureInfo.InvariantCulture))},sale_price={Q(equipamento.PrecoVenda.ToString(CultureInfo.InvariantCulture))} WHERE id={equipamento.Id};");
            if (estadoAnterior != equipamento.Estado) GuardarHistorico(equipamento.Id, estadoAnterior, equipamento.Estado, string.IsNullOrWhiteSpace(notaEstado) ? "Alteração de estado" : notaEstado);
        }
        return equipamento;
    }

    public void Remover(IEnumerable<int> ids)
    {
        var agora = Q(DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
        Executar("BEGIN;");
        foreach (var id in ids.Distinct()) Executar($"UPDATE equipment SET deleted_at={agora} WHERE id={id} AND deleted_at IS NULL;");
        Executar("COMMIT;");
    }

    public int DesfazerUltimaRemocao()
    {
        var data = Consulta("SELECT deleted_at FROM equipment WHERE deleted_at IS NOT NULL ORDER BY deleted_at DESC LIMIT 1;").FirstOrDefault()?.FirstOrDefault();
        if (string.IsNullOrEmpty(data)) return 0;
        var quantidade = Int(Consulta($"SELECT COUNT(*) FROM equipment WHERE deleted_at={Q(data)};")[0][0]);
        Executar($"UPDATE equipment SET deleted_at=NULL WHERE deleted_at={Q(data)};");
        return quantidade;
    }

    public List<AlteracaoEstado> ObterHistorico(int id) => Consulta($"SELECT new_status,changed_at,note FROM status_history WHERE equipment_id={id} ORDER BY changed_at DESC;")
        .Select(l => new AlteracaoEstado(l[0], DateTime.Parse(l[1], CultureInfo.InvariantCulture), l[2])).ToList();

    public ResumoInventario ObterResumo()
    {
        var rows = Consulta("SELECT status,COUNT(*),SUM(CAST(purchase_price AS REAL)),SUM(CAST(sale_price AS REAL)) FROM equipment WHERE deleted_at IS NULL GROUP BY status;");
        var total = 0; decimal stock = 0, margin = 0; var counts = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            var n = Int(row[1]); counts[row[0]] = n; total += n;
            var compra = (decimal)double.Parse(row[2] == "" ? "0" : row[2], CultureInfo.InvariantCulture);
            var venda = (decimal)double.Parse(row[3] == "" ? "0" : row[3], CultureInfo.InvariantCulture);
            if (row[0] != "Vendido" && row[0] != "Descartado") { stock += compra; margin += venda - compra; }
        }
        int C(string estado) => counts.TryGetValue(estado, out var n) ? n : 0;
        return new ResumoInventario(total, stock, margin, C("Por avaliar"), C("Em reparação"), C("Pronto para venda"), C("Vendido"), C("Descartado"));
    }

    public void GuardarHistorico(int id, string antigo, string novo, string nota) =>
        Executar($"INSERT INTO status_history(equipment_id,old_status,new_status,changed_at,note) VALUES ({id},{Q(antigo)},{Q(novo)},{Q(DateTime.Now.ToString("O", CultureInfo.InvariantCulture))},{Q(nota)});");

    public void GuardarCategoria(string categoria) => Executar($"INSERT OR IGNORE INTO categories(nome) VALUES ({Q(categoria)});");
    public bool RemoverCategoria(string categoria)
    {
        if (Int(Consulta($"SELECT COUNT(*) FROM equipment WHERE category={Q(categoria)} AND deleted_at IS NULL;")[0][0]) > 0) return false;
        Executar($"DELETE FROM categories WHERE nome={Q(categoria)};"); return true;
    }

    public void GuardarEstado(string estado)
    {
        var ordem = Int(Consulta("SELECT COALESCE(MAX(ordem),0)+1 FROM statuses;")[0][0]);
        Executar($"INSERT OR IGNORE INTO statuses(nome,ordem) VALUES ({Q(estado)},{ordem});");
    }
    public bool RemoverEstado(string estado)
    {
        if (Int(Consulta($"SELECT COUNT(*) FROM equipment WHERE status={Q(estado)} AND deleted_at IS NULL;")[0][0]) > 0) return false;
        Executar($"DELETE FROM statuses WHERE nome={Q(estado)};"); return true;
    }

    public string ObterDefinicao(string chave, string valorPadrao = "") => Consulta($"SELECT valor FROM settings WHERE chave={Q(chave)};").FirstOrDefault()?.FirstOrDefault() ?? valorPadrao;
    public void GuardarDefinicao(string chave, string valor) => Executar($"INSERT INTO settings(chave,valor) VALUES ({Q(chave)},{Q(valor)}) ON CONFLICT(chave) DO UPDATE SET valor=excluded.valor;");

    public void RestaurarBaseDados(string caminhoCopia)
    {
        Verificar(sqlite3_open(caminhoCopia, out var origem));
        try
        {
            var backup = sqlite3_backup_init(ligacao, "main", origem, "main");
            if (backup == IntPtr.Zero) throw new InvalidOperationException(ErroSqlite());
            try
            {
                var estado = sqlite3_backup_step(backup, -1);
                if (estado != SqliteDone && estado != SqliteOk) Verificar(estado);
            }
            finally { sqlite3_backup_finish(backup); }
        }
        finally { sqlite3_close(origem); }
    }

    private void MigrarJsonAntigo()
    {
        if (ObterDefinicao("json_migration_done") == "1") return;
        var caminhoAntigo = Path.Combine(Path.GetDirectoryName(Caminho)!, "equipamentos.json");
        if (!File.Exists(caminhoAntigo)) { GuardarDefinicao("json_migration_done", "1"); return; }
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(caminhoAntigo));
            var raiz = json.RootElement;
            var ns = raiz.GetProperty("nomes").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
            var cs = raiz.GetProperty("categorias").EnumerateArray().Select(x => x.GetString() ?? "Desktop").ToList();
            var es = raiz.GetProperty("estados").EnumerateArray().Select(x => x.GetString() ?? "Por avaliar").ToList();
            var ps = raiz.GetProperty("precos").EnumerateArray().Select(x => x.GetDecimal()).ToList();
            if (ns.Count != cs.Count || ns.Count != es.Count || ns.Count != ps.Count) throw new InvalidDataException("A base antiga tem listas incompletas.");
            Executar("BEGIN IMMEDIATE;");
            try
            {
                for (var i = 0; i < ns.Count; i++)
                {
                    GuardarCategoria(cs[i]);
                    var equipamento = new Equipamento { Nome = ns[i], Categoria = cs[i], Estado = MapearEstado(es[i]), PrecoCompra = ps[i], PrecoVenda = ps[i] };
                    Guardar(equipamento);
                }
                if (raiz.TryGetProperty("temaEscuro", out var tema)) GuardarDefinicao("tema", tema.GetBoolean() ? "escuro" : "claro");
                if (raiz.TryGetProperty("tamanhoFonte", out var tamanho)) GuardarDefinicao("tamanhoFonte", tamanho.GetInt32().ToString(CultureInfo.InvariantCulture));
                Executar("COMMIT;");
            }
            catch { Executar("ROLLBACK;"); throw; }
            GuardarDefinicao("json_migration_done", "1");
        }
        catch { throw new InvalidDataException($"Não foi possível migrar a base antiga. O ficheiro original foi preservado: {caminhoAntigo}"); }
    }

    private static string MapearEstado(string estado) => estado == "Recondicionado" ? "Pronto para venda" : estado;
    private static string Q(string? valor) => "'" + (valor ?? "").Replace("'", "''") + "'";
    private static int Int(string valor) => int.Parse(valor, CultureInfo.InvariantCulture);
    private static decimal Dec(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);

    private List<string[]> Consulta(string sql)
    {
        Verificar(sqlite3_prepare_v2(ligacao, sql, -1, out var statement, IntPtr.Zero));
        try
        {
            var linhas = new List<string[]>();
            var colunas = sqlite3_column_count(statement);
            while (true)
            {
                var estado = sqlite3_step(statement);
                if (estado == SqliteDone) break;
                if (estado != SqliteRow) throw new InvalidOperationException(ErroSqlite());
                var linha = new string[colunas];
                for (var i = 0; i < colunas; i++)
                {
                    var texto = sqlite3_column_text(statement, i);
                    linha[i] = texto == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(texto) ?? "";
                }
                linhas.Add(linha);
            }
            return linhas;
        }
        finally { sqlite3_finalize(statement); }
    }

    private void Executar(string sql)
    {
        var resultado = sqlite3_exec(ligacao, sql, IntPtr.Zero, IntPtr.Zero, out var erro);
        if (resultado == SqliteOk) return;
        var mensagem = erro == IntPtr.Zero ? ErroSqlite() : Marshal.PtrToStringUTF8(erro) ?? ErroSqlite();
        if (erro != IntPtr.Zero) sqlite3_free(erro);
        throw new InvalidOperationException(mensagem);
    }

    private string ErroSqlite() => Marshal.PtrToStringUTF8(sqlite3_errmsg(ligacao)) ?? "Erro desconhecido na base de dados.";
    private void Verificar(int resultado) { if (resultado != SqliteOk) throw new InvalidOperationException(ErroSqlite()); }
    public void Dispose() { if (ligacao != IntPtr.Zero) { sqlite3_close(ligacao); ligacao = IntPtr.Zero; } }

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open([MarshalAs(UnmanagedType.LPUTF8Str)] string filename, out IntPtr db);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_exec(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, IntPtr callback, IntPtr arg, out IntPtr error);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out IntPtr statement, IntPtr tail);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr statement);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr statement);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_count(IntPtr statement);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_errmsg(IntPtr db);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void sqlite3_free(IntPtr pointer);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_backup_init(IntPtr destination, [MarshalAs(UnmanagedType.LPUTF8Str)] string destinationName, IntPtr source, [MarshalAs(UnmanagedType.LPUTF8Str)] string sourceName);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_backup_step(IntPtr backup, int pages);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_backup_finish(IntPtr backup);
}

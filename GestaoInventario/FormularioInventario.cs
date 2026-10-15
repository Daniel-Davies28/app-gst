using System.Globalization;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using GestaoInventario.Data;
using GestaoInventario.Models;
using GestaoInventario.Services;
using GestaoInventario.UI;

namespace GestaoInventario;

public sealed class FormularioInventario : Form
{
    private readonly string pastaDados = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SIAInventario");
    private InventarioRepository repositorio = null!;
    private InventarioService servico = null!;
    private Paleta paleta = DesignSystem.Clara;
    private bool inicializando;
    private bool temaEscuro;
    private int tamanhoFonte = 1;
    private string moeda = "EUR";
    private string ordenarPor = "Nome";
    private bool ordemDescendente;
    private int idEmEdicao;
    private List<Equipamento> visiveis = new();
    private readonly Dictionary<Control, Font> fontesBase = new();
    private readonly Dictionary<Button, Size> tamanhosBaseBotoes = new();
    private readonly ToolTip dica = new();
    private readonly ErrorProvider erros;
    private readonly System.Windows.Forms.Timer temporizadorMensagem = new() { Interval = 3200 };

    private readonly TabControl abas = new();
    private readonly TabPage paginaInventario = new("Inventário");
    private readonly TabPage paginaDefinicoes = new("Definições");
    private readonly TableLayoutPanel layoutInventario = new();
    private readonly TableLayoutPanel layoutFormulario = new();
    private readonly TableLayoutPanel grelhaDados = new();
    private readonly FlowLayoutPanel barraFiltros = new();
    private readonly Panel cartaoFormulario = new();
    private readonly Panel painelGrelha = new();
    private readonly DataGridView grelha = new();
    private readonly Label estadoVazio = new();
    private readonly Label lblTitulo = new();
    private readonly Label lblSubtitulo = new();
    private readonly Label lblCodigo = new();
    private readonly Label lblMargem = new();
    private readonly Label lblResumoTotal = new();
    private readonly Label lblResumoStock = new();
    private readonly Label lblResumoMargem = new();
    private readonly Label lblResumoEstados = new();
    private readonly TextBox txtNome = new();
    private readonly TextBox txtMarca = new();
    private readonly TextBox txtModelo = new();
    private readonly TextBox txtSerie = new();
    private readonly TextBox txtCompra = new();
    private readonly TextBox txtVenda = new();
    private readonly TextBox txtNotas = new();
    private readonly TextBox txtPesquisa = new();
    private readonly ComboBox cmbCategoria = new();
    private readonly ComboBox cmbEstado = new();
    private readonly ComboBox cmbFiltroCategoria = new();
    private readonly ComboBox cmbFiltroEstado = new();
    private readonly ComboBox cmbTema = new();
    private readonly ComboBox cmbTamanho = new();
    private readonly ComboBox cmbMoeda = new();
    private readonly DateTimePicker dataEntrada = new();
    private readonly Button btnGuardar = new();
    private readonly Button btnCancelar = new();
    private readonly Button btnEditar = new();
    private readonly Button btnRemover = new();
    private readonly Button btnImportar = new();
    private readonly Button btnExportar = new();
    private readonly Button btnHistorico = new();
    private readonly Button btnTemaCabecalho = new();
    private readonly ListBox listaCategorias = new();
    private readonly ListBox listaEstados = new();
    private readonly TextBox txtNovaCategoria = new();
    private readonly TextBox txtNovoEstado = new();
    private readonly Label lblLocalBaseDados = new();
    private readonly ToolStripStatusLabel lblMensagem = new();
    private readonly ToolStripButton btnDesfazer = new("Desfazer remoção");
    private readonly StatusStrip barraEstado = new();

    public FormularioInventario(string? caminhoBaseDados = null)
    {
        Text = "Inventário de equipamentos";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);
        Size = new Size(1240, 820);
        Font = new Font("Segoe UI", 11);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        erros = new ErrorProvider { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };

        var caminhoDb = caminhoBaseDados ?? Path.Combine(pastaDados, "inventario.db");
        repositorio = new InventarioRepository(caminhoDb);
        servico = new InventarioService(repositorio);
        ConstruirInterface();
        RegistarFontes(this);

        Load += FormularioInventario_Load;
        FormClosing += (_, _) => { GuardarDefinicoes(); repositorio.Dispose(); };
        HandleCreated += (_, _) => AplicarTemaBarraTitulo();
        KeyDown += Formulario_KeyDown;
        abas.DrawItem += Abas_DrawItem;
        cmbTema.SelectedIndexChanged += DefinicoesAlteradas;
        cmbTamanho.SelectedIndexChanged += DefinicoesAlteradas;
        cmbMoeda.SelectedIndexChanged += DefinicoesAlteradas;
        cmbFiltroCategoria.SelectedIndexChanged += (_, _) => AtualizarGrelha();
        cmbFiltroEstado.SelectedIndexChanged += (_, _) => AtualizarGrelha();
        txtPesquisa.TextChanged += (_, _) => AtualizarGrelha();
        grelha.ColumnHeaderMouseClick += Grelha_ColumnHeaderMouseClick;
        grelha.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditarSelecionado(); };
        grelha.SelectionChanged += (_, _) => AtualizarEstadoBotoes();
        grelha.CellFormatting += Grelha_CellFormatting;
        btnGuardar.Click += (_, _) => GuardarFormulario();
        btnCancelar.Click += (_, _) => LimparFormulario();
        btnEditar.Click += (_, _) => EditarSelecionado();
        btnRemover.Click += (_, _) => RemoverSelecionados();
        btnImportar.Click += (_, _) => ImportarCsv();
        btnExportar.Click += (_, _) => ExportarCsv();
        btnHistorico.Click += (_, _) => MostrarHistorico();
        btnTemaCabecalho.Click += (_, _) =>
        {
            temaEscuro = !temaEscuro;
            inicializando = true; cmbTema.SelectedIndex = temaEscuro ? 1 : 0; inicializando = false;
            AplicarTema(); GuardarDefinicoes();
        };
        btnDesfazer.Click += (_, _) => DesfazerRemocao();
        temporizadorMensagem.Tick += (_, _) => { lblMensagem.Text = "Pronto"; temporizadorMensagem.Stop(); };
        txtCompra.TextChanged += (_, _) => AtualizarMargem();
        txtVenda.TextChanged += (_, _) => AtualizarMargem();
        cmbEstado.SelectedIndexChanged += (_, _) => AtualizarMargem();
        AplicarTema();
    }

    private void ConstruirInterface()
    {
        abas.Dock = DockStyle.Fill;
        abas.DrawMode = TabDrawMode.OwnerDrawFixed;
        abas.ItemSize = new Size(136, 38);
        abas.SizeMode = TabSizeMode.Fixed;
        abas.Padding = new Point(16, 7);
        paginaInventario.UseVisualStyleBackColor = false;
        paginaDefinicoes.UseVisualStyleBackColor = false;
        paginaInventario.Padding = new Padding(0);
        paginaDefinicoes.Padding = new Padding(0);
        abas.TabPages.AddRange(new[] { paginaInventario, paginaDefinicoes });
        Controls.Add(abas);

        barraEstado.Dock = DockStyle.Bottom;
        barraEstado.SizingGrip = false;
        lblMensagem.Text = "Pronto";
        btnDesfazer.Visible = false;
        barraEstado.Items.Add(lblMensagem);
        barraEstado.Items.Add(new ToolStripStatusLabel { Spring = true });
        barraEstado.Items.Add(btnDesfazer);
        Controls.Add(barraEstado);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = paleta.Fundo };
        paginaInventario.Controls.Add(scroll);
        layoutInventario.Dock = DockStyle.Fill;
        layoutInventario.Padding = new Padding(DesignSystem.Espaco24);
        layoutInventario.ColumnCount = 1;
        layoutInventario.RowCount = 5;
        layoutInventario.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
        layoutInventario.RowStyles.Add(new RowStyle(SizeType.Percent, 13));
        layoutInventario.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        layoutInventario.RowStyles.Add(new RowStyle(SizeType.Percent, 12));
        layoutInventario.RowStyles.Add(new RowStyle(SizeType.Percent, 23));
        layoutInventario.MinimumSize = new Size(840, 700);
        scroll.Controls.Add(layoutInventario);

        ConstruirCabecalho();
        ConstruirResumo();
        ConstruirFormulario();
        ConstruirBarraFiltros();
        ConstruirGrelha();
        layoutInventario.Controls.Add(painelGrelha, 0, 4);
        ConstruirDefinicoes();
    }

    private void ConstruirCabecalho()
    {
        var cabecalho = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = paleta.Fundo };
        cabecalho.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76));
        cabecalho.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        cabecalho.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        cabecalho.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        lblTitulo.Text = "Inventário de equipamentos";
        lblTitulo.Font = new Font("Segoe UI Semibold", DesignSystem.FonteTitulo);
        lblTitulo.Dock = DockStyle.Fill;
        lblTitulo.TextAlign = ContentAlignment.BottomLeft;
        lblSubtitulo.Text = "Gestão de equipamentos recondicionados";
        lblSubtitulo.Tag = "muted";
        lblSubtitulo.Dock = DockStyle.Fill;
        lblSubtitulo.TextAlign = ContentAlignment.TopLeft;
        btnTemaCabecalho.Text = "Tema escuro";
        btnTemaCabecalho.Dock = DockStyle.Right;
        btnTemaCabecalho.Width = 132;
        btnTemaCabecalho.Margin = new Padding(0, 4, 0, 4);
        cabecalho.Controls.Add(lblTitulo, 0, 0);
        cabecalho.Controls.Add(btnTemaCabecalho, 1, 0);
        cabecalho.Controls.Add(lblSubtitulo, 0, 1);
        layoutInventario.Controls.Add(cabecalho, 0, 0);
    }

    private void ConstruirResumo()
    {
        var cartoes = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = paleta.Fundo };
        for (var i = 0; i < 4; i++) cartoes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        cartoes.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AdicionarCartaoResumo(cartoes, "EQUIPAMENTOS", lblResumoTotal, 0);
        AdicionarCartaoResumo(cartoes, "VALOR EM STOCK", lblResumoStock, 1);
        AdicionarCartaoResumo(cartoes, "MARGEM PREVISTA", lblResumoMargem, 2);
        AdicionarCartaoResumo(cartoes, "POR ESTADO", lblResumoEstados, 3);
        layoutInventario.Controls.Add(cartoes, 0, 1);
    }

    private void AdicionarCartaoResumo(TableLayoutPanel painel, string titulo, Label valor, int coluna)
    {
        var card = CriarCartao();
        card.Margin = new Padding(0, 0, DesignSystem.Espaco12, 0);
        var conteudo = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(DesignSystem.Espaco16, DesignSystem.Espaco8, DesignSystem.Espaco8, DesignSystem.Espaco8), RowCount = 2, ColumnCount = 1, BackColor = paleta.Cartao };
        conteudo.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        conteudo.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        var labelTitulo = new Label { Text = titulo, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9), Tag = "muted", TextAlign = ContentAlignment.MiddleLeft };
        valor.Dock = DockStyle.Fill;
        valor.Font = new Font("Segoe UI Semibold", titulo == "POR ESTADO" ? DesignSystem.FontePequena + 1 : DesignSystem.FonteResumo);
        valor.TextAlign = ContentAlignment.MiddleLeft;
        conteudo.Controls.Add(labelTitulo, 0, 0);
        conteudo.Controls.Add(valor, 0, 1);
        card.Controls.Add(conteudo);
        painel.Controls.Add(card, coluna, 0);
    }

    private void ConstruirFormulario()
    {
        cartaoFormulario.Dock = DockStyle.Fill;
        cartaoFormulario.Padding = new Padding(DesignSystem.Espaco16, DesignSystem.Espaco8, DesignSystem.Espaco16, DesignSystem.Espaco8);
        cartaoFormulario.Tag = "cartao";
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = paleta.Cartao };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
        for (var i = 0; i < 3; i++) content.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 24));
        var titulo = new Label { Text = "Dados do equipamento", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", DesignSystem.FonteSecao), TextAlign = ContentAlignment.MiddleLeft };
        content.Controls.Add(titulo, 0, 0);

        grelhaDados.Dock = DockStyle.Fill;
        grelhaDados.ColumnCount = 4;
        grelhaDados.RowCount = 3;
        grelhaDados.BackColor = paleta.Cartao;
        grelhaDados.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        grelhaDados.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21));
        grelhaDados.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21));
        grelhaDados.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        for (var i = 0; i < 3; i++) grelhaDados.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        ConfigurarTexto(txtNome, "Nome do equipamento", 0, 0, true);
        ConfigurarDropdown(cmbCategoria, 1, "Categoria");
        ConfigurarDropdown(cmbEstado, 2, "Estado");
        ConfigurarData(3, "Data de entrada");
        ConfigurarCodigo(0, 1);
        ConfigurarTexto(txtMarca, "Marca", 1, 1);
        ConfigurarTexto(txtModelo, "Modelo", 2, 1);
        ConfigurarTexto(txtSerie, "N.º de série", 3, 1);
        ConfigurarTexto(txtCompra, "Preço de compra (€)", 0, 2, placeholder: "0,00");
        ConfigurarTexto(txtVenda, "Preço de venda (€)", 1, 2, placeholder: "0,00");
        ConfigurarMargem(2, 2);
        ConfigurarNotas(3, 2);
        content.Controls.Add(grelhaDados, 0, 1);
        content.SetRowSpan(grelhaDados, 3);

        var acoes = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 2, 0, 0), BackColor = paleta.Cartao };
        btnGuardar.Text = "Adicionar equipamento";
        btnCancelar.Text = "Cancelar";
        btnCancelar.Visible = false;
        ConfigurarBotao(btnGuardar, "primario");
        ConfigurarBotao(btnCancelar, "secundario");
        acoes.Controls.Add(btnGuardar);
        acoes.Controls.Add(btnCancelar);
        content.Controls.Add(acoes, 0, 4);
        cartaoFormulario.Controls.Add(content);
        layoutInventario.Controls.Add(cartaoFormulario, 0, 2);
    }

    private void ConfigurarTexto(TextBox caixa, string legenda, int coluna, int linha, bool foco = false, string placeholder = "")
    {
        var campo = CriarCampo(legenda);
        caixa.Dock = DockStyle.Fill;
        caixa.BorderStyle = BorderStyle.FixedSingle;
        caixa.Margin = new Padding(0, 0, DesignSystem.Espaco12, 0);
        caixa.PlaceholderText = placeholder;
        if (caixa == txtNotas) caixa.Multiline = true;
        caixa.Enter += (_, _) => caixa.BackColor = paleta.Selecao;
        caixa.Leave += (_, _) => caixa.BackColor = paleta.Campo;
        ((TableLayoutPanel)campo).Controls.Add(caixa, 0, 1);
        grelhaDados.Controls.Add(campo, coluna, linha);
        if (foco) dica.SetToolTip(caixa, "Obrigatório. Enter guarda o equipamento.");
    }

    private void ConfigurarDropdown(ComboBox combo, int coluna, string legenda)
    {
        var campo = CriarCampo(legenda);
        combo.Dock = DockStyle.Fill;
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.FlatStyle = FlatStyle.Flat;
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.ItemHeight = DesignSystem.AlturaCampo - 9;
        combo.Margin = new Padding(0, 0, DesignSystem.Espaco12, 0);
        combo.DrawItem += Combo_DrawItem;
        ((TableLayoutPanel)campo).Controls.Add(combo, 0, 1);
        grelhaDados.Controls.Add(campo, coluna, 0);
    }

    private void ConfigurarData(int coluna, string legenda)
    {
        var campo = CriarCampo(legenda);
        dataEntrada.Dock = DockStyle.Fill;
        dataEntrada.Format = DateTimePickerFormat.Short;
        dataEntrada.Margin = Padding.Empty;
        ((TableLayoutPanel)campo).Controls.Add(dataEntrada, 0, 1);
        grelhaDados.Controls.Add(campo, coluna, 0);
    }

    private void ConfigurarCodigo(int coluna, int linha)
    {
        var campo = CriarCampo("Código automático");
        lblCodigo.Text = "Será atribuído ao guardar";
        lblCodigo.Dock = DockStyle.Fill;
        lblCodigo.TextAlign = ContentAlignment.MiddleLeft;
        lblCodigo.Margin = new Padding(0, 0, DesignSystem.Espaco12, 0);
        ((TableLayoutPanel)campo).Controls.Add(lblCodigo, 0, 1);
        grelhaDados.Controls.Add(campo, coluna, linha);
    }

    private void ConfigurarMargem(int coluna, int linha)
    {
        var campo = CriarCampo("Margem prevista");
        lblMargem.Dock = DockStyle.Fill;
        lblMargem.Text = "0,00 €";
        lblMargem.TextAlign = ContentAlignment.MiddleLeft;
        lblMargem.Margin = new Padding(0, 0, DesignSystem.Espaco12, 0);
        ((TableLayoutPanel)campo).Controls.Add(lblMargem, 0, 1);
        grelhaDados.Controls.Add(campo, coluna, linha);
    }

    private void ConfigurarNotas(int coluna, int linha)
    {
        var campo = CriarCampo("Notas");
        txtNotas.Dock = DockStyle.Fill;
        txtNotas.Multiline = true;
        txtNotas.ScrollBars = ScrollBars.Vertical;
        txtNotas.Margin = Padding.Empty;
        ((TableLayoutPanel)campo).Controls.Add(txtNotas, 0, 1);
        grelhaDados.Controls.Add(campo, coluna, linha);
    }

    private Panel CriarCampo(string legenda)
    {
        var campo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = paleta.Cartao, Margin = new Padding(DesignSystem.Espaco8, 0, 0, 0) };
        campo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        campo.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        campo.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        campo.Controls.Add(new Label { Text = legenda, Dock = DockStyle.Fill, ForeColor = paleta.TextoSecundario, Tag = "muted", TextAlign = ContentAlignment.BottomLeft }, 0, 0);
        return campo;
    }

    private void ConstruirBarraFiltros()
    {
        barraFiltros.Dock = DockStyle.Fill;
        barraFiltros.FlowDirection = FlowDirection.LeftToRight;
        barraFiltros.WrapContents = true;
        barraFiltros.AutoScroll = true;
        barraFiltros.Padding = new Padding(0, DesignSystem.Espaco8, 0, 0);
        ConfigurarPesquisa();
        ConfigurarFiltro(cmbFiltroCategoria, 136, "Todas");
        ConfigurarFiltro(cmbFiltroEstado, 164, "Todos");
        ConfigurarBotao(btnEditar, "secundario"); btnEditar.Text = "Editar";
        ConfigurarBotao(btnRemover, "destrutivo"); btnRemover.Text = "Remover";
        ConfigurarBotao(btnImportar, "secundario"); btnImportar.Text = "Importar CSV";
        ConfigurarBotao(btnExportar, "secundario"); btnExportar.Text = "Exportar CSV";
        ConfigurarBotao(btnHistorico, "secundario"); btnHistorico.Text = "Histórico";
        foreach (var botao in new[] { btnEditar, btnRemover, btnImportar, btnExportar, btnHistorico }) barraFiltros.Controls.Add(botao);
        layoutInventario.Controls.Add(barraFiltros, 0, 3);
    }

    private void ConfigurarPesquisa()
    {
        txtPesquisa.Width = 214;
        txtPesquisa.PlaceholderText = "Pesquisar código, nome, marca…";
        txtPesquisa.Margin = new Padding(0, 0, DesignSystem.Espaco8, 0);
        txtPesquisa.BorderStyle = BorderStyle.FixedSingle;
        barraFiltros.Controls.Add(txtPesquisa);
        ConfigurarFiltro(cmbFiltroCategoria, 136, "Todas");
        ConfigurarFiltro(cmbFiltroEstado, 164, "Todos");
    }

    private void ConfigurarFiltro(ComboBox combo, int largura, string primeiraOpcao)
    {
        combo.Width = largura;
        combo.Height = DesignSystem.AlturaCampo;
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.ItemHeight = DesignSystem.AlturaCampo - 9;
        combo.Margin = new Padding(0, 0, DesignSystem.Espaco8, 0);
        combo.DrawItem += Combo_DrawItem;
        combo.Tag = primeiraOpcao;
        barraFiltros.Controls.Add(combo);
    }

    private void ConstruirGrelha()
    {
        painelGrelha.Dock = DockStyle.Fill;
        painelGrelha.Tag = "cartao";
        painelGrelha.Padding = new Padding(DesignSystem.Espaco8);
        grelha.Dock = DockStyle.Fill;
        grelha.AllowUserToAddRows = false;
        grelha.AllowUserToDeleteRows = false;
        grelha.ReadOnly = true;
        grelha.MultiSelect = true;
        grelha.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grelha.AutoGenerateColumns = false;
        grelha.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grelha.RowHeadersVisible = false;
        grelha.BorderStyle = BorderStyle.None;
        grelha.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grelha.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grelha.EnableHeadersVisualStyles = false;
        grelha.RowTemplate.Height = 42;
        grelha.Columns.Add(Coluna("Codigo", "Código", 8));
        grelha.Columns.Add(Coluna("Nome", "Nome do equipamento", 20));
        grelha.Columns.Add(Coluna("Categoria", "Categoria", 10));
        grelha.Columns.Add(Coluna("Marca", "Marca", 9));
        grelha.Columns.Add(Coluna("Modelo", "Modelo", 10));
        grelha.Columns.Add(Coluna("Estado", "Estado", 13));
        grelha.Columns.Add(Coluna("Data", "Entrada", 9));
        grelha.Columns.Add(Coluna("Compra", "Compra", 9, true));
        grelha.Columns.Add(Coluna("Venda", "Venda", 9, true));
        grelha.Columns.Add(Coluna("Margem", "Margem", 9, true));
        painelGrelha.Controls.Add(grelha);
        estadoVazio.Dock = DockStyle.Fill;
        estadoVazio.Text = "Ainda não há equipamentos\r\nAdiciona o primeiro equipamento no formulário acima.";
        estadoVazio.TextAlign = ContentAlignment.MiddleCenter;
        estadoVazio.Visible = false;
        estadoVazio.BringToFront();
        painelGrelha.Controls.Add(estadoVazio);
        layoutInventario.Controls.Add(painelGrelha, 0, 4);
    }

    private static DataGridViewTextBoxColumn Coluna(string nome, string titulo, float peso, bool dinheiro = false)
    {
        var coluna = new DataGridViewTextBoxColumn { Name = nome, HeaderText = titulo, FillWeight = peso, SortMode = DataGridViewColumnSortMode.Programmatic };
        if (dinheiro)
        {
            coluna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            coluna.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        return coluna;
    }

    private void ConstruirDefinicoes()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = paleta.Fundo };
        paginaDefinicoes.Controls.Add(scroll);
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 3, Padding = new Padding(DesignSystem.Espaco24) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 246));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
        scroll.Controls.Add(layout);

        var aparencia = CriarGrupo("Aparência", 0, 0);
        var aparenciaGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(DesignSystem.Espaco16) };
        aparenciaGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); aparenciaGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        AdicionarDefinicao(aparenciaGrid, "Tema", cmbTema, new[] { "Claro", "Escuro" }, 0);
        AdicionarDefinicao(aparenciaGrid, "Tamanho do texto", cmbTamanho, new[] { "Normal", "Grande", "Muito grande" }, 1);
        AdicionarDefinicao(aparenciaGrid, "Moeda", cmbMoeda, new[] { "EUR (€)", "USD ($)", "GBP (£)" }, 2);
        aparencia.Controls.Add(aparenciaGrid);

        var categorias = CriarGrupo("Categorias", 0, 1);
        ConstruirGestorLista(categorias, listaCategorias, txtNovaCategoria, "Nova categoria", true);
        var estados = CriarGrupo("Estados de recondicionamento", 1, 1);
        ConstruirGestorLista(estados, listaEstados, txtNovoEstado, "Novo estado", false);

        var baseDados = CriarGrupo("Base de dados e segurança", 0, 2, 2);
        var opcoesDados = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(DesignSystem.Espaco16) };
        lblLocalBaseDados.Text = repositorio.Caminho;
        lblLocalBaseDados.Dock = DockStyle.Fill;
        lblLocalBaseDados.AutoEllipsis = true;
        lblLocalBaseDados.Tag = "muted";
        opcoesDados.Controls.Add(lblLocalBaseDados, 0, 0);
        var acoes = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        var btnAbrir = NovoBotao("Abrir pasta"); var btnBackup = NovoBotao("Criar cópia"); var btnRestaurar = NovoBotao("Restaurar cópia");
        acoes.Controls.AddRange(new Control[] { btnAbrir, btnBackup, btnRestaurar });
        btnAbrir.Click += (_, _) => Process.Start(new ProcessStartInfo { FileName = pastaDados, UseShellExecute = true });
        btnBackup.Click += (_, _) => CriarCopiaSeguranca();
        btnRestaurar.Click += (_, _) => RestaurarCopiaSeguranca();
        opcoesDados.Controls.Add(acoes, 0, 1);
        opcoesDados.Controls.Add(new Label { Text = "A base SQLite é atualizada automaticamente após cada alteração.", Dock = DockStyle.Fill, Tag = "muted", TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        baseDados.Controls.Add(opcoesDados);
    }

    private Panel CriarGrupo(string titulo, int coluna, int linha, int largura = 1)
    {
        var panel = CriarCartao();
        panel.Padding = new Padding(DesignSystem.Espaco16);
        var box = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        box.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); box.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        box.Controls.Add(new Label { Text = titulo, Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 12), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        panel.Controls.Add(box);
        var parent = (TableLayoutPanel)paginaDefinicoes.Controls[0].Controls[0];
        parent.Controls.Add(panel, coluna, linha);
        if (largura == 2) parent.SetColumnSpan(panel, 2);
        return panel;
    }

    private void ConstruirGestorLista(Panel painel, ListBox lista, TextBox caixaNova, string placeholder, bool categoria)
    {
        var host = (TableLayoutPanel)painel.Controls[0];
        var conteudo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        conteudo.RowStyles.Add(new RowStyle(SizeType.Percent, 72)); conteudo.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
        lista.Dock = DockStyle.Fill; lista.IntegralHeight = false;
        conteudo.Controls.Add(lista, 0, 0);
        var acoes = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        caixaNova.Width = 190; caixaNova.PlaceholderText = placeholder;
        var adicionar = NovoBotao("Adicionar"); var remover = NovoBotao("Remover");
        acoes.Controls.AddRange(new Control[] { caixaNova, adicionar, remover });
        adicionar.Click += (_, _) =>
        {
            var valor = caixaNova.Text.Trim(); if (valor.Length == 0) return;
            if (categoria) repositorio.GuardarCategoria(valor); else repositorio.GuardarEstado(valor);
            caixaNova.Clear(); AtualizarListasDefinicoes(); AtualizarCombos(); Notificar(categoria ? "Categoria adicionada" : "Estado adicionado");
        };
        remover.Click += (_, _) =>
        {
            if (lista.SelectedItem is not string valor) return;
            var removido = categoria ? repositorio.RemoverCategoria(valor) : repositorio.RemoverEstado(valor);
            if (!removido) MessageBox.Show("Este valor está a ser usado por equipamentos e não pode ser removido.", "Valor em utilização", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else { AtualizarListasDefinicoes(); AtualizarCombos(); Notificar("Opção removida"); }
        };
        conteudo.Controls.Add(acoes, 0, 1); host.Controls.Add(conteudo, 0, 1);
    }

    private void AdicionarDefinicao(TableLayoutPanel painel, string texto, ComboBox combo, string[] opcoes, int linha)
    {
        painel.Controls.Add(new Label { Text = texto, Dock = DockStyle.Fill, Tag = "muted", TextAlign = ContentAlignment.MiddleLeft }, 0, linha);
        ConfigurarComboVisual(combo); combo.Items.AddRange(opcoes.Cast<object>().ToArray()); combo.Dock = DockStyle.Fill;
        painel.Controls.Add(combo, 1, linha);
    }

    private Button NovoBotao(string texto)
    {
        var button = new Button { Text = texto, AutoSize = true, MinimumSize = new Size(84, DesignSystem.AlturaBotao), Margin = new Padding(DesignSystem.Espaco8, 0, 0, 0) };
        ConfigurarBotao(button, "secundario");
        return button;
    }

    private Panel CriarCartao()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = paleta.Cartao, Tag = "cartao", Margin = new Padding(0, 0, DesignSystem.Espaco12, DesignSystem.Espaco12) };
        panel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(paleta.Borda);
            using var path = CaminhoRedondo(new Rectangle(0, 0, panel.Width - 1, panel.Height - 1), 10);
            e.Graphics.DrawPath(pen, path);
        };
        panel.SizeChanged += (_, _) => AplicarCantos(panel, DesignSystem.RaioCartao);
        return panel;
    }

    private void ConfigurarComboVisual(ComboBox combo)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.ItemHeight = DesignSystem.AlturaCampo - 9;
        combo.DrawItem += Combo_DrawItem;
    }

    private void ConfigurarBotao(Button button, string variante)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Font = new Font("Segoe UI Semibold", DesignSystem.FontePequena + 1);
        button.Height = DesignSystem.AlturaBotao;
        button.Cursor = Cursors.Hand;
        button.Tag = variante;
        AplicarEstiloBotao(button);
        button.SizeChanged += (_, _) => AplicarCantos(button, DesignSystem.RaioBotao);
    }

    private void FormularioInventario_Load(object? sender, EventArgs e)
    {
        inicializando = true;
        temaEscuro = repositorio.ObterDefinicao("tema") == "escuro";
        int.TryParse(repositorio.ObterDefinicao("tamanhoFonte", "1"), out tamanhoFonte);
        tamanhoFonte = Math.Clamp(tamanhoFonte, 0, 2);
        moeda = repositorio.ObterDefinicao("moeda", "EUR");
        cmbTema.Items.AddRange(new object[] { "Claro", "Escuro" });
        cmbTamanho.Items.AddRange(new object[] { "Normal", "Grande", "Muito grande" });
        cmbMoeda.Items.AddRange(new object[] { "EUR (€)", "USD ($)", "GBP (£)" });
        cmbTema.SelectedIndex = temaEscuro ? 1 : 0;
        cmbTamanho.SelectedIndex = tamanhoFonte;
        cmbMoeda.SelectedIndex = moeda switch { "USD" => 1, "GBP" => 2, _ => 0 };
        AtualizarListasDefinicoes(); AtualizarCombos();
        inicializando = false;
        AplicarTema(); AplicarTamanhoFonte(); AtualizarGrelha(); AtualizarResumo(); AtualizarEstadoBotoes();
        Notificar("Base SQLite pronta");
    }

    private void AtualizarCombos()
    {
        var categoria = cmbCategoria.SelectedItem?.ToString(); var estado = cmbEstado.SelectedItem?.ToString();
        var categoriaFiltro = cmbFiltroCategoria.SelectedItem?.ToString() ?? "Todas";
        var estadoFiltro = cmbFiltroEstado.SelectedItem?.ToString() ?? "Todos";
        PopularCombo(cmbCategoria, repositorio.ObterCategorias(), categoria);
        PopularCombo(cmbEstado, repositorio.ObterEstados(), estado);
        PopularCombo(cmbFiltroCategoria, new[] { "Todas" }.Concat(repositorio.ObterCategorias()).ToList(), categoriaFiltro);
        PopularCombo(cmbFiltroEstado, new[] { "Todos" }.Concat(repositorio.ObterEstados()).ToList(), estadoFiltro);
    }

    private static void PopularCombo(ComboBox combo, IEnumerable<string> itens, string? selecionar)
    {
        combo.BeginUpdate(); combo.Items.Clear(); combo.Items.AddRange(itens.Cast<object>().ToArray());
        var indice = selecionar == null ? -1 : combo.Items.IndexOf(selecionar);
        combo.SelectedIndex = indice >= 0 ? indice : (combo.Items.Count > 0 ? 0 : -1);
        combo.EndUpdate();
    }

    private void AtualizarListasDefinicoes()
    {
        listaCategorias.Items.Clear(); listaCategorias.Items.AddRange(repositorio.ObterCategorias().Cast<object>().ToArray());
        listaEstados.Items.Clear(); listaEstados.Items.AddRange(repositorio.ObterEstados().Cast<object>().ToArray());
    }

    private void AtualizarGrelha()
    {
        if (inicializando || repositorio == null) return;
        var categoria = cmbFiltroCategoria.SelectedItem?.ToString() ?? "Todas";
        var estado = cmbFiltroEstado.SelectedItem?.ToString() ?? "Todos";
        visiveis = servico.Pesquisar(txtPesquisa.Text, categoria, estado, ordenarPor, ordemDescendente);
        grelha.Rows.Clear();
        foreach (var e in visiveis)
        {
            var row = grelha.Rows.Add(e.Codigo, e.Nome, e.Categoria, e.Marca, e.Modelo, e.Estado,
                e.DataEntrada.ToString("d", CultureInfo.GetCultureInfo("pt-PT")), FormatarMoeda(e.PrecoCompra), FormatarMoeda(e.PrecoVenda), FormatarMoeda(e.MargemPrevista));
            grelha.Rows[row].Tag = e;
        }
        estadoVazio.Visible = visiveis.Count == 0;
        estadoVazio.Text = repositorio.ObterEquipamentos().Count == 0
            ? "Ainda não há equipamentos\r\nAdiciona o primeiro equipamento no formulário acima."
            : "Não foram encontrados equipamentos\r\nAltera a pesquisa ou os filtros.";
        estadoVazio.BringToFront();
        AtualizarEstadoBotoes(); AtualizarResumo();
    }

    private void GuardarFormulario()
    {
        erros.Clear();
        var valido = true;
        if (string.IsNullOrWhiteSpace(txtNome.Text)) { erros.SetError(txtNome, "O nome é obrigatório."); valido = false; }
        if (cmbCategoria.SelectedItem == null) { erros.SetError(cmbCategoria, "Escolhe uma categoria."); valido = false; }
        if (cmbEstado.SelectedItem == null) { erros.SetError(cmbEstado, "Escolhe um estado."); valido = false; }
        if (!LerPreco(txtCompra.Text, out var compra)) { erros.SetError(txtCompra, "Indica um preço de compra válido."); valido = false; }
        if (!LerPreco(txtVenda.Text, out var venda)) { erros.SetError(txtVenda, "Indica um preço de venda válido."); valido = false; }
        if (!valido) { Notificar("Verifica os campos assinalados"); return; }

        var anterior = idEmEdicao == 0 ? null : repositorio.ObterEquipamentos().FirstOrDefault(e => e.Id == idEmEdicao);
        var equipamento = new Equipamento
        {
            Id = idEmEdicao, Codigo = lblCodigo.Text.StartsWith("EQ-") ? lblCodigo.Text : "",
            Nome = txtNome.Text.Trim(), Categoria = cmbCategoria.SelectedItem!.ToString()!, Estado = cmbEstado.SelectedItem!.ToString()!,
            Marca = txtMarca.Text.Trim(), Modelo = txtModelo.Text.Trim(), NumeroSerie = txtSerie.Text.Trim(),
            DataEntrada = dataEntrada.Value.Date, Notas = txtNotas.Text.Trim(), PrecoCompra = compra, PrecoVenda = venda
        };
        repositorio.Guardar(equipamento, txtNotas.Text.Trim());
        LimparFormulario(); AtualizarGrelha(); Notificar(anterior == null ? $"{equipamento.Codigo} adicionado" : $"{equipamento.Codigo} guardado");
    }

    private static bool LerPreco(string texto, out decimal valor)
    {
        texto = texto.Trim().Replace("€", "").Replace("$", "").Replace("£", "").Trim();
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-PT"), out valor)
            || decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    }

    private string FormatarMoeda(decimal valor)
    {
        var cultura = CultureInfo.GetCultureInfo("pt-PT");
        var simbolo = moeda switch { "USD" => "$", "GBP" => "£", _ => "€" };
        return valor.ToString("N2", cultura) + " " + simbolo;
    }

    private void AtualizarMargem()
    {
        var okCompra = LerPreco(txtCompra.Text, out var compra); var okVenda = LerPreco(txtVenda.Text, out var venda);
        lblMargem.Text = okCompra && okVenda ? FormatarMoeda(venda - compra) : "—";
    }

    private void AtualizarResumo()
    {
        if (repositorio == null) return;
        var r = repositorio.ObterResumo();
        lblResumoTotal.Text = r.Total.ToString(CultureInfo.GetCultureInfo("pt-PT"));
        lblResumoStock.Text = FormatarMoeda(r.ValorEmStock);
        lblResumoMargem.Text = FormatarMoeda(r.MargemPrevista);
        lblResumoEstados.Text = $"Avaliar {r.PorAvaliar}   ·   Reparação {r.EmReparacao}   ·   Pronto {r.ProntoVenda}\r\nVendido {r.Vendidos}   ·   Descartado {r.Descartados}";
    }

    private void EditarSelecionado()
    {
        if (grelha.SelectedRows.Count == 0) return;
        if (grelha.SelectedRows.Count > 1) { Notificar("Seleciona apenas um equipamento para editar"); return; }
        if (grelha.SelectedRows[0].Tag is not Equipamento e) return;
        idEmEdicao = e.Id;
        lblCodigo.Text = e.Codigo;
        txtNome.Text = e.Nome; cmbCategoria.SelectedItem = e.Categoria; cmbEstado.SelectedItem = e.Estado;
        txtMarca.Text = e.Marca; txtModelo.Text = e.Modelo; txtSerie.Text = e.NumeroSerie;
        dataEntrada.Value = e.DataEntrada; txtCompra.Text = e.PrecoCompra.ToString("0.00", CultureInfo.GetCultureInfo("pt-PT"));
        txtVenda.Text = e.PrecoVenda.ToString("0.00", CultureInfo.GetCultureInfo("pt-PT")); txtNotas.Text = e.Notas;
        btnGuardar.Text = "Guardar alterações"; btnCancelar.Visible = true; AtualizarMargem(); txtNome.Focus();
        Notificar($"A editar {e.Codigo}");
    }

    private void LimparFormulario()
    {
        idEmEdicao = 0; lblCodigo.Text = "Será atribuído ao guardar";
        txtNome.Clear(); txtMarca.Clear(); txtModelo.Clear(); txtSerie.Clear(); txtCompra.Clear(); txtVenda.Clear(); txtNotas.Clear();
        if (cmbCategoria.Items.Count > 0) cmbCategoria.SelectedIndex = 0;
        if (cmbEstado.Items.Count > 0) cmbEstado.SelectedIndex = 0;
        dataEntrada.Value = DateTime.Today; btnGuardar.Text = "Adicionar equipamento"; btnCancelar.Visible = false;
        erros.Clear(); txtNome.Focus();
    }

    private void RemoverSelecionados()
    {
        var selecionados = grelha.SelectedRows.Cast<DataGridViewRow>().Select(r => (r.Tag as Equipamento)?.Id ?? 0).Where(id => id > 0).Distinct().ToList();
        if (selecionados.Count == 0) return;
        var confirmacao = MessageBox.Show($"Remover {selecionados.Count} equipamento(s)? Poderás desfazer esta ação.", "Confirmar remoção", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmacao != DialogResult.Yes) return;
        repositorio.Remover(selecionados); btnDesfazer.Visible = true; AtualizarGrelha(); Notificar($"{selecionados.Count} equipamento(s) removido(s)");
    }

    private void DesfazerRemocao()
    {
        var quantidade = repositorio.DesfazerUltimaRemocao();
        btnDesfazer.Visible = false; AtualizarGrelha(); Notificar(quantidade > 0 ? $"Remoção desfeita ({quantidade})" : "Não há remoção para desfazer");
    }

    private void MostrarHistorico()
    {
        if (grelha.SelectedRows.Count == 0 || grelha.SelectedRows[0].Tag is not Equipamento e) return;
        var linhas = repositorio.ObterHistorico(e.Id);
        var texto = linhas.Count == 0 ? "Sem alterações de estado registadas." : string.Join(Environment.NewLine, linhas.Select(h => $"{h.Data:dd/MM/yyyy HH:mm}  {h.Estado}  {h.Nota}"));
        MessageBox.Show(texto, $"Histórico de {e.Codigo}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ImportarCsv()
    {
        using var dialogo = new OpenFileDialog { Filter = "CSV (*.csv)|*.csv", Title = "Importar equipamentos" };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var resultado = servico.ImportarCsv(dialogo.FileName, repositorio.ObterCategorias(), repositorio.ObterEstados());
            AtualizarGrelha();
            var resumo = $"Importados: {resultado.Importados}" + (resultado.Erros.Count > 0 ? $"\r\n\r\nErros ({resultado.Erros.Count}):\r\n" + string.Join("\r\n", resultado.Erros.Take(20)) : "");
            MessageBox.Show(resumo, resultado.Erros.Count > 0 ? "Relatório de importação" : "Importação concluída", MessageBoxButtons.OK, resultado.Erros.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            Notificar(resultado.Erros.Count > 0 ? "Importação concluída com erros" : "Equipamentos importados");
        }
        catch (Exception erro) { Notificar("Erro ao importar"); MessageBox.Show(erro.Message, "Erro ao importar", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ExportarCsv()
    {
        using var dialogo = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "inventario.csv", Title = "Exportar lista filtrada" };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        InventarioService.ExportarCsv(visiveis, dialogo.FileName); Notificar($"{visiveis.Count} linha(s) exportada(s)");
    }

    private void CriarCopiaSeguranca()
    {
        using var dialogo = new SaveFileDialog { Filter = "Base SQLite (*.db)|*.db", FileName = $"inventario-backup-{DateTime.Now:yyyyMMdd-HHmm}.db" };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        repositorio.Dispose(); File.Copy(Path.Combine(pastaDados, "inventario.db"), dialogo.FileName, true);
        repositorio = new InventarioRepository(Path.Combine(pastaDados, "inventario.db")); servico = new InventarioService(repositorio);
        Notificar("Cópia de segurança criada");
    }

    private void RestaurarCopiaSeguranca()
    {
        using var dialogo = new OpenFileDialog { Filter = "Base SQLite (*.db)|*.db", Title = "Restaurar cópia de segurança" };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        var confirmacao = MessageBox.Show("A base atual será substituída pela cópia selecionada. Continuar?", "Confirmar restauro", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirmacao != DialogResult.Yes) return;
        repositorio.RestaurarBaseDados(dialogo.FileName);
        temaEscuro = repositorio.ObterDefinicao("tema") == "escuro";
        int.TryParse(repositorio.ObterDefinicao("tamanhoFonte", "1"), out tamanhoFonte); tamanhoFonte = Math.Clamp(tamanhoFonte, 0, 2);
        moeda = repositorio.ObterDefinicao("moeda", "EUR");
        inicializando = true;
        cmbTema.SelectedIndex = temaEscuro ? 1 : 0; cmbTamanho.SelectedIndex = tamanhoFonte;
        cmbMoeda.SelectedIndex = moeda switch { "USD" => 1, "GBP" => 2, _ => 0 };
        inicializando = false;
        AplicarTema(); AplicarTamanhoFonte(); AtualizarListasDefinicoes(); AtualizarCombos(); AtualizarGrelha(); Notificar("Cópia restaurada");
    }

    private void GuardarDefinicoes()
    {
        repositorio.GuardarDefinicao("tema", temaEscuro ? "escuro" : "claro");
        repositorio.GuardarDefinicao("tamanhoFonte", tamanhoFonte.ToString(CultureInfo.InvariantCulture));
        repositorio.GuardarDefinicao("moeda", moeda);
    }

    private void DefinicoesAlteradas(object? sender, EventArgs e)
    {
        if (inicializando || cmbTema.SelectedIndex < 0 || cmbTamanho.SelectedIndex < 0 || cmbMoeda.SelectedIndex < 0) return;
        temaEscuro = cmbTema.SelectedIndex == 1; tamanhoFonte = cmbTamanho.SelectedIndex;
        moeda = cmbMoeda.SelectedIndex switch { 1 => "USD", 2 => "GBP", _ => "EUR" };
        AplicarTema(); AplicarTamanhoFonte(); AtualizarGrelha(); GuardarDefinicoes(); Notificar("Definições guardadas");
    }

    private void AplicarTema()
    {
        paleta = temaEscuro ? DesignSystem.Escura : DesignSystem.Clara;
        BackColor = paleta.Fundo; ForeColor = paleta.Texto;
        AplicarTemaControlo(this);
        btnTemaCabecalho.Text = temaEscuro ? "Tema claro" : "Tema escuro";
        foreach (var button in TodosBotoes(this)) AplicarEstiloBotao(button);
        grelha.BackgroundColor = paleta.Cartao; grelha.GridColor = paleta.Borda;
        grelha.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(temaEscuro ? 48 : 232, temaEscuro ? 64 : 240, temaEscuro ? 63 : 236);
        grelha.ColumnHeadersDefaultCellStyle.ForeColor = paleta.Texto;
        grelha.DefaultCellStyle.BackColor = paleta.Cartao; grelha.DefaultCellStyle.ForeColor = paleta.Texto;
        grelha.DefaultCellStyle.SelectionBackColor = paleta.Selecao; grelha.DefaultCellStyle.SelectionForeColor = paleta.Texto;
        grelha.AlternatingRowsDefaultCellStyle.BackColor = temaEscuro ? Color.FromArgb(42, 55, 55) : Color.FromArgb(246, 249, 247);
        barraEstado.BackColor = paleta.Cartao; barraEstado.ForeColor = paleta.Texto;
        foreach (Control c in Controls) if (c is TabControl tab) tab.Invalidate();
        if (IsHandleCreated) AplicarTemaBarraTitulo();
        Refresh();
    }

    private void AplicarTemaControlo(Control pai)
    {
        foreach (Control c in pai.Controls)
        {
            if (c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel)
                c.BackColor = DentroCartao(c) ? paleta.Cartao : paleta.Fundo;
            else if (c is TabControl || c is TabPage) { c.BackColor = paleta.Fundo; c.ForeColor = paleta.Texto; }
            else if (c is Label l) { l.ForeColor = l.Tag?.ToString() == "muted" ? paleta.TextoSecundario : paleta.Texto; l.BackColor = DentroCartao(l) ? paleta.Cartao : paleta.Fundo; }
            else if (c is TextBox t) { t.BackColor = paleta.Campo; t.ForeColor = paleta.Texto; }
            else if (c is ComboBox cb) { cb.BackColor = paleta.Campo; cb.ForeColor = paleta.Texto; }
            else if (c is ListBox lb) { lb.BackColor = paleta.Campo; lb.ForeColor = paleta.Texto; }
            if (c.HasChildren) AplicarTemaControlo(c);
        }
    }

    private void AplicarEstiloBotao(Button button)
    {
        var variante = button.Tag?.ToString() ?? "secundario";
        var primario = variante == "primario"; var destrutivo = variante == "destrutivo";
        var fundo = primario ? paleta.Destaque : destrutivo ? paleta.Destrutivo : paleta.Cartao;
        button.BackColor = button.Enabled ? fundo : paleta.Borda;
        button.ForeColor = !button.Enabled ? paleta.TextoSecundario : primario || destrutivo ? Color.White : paleta.Texto;
        button.FlatAppearance.MouseOverBackColor = primario ? paleta.DestaqueHover : destrutivo ? paleta.DestrutivoHover : paleta.Selecao;
        button.FlatAppearance.MouseDownBackColor = primario ? paleta.DestaqueHover : destrutivo ? paleta.DestrutivoHover : paleta.Borda;
        button.FlatAppearance.BorderColor = paleta.Borda;
        button.FlatAppearance.BorderSize = variante == "secundario" ? 1 : 0;
        button.Enabled = button == btnRemover ? grelha.SelectedRows.Count > 0
            : button == btnEditar || button == btnHistorico ? grelha.SelectedRows.Count == 1
            : button.Enabled;
        button.Cursor = button.Enabled ? Cursors.Hand : Cursors.Default;
    }

    private IEnumerable<Button> TodosBotoes(Control pai)
    {
        foreach (Control c in pai.Controls)
        {
            if (c is Button b) yield return b;
            foreach (var child in TodosBotoes(c)) yield return child;
        }
    }

    private bool DentroCartao(Control c)
    {
        for (var parent = c.Parent; parent != null && parent != this; parent = parent.Parent)
            if (parent.Tag?.ToString() == "cartao") return true;
        return c.Tag?.ToString() == "cartao";
    }

    private void AplicarTamanhoFonte()
    {
        var escala = tamanhoFonte switch { 0 => 1f, 2 => 1.22f, _ => 1.1f };
        foreach (var item in fontesBase) item.Key.Font = new Font(item.Value.FontFamily, item.Value.Size * escala, item.Value.Style);
        foreach (var item in tamanhosBaseBotoes) item.Key.Size = new Size((int)(item.Value.Width * escala), (int)(item.Value.Height * escala));
        grelha.RowTemplate.Height = (int)(42 * escala);
        grelha.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10 * escala);
        grelha.DefaultCellStyle.Font = new Font("Segoe UI", 10.5f * escala);
    }

    private void RegistarFontes(Control pai)
    {
        foreach (Control c in pai.Controls)
        {
            fontesBase[c] = (Font)c.Font.Clone();
            if (c is Button b) tamanhosBaseBotoes[b] = b.Size;
            if (c.HasChildren) RegistarFontes(c);
        }
    }

    private void AtualizarEstadoBotoes()
    {
        btnEditar.Enabled = btnHistorico.Enabled = grelha.SelectedRows.Count == 1;
        btnRemover.Enabled = grelha.SelectedRows.Count > 0;
        foreach (var b in new[] { btnEditar, btnRemover, btnHistorico }) { b.Cursor = b.Enabled ? Cursors.Hand : Cursors.Default; b.Invalidate(); }
    }

    private void Notificar(string mensagem)
    {
        temporizadorMensagem.Stop(); lblMensagem.Text = mensagem; temporizadorMensagem.Start();
    }

    private void Grelha_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        var coluna = grelha.Columns[e.ColumnIndex].Name;
        var campo = coluna switch { "Codigo" => "Código", "Categoria" => "Categoria", "Marca" => "Marca", "Modelo" => "Modelo", "Estado" => "Estado", "Compra" => "Preço de compra", "Venda" => "Preço de venda", "Data" => "Data de entrada", _ => "Nome" };
        ordemDescendente = ordenarPor == campo && !ordemDescendente; ordenarPor = campo;
        foreach (DataGridViewColumn c in grelha.Columns) c.HeaderText = c.HeaderText.TrimEnd(' ', '▲', '▼');
        grelha.Columns[e.ColumnIndex].HeaderText += ordemDescendente ? " ▼" : " ▲";
        AtualizarGrelha();
    }

    private void Grelha_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= grelha.Rows.Count || grelha.Rows[e.RowIndex].Tag is not Equipamento equipamento || e.CellStyle is not { } style) return;
        if (grelha.Columns[e.ColumnIndex].Name == "Estado")
        {
            style.BackColor = CorEstado(equipamento.Estado);
            style.ForeColor = temaEscuro ? Color.FromArgb(24, 31, 31) : Color.White;
            style.SelectionBackColor = paleta.Selecao;
            style.SelectionForeColor = paleta.Texto;
        }
    }

    private Color CorEstado(string estado) => estado switch
    {
        "Por avaliar" => temaEscuro ? Color.FromArgb(218, 183, 112) : Color.FromArgb(178, 132, 61),
        "Em reparação" => temaEscuro ? Color.FromArgb(141, 179, 207) : Color.FromArgb(75, 119, 151),
        "Pronto para venda" => temaEscuro ? Color.FromArgb(132, 190, 147) : Color.FromArgb(67, 128, 91),
        "Vendido" => paleta.Neutro,
        _ => temaEscuro ? Color.FromArgb(208, 139, 132) : Color.FromArgb(166, 83, 77)
    };

    private void Combo_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Index < 0) return;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var brush = new SolidBrush(selected ? paleta.Selecao : paleta.Campo);
        e.Graphics.FillRectangle(brush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, combo.Items[e.Index]?.ToString() ?? "", combo.Font, e.Bounds,
            paleta.Texto, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void Abas_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tab || e.Index < 0) return;
        var ativa = e.Index == tab.SelectedIndex;
        using var brush = new SolidBrush(ativa ? paleta.Cartao : paleta.Fundo);
        e.Graphics.FillRectangle(brush, e.Bounds);
        TextRenderer.DrawText(e.Graphics, tab.TabPages[e.Index].Text, tab.Font, e.Bounds,
            ativa ? paleta.Destaque : paleta.TextoSecundario, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (ativa) { using var pen = new Pen(paleta.Destaque, 2); e.Graphics.DrawLine(pen, e.Bounds.Left + 18, e.Bounds.Bottom - 2, e.Bounds.Right - 18, e.Bounds.Bottom - 2); }
    }

    private void Formulario_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.N) { LimparFormulario(); abas.SelectedTab = paginaInventario; e.SuppressKeyPress = true; }
        else if (e.Control && e.KeyCode == Keys.F) { txtPesquisa.Focus(); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Delete && grelha.ContainsFocus) { RemoverSelecionados(); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Escape) { LimparFormulario(); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Enter && cartaoFormulario.ContainsFocus && !txtNotas.Focused) { GuardarFormulario(); e.SuppressKeyPress = true; }
    }

    private void AplicarTemaBarraTitulo()
    {
        if (!IsHandleCreated) return;
        var valor = temaEscuro ? 1 : 0;
        if (DwmSetWindowAttribute(Handle, 20, ref valor, sizeof(int)) != 0)
            DwmSetWindowAttribute(Handle, 19, ref valor, sizeof(int));
    }

    private static void AplicarCantos(Control control, int raio)
    {
        if (control.Width < 2 || control.Height < 2) return;
        using var path = CaminhoRedondo(new Rectangle(0, 0, control.Width, control.Height), raio);
        control.Region = new Region(path);
    }

    private static GraphicsPath CaminhoRedondo(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath(); var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90); path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90); path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }

    private static GraphicsPath CaminhoRedondo(Rectangle bounds, float radius) => CaminhoRedondo(bounds, (int)radius);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);
}

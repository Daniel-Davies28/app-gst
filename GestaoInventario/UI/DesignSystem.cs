using System.Drawing;

namespace GestaoInventario.UI;

public sealed record Paleta(Color Fundo, Color Cartao, Color Campo, Color Texto, Color TextoSecundario,
    Color Borda, Color Destaque, Color DestaqueHover, Color Selecao, Color Destrutivo,
    Color DestrutivoHover, Color Verde, Color Ambar, Color Azul, Color Neutro);

public static class DesignSystem
{
    public const int Espaco8 = 8;
    public const int Espaco12 = 12;
    public const int Espaco16 = 16;
    public const int Espaco24 = 24;
    public const int AlturaCampo = 36;
    public const int AlturaBotao = 38;
    public const int RaioCartao = 10;
    public const int RaioBotao = 8;
    public const float FontePequena = 9f;
    public const float FonteCorpo = 11f;
    public const float FonteSecao = 12f;
    public const float FonteTitulo = 23f;
    public const float FonteResumo = 17f;

    public static readonly Paleta Clara = new(
        Color.FromArgb(242, 246, 244), Color.FromArgb(252, 253, 252), Color.FromArgb(246, 249, 247),
        Color.FromArgb(39, 55, 54), Color.FromArgb(102, 120, 116), Color.FromArgb(218, 228, 223),
        Color.FromArgb(63, 122, 107), Color.FromArgb(49, 102, 90), Color.FromArgb(213, 235, 227),
        Color.FromArgb(169, 84, 78), Color.FromArgb(149, 66, 62), Color.FromArgb(69, 133, 99),
        Color.FromArgb(164, 122, 57), Color.FromArgb(74, 119, 152), Color.FromArgb(110, 126, 121));

    public static readonly Paleta Escura = new(
        Color.FromArgb(28, 37, 38), Color.FromArgb(38, 50, 51), Color.FromArgb(45, 59, 59),
        Color.FromArgb(231, 239, 235), Color.FromArgb(174, 190, 184), Color.FromArgb(57, 73, 72),
        Color.FromArgb(111, 171, 150), Color.FromArgb(130, 188, 167), Color.FromArgb(54, 78, 69),
        Color.FromArgb(195, 112, 105), Color.FromArgb(214, 130, 121), Color.FromArgb(116, 184, 139),
        Color.FromArgb(215, 177, 105), Color.FromArgb(133, 174, 205), Color.FromArgb(166, 183, 177));
}

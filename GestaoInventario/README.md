# app-gst

## Gestão de inventário de equipamentos recondicionados

Aplicação desktop Windows Forms em C# para gerir computadores recondicionados. O projeto abre no Visual Studio com o .NET 8 e Windows Forms.

## Executar

Para execução direta, extrai o ZIP `GestaoInventario-Windows-x64.zip` e abre `Iniciar.bat`. É necessário ter instalado o **.NET 8 Desktop Runtime para Windows x64**. Para desenvolver, abre `GestaoInventario.sln` no Visual Studio 2022 com a carga de trabalho **Desenvolvimento para desktop com .NET**.

## Funcionalidades

- Base SQLite local em `%LOCALAPPDATA%\SIAInventario\inventario.db`, gravada automaticamente. A base JSON da versão anterior é migrada no primeiro arranque e preservada no disco.
- Código automático `EQ-0001`, marca, modelo, número de série, data de entrada, notas, estado, preços de compra e venda e margem prevista.
- Pesquisa e filtros combináveis, ordenação por cabeçalho, seleção múltipla, edição, remoção com confirmação e desfazer.
- Histórico de estado, resumo de stock, importação CSV validada, exportação da lista filtrada, cópia e restauro da base.
- Preferências de tema, tamanho de letra e símbolo monetário guardadas na base de dados.
- Tabelas reservadas para fotografias, checklists, fornecedores, clientes, vendas e utilizadores, sem funcionalidades ativas para esses módulos.

Os ficheiros estão separados por `Models`, `Data`, `Services` e `UI`. A aplicação usa SQLite nativo do Windows (`winsqlite3.dll`), sem dependências NuGet adicionais.

## Localização dos dados

A base ativa fica em `%LOCALAPPDATA%\SIAInventario\inventario.db`. A cópia de segurança pode ser criada e restaurada no separador **Definições**. Uma base JSON da versão anterior é importada automaticamente no primeiro arranque; o ficheiro original é mantido.

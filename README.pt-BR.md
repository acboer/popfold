<p align="center"><img src="docs/images/logo.png" width="96" alt="Logo do Popfold"></p>

# Popfold

**Seus apps, num pop.** Pastas estilo Android/iOS na área de trabalho e na barra de tarefas do Windows.

**[⬇ Baixar para Windows](../../releases/latest)** · [Read in English](README.md)

## Instalar

1. Baixe o **Popfold-Setup-x.y.z.exe** na [versão mais recente](../../releases/latest).
2. Abra o arquivo. Como o Popfold ainda não tem assinatura digital, o Windows pode mostrar
   "O Windows protegeu o computador": clique em **Mais informações** → **Executar assim mesmo**.
3. O Popfold abre com algumas pastas de exemplo. Botão direito numa pasta mostra as opções.

Requer Windows 10 (1809) ou 11, 64 bits. A opção **Novo > Popfold** do botão direito aparece depois de
reiniciar o computador uma vez.

## Como usar

- **Clique** numa pasta: abre o painel com os itens.
- **Clique num item**: ele abre e o painel se fecha. Nada passa pelo Explorador.
- **Clique fora** ou **Esc**: o painel se fecha.
- **Arraste** programas, atalhos, arquivos, pastas ou links do navegador **até o ícone da pasta**.
  Os atalhos (.lnk/.url) são copiados para dentro do app, então você pode apagar o original da área de trabalho.
- **Arraste a pasta** para mudar ela de lugar.
- **Arraste um item dentro do painel** para mudar a ordem (a prévia 2×2 mostra os quatro primeiros), ou
  **solte-o em outra pasta** para mover.
- **Clique no nome** dentro do painel para renomear a pasta.
- **Botão direito na pasta**: renomear, nova pasta, excluir, sair.
- **Botão direito num item**: executar como administrador, abrir o local do arquivo, renomear, remover.
- **Ícone na bandeja**: nova pasta, opções, iniciar com o Windows, procurar atualizações, pasta de dados, sair.
- **Versão nova**: uma vez por dia o Popfold pergunta ao GitHub se saiu versão nova e avisa na bandeja
  (clique no aviso para abrir a página de download). Só lê a lista pública de versões: não envia nada sobre você.
  Dá para desligar nas Opções.
- **Selecionar várias**: Ctrl+clique nas pastas ou arraste uma seleção na área de trabalho. Arrastar uma pasta selecionada move todas juntas.
- **Opções** (botão direito na pasta ou duplo clique na bandeja): transparência, efeito vidro (desfoca o papel de parede), estilo claro/escuro, idioma (português/inglês), iniciar com o Windows e avisar sobre versões novas.

As pastas continuam visíveis com Win+D ("Mostrar área de trabalho").

- **Na barra de tarefas**: botão direito na pasta → *Fixar na barra de tarefas…*. O Popfold cria um atalho com o
  ícone 2×2 da pasta (menu Iniciar > Popfold) e abre o local dele; botão direito no atalho → *Mostrar mais opções* →
  *Fixar na barra de tarefas* (o Windows 11 não deixa programas se fixarem sozinhos). Clicar no ícone fixado abre o
  painel logo acima dele; clicar de novo recolhe. Botão direito no ícone fixado lista os itens da pasta.
  O ícone, o nome e a lista se atualizam quando a pasta muda (a barra pode demorar um pouco para redesenhar o ícone).
- **Novo > Popfold**: botão direito na área de trabalho → Novo → Popfold cria uma pasta onde você clicou.
  (O Explorer só mostra a opção nova depois de reiniciar o Explorer ou o Windows uma vez.)

## Compilar e rodar

Requer o .NET SDK 10.

```powershell
dotnet build -c Release
.\bin\Release\net10.0-windows\Popfold.exe
```

## Instalador

```powershell
.\tools\build-installer.ps1
```

Requer o Inno Setup 6. Publica o app com o .NET embutido (quem instala não precisa de mais nada) e gera
`dist\Popfold-Setup-<versão>.exe` (~50 MB) a partir de `installer\Popfold.iss`. O instalador:

- instala em `Arquivos de Programas\Popfold` (pede administrador por causa do item Novo > Popfold);
- cria o atalho no menu Iniciar, registra Novo > Popfold e (opcional, marcado) Iniciar com o Windows;
- detecta português/inglês pelo idioma do Windows;
- na desinstalação, fecha o app, remove registros e atalhos e pergunta se apaga as pastas do usuário.

O executável não é assinado: na primeira vez o Windows pode mostrar "O Windows protegeu o computador" →
*Mais informações* → *Executar assim mesmo*.

## Dados

Ficam em `%AppData%\Popfold`:

- `config.json`: grupos, posições e itens
- `shortcuts\`: cópias dos atalhos arrastados
- `icons\`: cache dos favicons dos sites, buscados direto no próprio site

## Identidade visual (Bala de Goma)

- Cores: Chiclete `#FF7AB6`, Lavanda `#B79CFF`, Menta `#7FE3C4`, Limonada `#FFE27A`, Uva `#2B1B4A`, fundo `#FFF4F8`.
- Fontes: Fredoka (títulos) e Nunito (textos), embutidas no app a partir de `fonts\` (licença SIL OFL; textos em `fonts\OFL-*.txt`, que devem acompanhar o instalador).
- `Theme.xaml`: cores, fontes e controles (interruptor, seletor em pílula, slider, botão).
- `tools\make-icon.ps1`: gera `Popfold.ico` e os PNGs em `branding\icons\` (tamanhos pequenos simplificados).
- `branding\installer\`: arte do assistente de instalação (HTML de origem + PNG/BMP em 100% e 250%),
  gerada por `tools\render-installer-art.ps1` (usa o Edge em modo headless).

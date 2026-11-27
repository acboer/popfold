# Popfold: notas de desenvolvimento

Tudo o que foi decidido e aprendido no desenvolvimento do Popfold até a versão 0.4.0, para continuar o trabalho
em outra conta (ou outro computador) sem perder o contexto.
As regras curtas para o Claude Code estão em [`CLAUDE.md`](../CLAUDE.md); este arquivo é a versão completa.

## 1. O que é

Popfold é um app para Windows 10/11 que cria pastas no estilo Android/iOS na área de trabalho e na barra de
tarefas. Cada pasta mostra uma prévia 2×2 dos apps; um clique abre um painel compacto com os itens, e um clique
num item o executa.

- Repositório público: <https://github.com/acboer/popfold> (licença MIT).
- Versão atual: **0.4.0** (0.3.0 foi a primeira pública, 25/11/2026).
- O nome antigo do projeto era "Smart Folders"; o app migra sozinho os dados da pasta antiga (`ConfigStore.cs`).

## 2. Ambiente

| Item | Onde / versão |
| --- | --- |
| Código | `E:\Smart Folders\SmartFolders` (repositório git, remoto `origin` = GitHub) |
| .NET SDK | 10 (`dotnet --version` → 10.0.404) |
| Inno Setup 6 | `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe` (instalação por usuário) |
| GitHub CLI | `gh`, logado na conta `acboer` |
| App instalado | `C:\Program Files\Popfold` |
| Dados do usuário | `%AppData%\Popfold` |

Trocar a conta do Claude **não** mexe no git, no `gh` nem nos arquivos: o login do GitHub continua o mesmo.
A memória automática do Claude fica na conta antiga; o que importava dela está neste documento e no `CLAUDE.md`.

## 3. Compilar, testar e lançar versão

```powershell
dotnet build -c Release                     # compila
.\tools\build-installer.ps1                 # publish self-contained + Inno Setup → dist\Popfold-Setup-<versão>.exe
```

**Testar:** abra o `bin\Release\net10.0-windows\Popfold.exe` pelo Explorador ou por um terminal comum, **nunca de
dentro do Claude** (ver seção 6). Só pode rodar uma instância (mutex `Local\Popfold.SingleInstance`): feche o
Popfold instalado (ícone da bandeja → *Quit*) antes de abrir a versão de teste. As duas usam os mesmos dados em
`%AppData%\Popfold`.

**Lançar uma versão nova** (o GitHub monta e publica o instalador):

1. Mude `<Version>` no `Popfold.csproj` (ex.: `0.3.1`).
2. Crie `docs/release-notes-0.3.1.md` (texto da versão, em inglês, com um parágrafo em português no fim).
3. Commit, push e tag:
   ```powershell
   git tag v0.3.1
   git push origin main v0.3.1
   ```
4. O workflow `.github/workflows/release.yml` confere se o tag bate com `<Version>` (se não bater, para),
   instala o Inno Setup se faltar, roda `build-installer.ps1`, calcula o SHA256, registra o atestado de origem
   (`attest-build-provenance`) e cria a release com o instalador, o `SHA256SUMS.txt` e a seção
   *Verify the download* (link do log, SHA256, comando `gh attestation verify`).
5. Depois de publicada: envie o instalador baixado do GitHub para o VirusTotal (aba **File**, não URL; pela URL
   o relatório fica com o nome interno aleatório do armazenamento do GitHub) e adicione no texto da release o link
   `https://www.virustotal.com/gui/file/<sha256>`.

Rodar o workflow à mão (Actions → Release → Run workflow) só monta o instalador e o anexa à execução, sem publicar.

## 4. Arquitetura

.NET 10, WPF para tudo e WinForms só para o ícone da bandeja. Ponto de entrada próprio em `Program.cs`
(`<StartupObject>Popfold.Program</StartupObject>`), que trata argumentos antes de carregar o WPF (~70 ms em vez
de ~1 s ao clicar no ícone da barra de tarefas).

| Arquivo | Função |
| --- | --- |
| `Program.cs` | `--run <id> <i>` (item da jump list), `--open <id>` (clique na barra de tarefas), `--new <arquivo>` (Novo > Popfold); se já houver uma instância, repassa o pedido para ela; detecta o sandbox do Claude e recusa abrir |
| `App.xaml.cs` | Ciclo de vida, lista de pastas, salvar, fixar na barra, "só na barra de tarefas", exclusões |
| `FolderWindow.*` | A pasta na área de trabalho (vidro, prévia 2×2, arrastar, soltar, menu de contexto) |
| `PanelWindow.*` | O painel aberto; posiciona acima/abaixo/ao lado da barra de tarefas quando aberto por ela |
| `SettingsWindow.*`, `Theme.xaml` | Janela de opções e o tema "Bubble Gum" |
| `TaskbarPins.cs`, `ShellLink.cs` | Atalhos `.lnk` com AppUserModelID, ícones gerados e jump lists |
| `ShellIntegration.cs` | Eventos nomeados entre instâncias (`Local\Popfold.OpenFolder`, `Local\Popfold.NewFolder`) |
| `DesktopWatcher.cs` | Captura o arquivo `.popfold` criado por Novo > Popfold, apaga e avisa o Explorer |
| `DesktopSelection.cs` | Seleção por retângulo e tecla Delete na área de trabalho |
| `Wallpaper.cs` | Pedaço do papel de parede atrás de cada pasta para o efeito vidro |
| `IconProvider.cs`, `DropParser.cs`, `ShellLauncher.cs` | Ícones, interpretar o que foi arrastado, executar itens |
| `UpdateChecker.cs` | Consulta `api.github.com/repos/acboer/popfold/releases/latest` e compara com a versão do exe |
| `TrayIcon.cs` | Ícone e menu da bandeja, balões (com ação ao clicar) |
| `ConfigStore.cs`, `Models.cs` | `config.json` e o modelo de dados |
| `L.cs` | Textos em inglês e português (`L.Apply("auto")` escolhe pelo idioma do Windows) |
| `Native.cs` | P/Invoke (Win32) |
| `installer/` | Script do Inno Setup e o modelo vazio do menu Novo |
| `tools/` | Ícone, arte do instalador, build do instalador, registro HKLM, recuperação do sandbox |
| `tools/dev/` | Scripts de captura de tela e cliques usados para as imagens do README |
| `branding/`, `fonts/` | Ícones, arte do instalador; Fredoka e Nunito (OFL) embutidas como recurso |

**Dados** (`%AppData%\Popfold`):
- `config.json`: `Settings` (Transparency, Glass, Style, Language, CheckUpdates, LastUpdateCheck, NotifiedVersion) e `Folders` (Id, Name, X, Y, Items, Taskbar,
  OnDesktop). Cada item tem Name, Path, Kind e Arguments.
- `shortcuts\`: cópias dos atalhos arrastados (por isso dá para apagar os originais da área de trabalho).
- `icons\`: cache de ícones. `taskbar\`: ícones gerados das pastas fixadas e dos itens da jump list.
- `open-request`: arquivo temporário usado no `--open` entre instâncias.

**Barra de tarefas:**
- Cada pasta fixada vira `%AppData%\Microsoft\Windows\Start Menu\Programs\Popfold\<nome>.lnk`. O atalho tem o
  AppUserModelID `Popfold.Folder.<id>` (para o Windows tratá-lo como app separado), argumentos `--open <id>` e
  um `.ico` 2×2 gerado. O nome do `.ico` leva um hash do conteúdo, para furar o cache de ícones do Windows.
- O Windows não deixa um programa fixar sozinho na barra. O app abre o Explorador com o atalho selecionado, e o
  usuário faz *Mostrar mais opções → Fixar na barra de tarefas*.
- As cópias fixadas ficam em `...\User Pinned\TaskBar`; ao renomear ou mudar itens, `ShellLink.Retarget`
  atualiza alvo e ícone delas.
- A jump list (`ICustomDestinationList`) tem uma categoria com o nome da pasta; cada item roda `--run <id> <i>`.
- Clicar de novo no ícone com o painel aberto fecha o painel (`ClosedByTaskbarClick` + janela de 2,5 s / 40 px).

**Menu Novo > Popfold:**
- Fica em **HKLM** (`.popfold\ShellNew` com `FileName` apontando para um modelo vazio, oculto + sistema), mais o
  ProgId `Popfold.Folder` que abre com `--new "%1"`.
- O instalador registra isso. Para registrar uma versão de desenvolvimento, use `tools/register-machine.ps1`
  como administrador. O item só aparece depois que o Explorer reinicia (ou o PC).

## 5. Instalador (Inno Setup)

- `AppId {95C110DC-3880-411B-BFEF-949439384231}`: não mude, senão as atualizações viram instalações separadas.
  A versão sai do próprio exe publicado.
- Instala como administrador em `{autopf}\Popfold`, por causa do HKLM do menu Novo.
- Publicação self-contained `win-x64`; o usuário não precisa instalar o .NET. `SatelliteResourceLanguages`
  limita as traduções do .NET a pt-BR para reduzir o tamanho.
- Idiomas inglês e português. Tarefa opcional "iniciar com o Windows" (HKCU `Run`).
- Fecha o Popfold (`taskkill`) antes de instalar e de desinstalar. Ao desinstalar, pergunta se apaga as pastas do
  usuário (o padrão é **não**, para quem reinstalar).
- O instalador **não é assinado**, então aparece "O Windows protegeu o computador" (*Mais informações → Executar
  assim mesmo*). Opções para assinar mais tarde: SignPath Foundation (gratuito para código aberto, precisa de
  aprovação) ou Azure Trusted Signing (pago, barato).

## 6. Armadilhas (aprendidas do jeito difícil)

1. **Sandbox do app do Claude.** Todo processo iniciado pelo Claude Desktop tem as gravações em `%AppData%` e no
   HKCU redirecionadas para `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming` (e um registro
   privado). Isso vale mesmo sem identidade de pacote (`GetCurrentPackageFullName` diz que não é pacote). Um
   Popfold aberto pelo Claude gravou tudo lá, e depois de reiniciar o PC as pastas, posições, ícones fixados e a
   inicialização automática "sumiram".
   - **Nunca abra o Popfold pelo Claude.** Peça ao usuário para abrir pelo menu Iniciar ou pelo Explorador.
     Fechar (`taskkill`) ou tirar print pelo Claude não tem problema.
   - `Program.cs` grava um arquivo-sonda e, se ele aparecer em `Packages\*\LocalCache\Roaming\Popfold`, mostra
     um aviso e não abre.
   - `tools/recover-from-sandbox.ps1` (rodar pelo Explorador, não pelo Claude) copia os dados de volta, com backup.
   - Tentar escapar do sandbox via WMI (`Win32_Process.Create`) é bloqueado pelo classificador de permissões.
2. **Windows 11 ignora o menu Novo registrado só em HKCU** (talvez também efeito do sandbox, mas HKLM funciona).
   Valores `Command` no ShellNew são ignorados; use `FileName` com um modelo.
3. **Arquivo fantasma:** depois de apagar o `.popfold` temporário, é preciso mandar `SHCNE_DELETE`/`SHCNE_UPDATEDIR`
   e atualizar a área de trabalho, senão fica um ícone que não dá para apagar (`DesktopWatcher.cs`).
4. **Explorer reiniciado:** as janelas das pastas pertencem ao `Progman`; quando o Explorer reinicia, elas precisam
   ser "adotadas" de novo (mensagem `TaskbarCreated`). Pergunte antes de reiniciar o Explorer do usuário.
5. **Cliques simulados não funcionam** no menu de contexto XAML do Windows 11 nem na barra de tarefas. Para testar
   esses fluxos, peça ao usuário para clicar. Para capturar o painel, rode `tools/dev/wait-panel.ps1` em segundo
   plano e peça o clique.
6. **PowerShell 5.1 corrompe UTF-8** (`Get-Content`/`Set-Content` geram mojibake e BOM). Edite arquivos com o editor
   ou com `sed`. Isso já quebrou setas `→` no workflow e no `L.cs`.
7. **Coordenadas de tela:** o PowerShell não reconhece DPI; nesta tela, pixels de captura ≈ DIP × 1,275
   (`wait-panel.ps1` usa esse fator e a altura 1152).
8. **Pílulas no tema:** `CornerRadius="999"` distorce; use altura fixa e raio = metade da altura.
9. **Variáveis do PowerShell não diferenciam maiúsculas** (`$s` e `$S` são a mesma).
10. **Privacidade nas imagens:** prints para o README devem mostrar só o app. Minimize as outras janelas, recorte
    justo e cubra os outros ícones da barra de tarefas.

## 7. Histórico de decisões

- Nome **Popfold**, slogan "Your apps, in a pop.", tema "Bubble Gum" (lilás/rosa), fontes Fredoka e Nunito
  embutidas.
- App em inglês e português, escolhido pelo idioma do Windows (pode ser trocado nas opções). README, imagens e
  texto da release em inglês; `README.pt-BR.md` em português.
- Atalhos arrastados são copiados para os dados do app, para o usuário poder apagar o original da área de trabalho.
  Arquivos comuns (ex.: um `.txt`) não são copiados: a pasta guarda o caminho.
- Pastas podem ficar **só na barra de tarefas**; voltam pela bandeja (*Show on desktop*).
- Licença MIT; repositório público; instalador montado pelo GitHub Actions (mais confiável do que subir um exe
  montado em casa), com SHA256, atestado de origem e link do VirusTotal.
- **0.4.0** (27/11/2026): reordenar itens arrastando no painel (formato interno `Popfold.ReorderItem`, que o
  Explorer e o `DropParser` ignoram; a ordem só é gravada ao soltar dentro do painel), mover item para outra
  pasta soltando-o nela (`App.MoveItem`: não apaga o atalho guardado, que passa a ser da pasta de destino) e
  aviso de versão nova. O aviso **não baixa nada**: abre a página da release no navegador. Primeira consulta 1 min
  depois de abrir, depois no máximo uma por ~23 h; balão só uma vez por versão (`NotifiedVersion`); falha
  silenciosa na automática. Usa `/releases/latest`, que ignora rascunhos e pré-lançamentos, e tags com sufixo
  (`v0.5.0-beta`) são ignoradas. Para testar o aviso sem publicar nada, compile uma cópia "antiga":
  `dotnet build -c Release -p:Version=0.2.9 -p:OutputPath='bin\UpdateTest\'`.
- Doações: **ainda não**. Quando quiser, criar `.github/FUNDING.yml` (GitHub Sponsors, Ko-fi, etc.).

## 8. Ideias e pendências

- Assinatura digital do instalador (ver seção 5).
- Doações (`FUNDING.yml`).
- A imagem `docs/images/menu-en.png` ainda mostra parte do nome "Jogos" atrás do menu; refazer com a pasta "Games".
- Atualização automática de verdade (desde a 0.4.0 o app avisa e abre a página; o usuário ainda baixa e instala
  por cima, e os dados são mantidos). Baixar o instalador sozinho exigiria conferir o SHA256/atestado antes.
- Próximas ideias levantadas em 27/11: backup/restauração das pastas em `.zip`, atalho de teclado global para abrir
  uma pasta, busca digitando no painel, cor por pasta, alinhar pastas à grade, pastas inteligentes (espelham uma
  pasta do disco), selo com número de itens / prévia 3×3, posição por monitor, mais idiomas.

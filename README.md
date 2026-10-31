# Pedal Trainer

Treinador de frenagem para simuladores. Lê o seu conjunto de pedais pelo DirectInput,
compara a sua pisada com a de uma volta de referência e devolve uma nota de precisão curva
a curva — em vez de "eu acho que freie mais tarde", você vê o erro em pontos percentuais.

Aplicativo desktop para Windows, escrito em C# / .NET 8 com WPF. Não é um plugin: roda em
paralelo ao simulador, lê a memória compartilhada do jogo e funciona mesmo com o jogo
fechado (modo simulado, para treinar em qualquer lugar).

Simuladores suportados: **iRacing**, **Assetto Corsa Competizione**, **Assetto Corsa EVO**,
**Automobilista 2** e **Le Mans Ultimate**.

---

## Download

**[⬇ Baixar PedalTrainer.exe](https://github.com/paulosette07/pedaltrainer/releases/latest/download/PedalTrainer.exe)**
— Windows x64, arquivo único, ~69 MB.

O mesmo binário está disponível em dois lugares:

- **Release** (link acima): versão publicada, compilada pelo CI a cada tag em
  [releases](https://github.com/paulosette07/pedaltrainer/releases).
- **Direto do repositório**: [`dist/PedalTrainer.exe`](dist/PedalTrainer.exe), sempre
  atualizado no branch `main`.

Detalhes:

- **Não precisa instalar .NET** nem nenhuma dependência: o executável é autocontido.
- Windows 10/11 64 bits.
- O Windows pode mostrar o aviso do SmartScreen na primeira execução (binário sem
  assinatura digital). Clique em **Mais informações → Executar assim mesmo** — veja
  [Aviso do SmartScreen](#aviso-do-smartscreen) para o motivo e para a correção definitiva.
- Não há instalador: baixe, coloque numa pasta e execute. Os dados ficam em
  `%APPDATA%\PedalTrainer` — nada é escrito ao lado do executável.

---

## Aviso do SmartScreen

Na primeira execução o Windows pode mostrar **"O Windows protegeu o computador"** com o
publicador como *Desconhecido*. Não é detecção de vírus: o Windows marca todo arquivo baixado
da internet (*Mark of the Web*) e o SmartScreen bloqueia executáveis **sem assinatura digital**
que ainda não têm histórico de downloads. Como o Pedal Trainer é um projeto pequeno, sem
certificado de code signing, o aviso aparece.

Para rodar agora, uma das três:

| Como | Passos |
| --- | --- |
| Na própria janela do aviso | **Mais informações** → **Executar assim mesmo**. |
| Antes de abrir | Clique com o botão direito no `.exe` → **Propriedades** → marque **Desbloquear** → **OK**. |
| Pelo PowerShell | `Unblock-File .\PedalTrainer.exe` |

Se preferir confirmar que o download não foi corrompido, compare o SHA-256 com o da release:

```powershell
certutil -hashfile PedalTrainer.exe SHA256
```

A correção definitiva é assinar o executável — veja [Assinatura digital](#assinatura-digital).

---

## O que ele faz

1. **Lê o pedal de verdade.** DirectInput, até 200 Hz de amostragem no motor, com
   calibração de faixa, zona morta, curva de resposta e filtro por pedal.
2. **Compara com uma referência.** Carrega uma volta (CSV/JSON), extrai as zonas de
   frenagem e, a cada frenagem, abre um "desafio" que acompanha a curva do piloto de
   referência.
3. **Pontua a precisão.** Erro instantâneo, RMSE do trecho, nota de 0 a 100 com conceito
   (A/B/C...), sequência de acertos e recorde por zona.
4. **Treina o pé fora do carro.** Programas de exercício (freio progressivo, modulação de
   acelerador) com curva-alvo, repetições e avaliação por repetição.
5. **Grava a sua própria referência.** Um clique grava uma volta do simulador conectado e a
   transforma na referência do treino.

---

## Telas

| Tela | Para quê |
| --- | --- |
| **Treino** | Barra de freio/acelerador ao vivo, gráfico de traço contra a curva de referência, medidor de precisão, nota ao vivo, zonas da volta e estatísticas da sessão. |
| **Programas** | Biblioteca de treinos do tipo "pise até 20%, segure, solte devagar", com alvo sorteado por repetição. Programas são arquivos JSON editáveis. |
| **Calibração** | Descobre qual eixo do controlador é o freio e qual é o acelerador, aprende a faixa bruta do pedal e ajusta zona morta, curva e suavização. |
| **Configurações** | Simulador de onde vem a telemetria, rigor do treino (gatilho de frenagem, banda de tolerância), janela do gráfico, alinhamento por distância, escolha do controlador e do modelo de pedal. |

---

## Primeiros passos

1. **Calibre os pedais** — aba *Calibração*. Pise fundo no freio e no acelerador: o app
   descobre o eixo e aprende a faixa de cada pedal sozinho. Ajuste a zona morta e a curva se
   quiser, salve.
2. **Escolha o modelo do seu conjunto** — aba *Configurações* → *Modelo de pedal*. O catálogo
   aplica curva, zona morta e filtro recomendados para o hardware (Load Cell, Hall ou
   potenciômetro). Se o nome do controlador for reconhecido, o app já pré-seleciona.
3. **Carregue uma referência** e faça uma frenagem — ou clique em *Volta demo* para começar
   sem o simulador aberto.
4. **Abra o simulador** (opcional, mas recomendado) e confirme em *Configurações* →
   *Simulador* que ele está selecionado — ou deixe em **Detectar automaticamente**. Com o
   jogo publicando telemetria, o app alinha a frenagem pela **distância percorrida** em vez
   do tempo, então você não precisa frear no mesmo instante da referência — só no mesmo
   lugar da pista.

---

## Simuladores suportados

A telemetria vem da memória compartilhada de cada jogo. Não existe plugin do Pedal Trainer: o
app só lê o que o simulador já publica.

| Simulador | Como habilitar |
| --- | --- |
| **iRacing** | Nada a instalar: abra o jogo e entre na pista. |
| **Assetto Corsa Competizione** | Nada a instalar: abra o ACC e entre na pista. |
| **Assetto Corsa EVO** | AC EVO 0.6 ou mais novo, com a memória compartilhada ativa. |
| **Automobilista 2** | No jogo: *Options → System → Shared Memory* = `Project CARS 2`. |
| **Le Mans Ultimate** | No jogo: *Settings → Gameplay → Enable Plugins* (plugin `rFactor 2 Shared Memory Map`). |

Em **Detectar automaticamente** o app testa os cinco em sequência e passa para o próximo
quando o primeiro para de publicar — pode abrir o jogo que quiser, na ordem que quiser. Para
não deixar dúvida, escolha o simulador na lista: o app passa a ler só aquele.

De cada jogo o app lê velocidade, freio, acelerador, número da volta e posição na pista. O
tempo de sessão só existe no iRacing; nos outros o relógio é mantido internamente.

---

## Formato da volta de referência

O app aceita **CSV**, **TSV** e **JSON**, com detecção automática de separador (`,` `;` tab),
cabeçalho opcional e vírgula ou ponto como separador decimal.

### CSV / TSV

```csv
Time,Brake,Throttle,Distance,Speed
0.000,0.00,100.00,0.0,142.5
0.100,0.00,100.00,4.0,143.1
0.200,12.50,80.00,8.0,143.4
0.300,68.00,10.00,12.0,140.2
```

| Coluna | Obrigatória | Significado |
| --- | --- | --- |
| `Time` | sim | Tempo em segundos (o zero é relativo ao início do arquivo). |
| `Brake` | sim | Pressão de freio, 0–100 (aceita 0–1 e converte automaticamente). |
| `Throttle` | não | Acelerador, 0–100 (mesma conversão). |
| `Distance` | não | Distância em metros; habilita o alinhamento por posição na pista. |
| `Speed` | não | Velocidade em km/h, só exibida. |

Os nomes de coluna são tolerantes: `time`/`tempo`, `brake`/`freio`/`brakepct`,
`throttle`/`gas`/`acelerador`, `distance`/`lapdist`, `speed`/`velocidade`. Sem cabeçalho, a
ordem posicional é `Time, Brake, Throttle, Distance, Speed`.

### JSON

```json
{
  "name": "Minha volta",
  "trackLengthMeters": 4309,
  "samples": [
    { "t": 0.000, "b": 0.00, "th": 100.00, "d": 0.0, "v": 142.5 },
    { "t": 0.200, "b": 12.50, "th": 80.00, "d": 8.0, "v": 143.4 }
  ]
}
```

Aceita chaves curtas (`t`, `b`, `th`, `d`, `v`) ou completas (`time`, `brake`, `throttle`,
`distance`, `speed`), e também um array de amostras puro. `trackLengthMeters` não é
obrigatório, mas é o que permite converter `Distance` em posição na pista.

### Gravar a referência do simulador

Botão **Gravar volta** na barra superior:

- Arma o gravador; a captura começa quando o carro cruza a linha e termina na volta seguinte.
- Descarta voltas que não cobriram 90% da pista, que passaram de 10 minutos, ou se a sessão
  for reiniciada.
- A distância é calculada integrando a velocidade e calibrada pelo percentual de pista
  percorrido, então o alinhamento por distância funciona com essa referência.
- O CSV é salvo em `%APPDATA%\PedalTrainer\samples\volta-AAAAMMDD-HHMMSS.csv` e já entra como
  referência ativa.

---

## Formato dos programas de treino

Os programas ficam em `%APPDATA%\PedalTrainer\drills\*.drill.json` e são criados na primeira
execução. São arquivos de texto comuns: copie, edite os percentuais e recarregue a tela.

```json
{
  "name": "Freio progressivo",
  "description": "Sobe até 20%, segura e solta devagar.",
  "version": 1,
  "restPercent": 4,
  "steps": [
    {
      "label": "Freio até 20% e soltar devagar",
      "pedal": "brake",
      "targetPercent": 20,
      "tolerancePercent": 4,
      "repetitions": 10,
      "segments": [
        { "kind": "ramp", "seconds": 1.0 },
        { "kind": "hold", "seconds": 0.8 },
        { "kind": "release", "seconds": 2.5, "toPercent": 0 }
      ]
    },
    {
      "label": "Alvo sorteado (20 a 85%)",
      "pedal": "brake",
      "targetPercent": 50,
      "tolerancePercent": 7,
      "repetitions": 12,
      "randomTargetMinPercent": 20,
      "randomTargetMaxPercent": 85,
      "segments": [
        { "kind": "ramp", "seconds": 1.5 },
        { "kind": "hold", "seconds": 1.0 },
        { "kind": "release", "seconds": 3.0 }
      ]
    }
  ]
}
```

| Campo | Efeito |
| --- | --- |
| `pedal` | `brake` ou `throttle`. |
| `targetPercent` | Alvo da etapa, em % do curso do pedal. |
| `tolerancePercent` | Banda em que o erro não é penalizado. |
| `repetitions` | Quantas vezes a sequência de segmentos se repete. |
| `randomTargetMinPercent` / `Max` | Sorteia o alvo a cada repetição (deixe nulo para alvo fixo). |
| `segments[].kind` | `ramp` (subir até o alvo), `hold` (manter) ou `release` (soltar). |
| `segments[].seconds` | Duração do trecho. |
| `segments[].toPercent` | Percentual ao fim do trecho; nulo usa o alvo da etapa. |
| `restPercent` | Abaixo disso o pedal conta como solto (volta ao repouso). |

Regras de validação: cada etapa precisa de pelo menos um segmento, uma repetição e duração
total de até 120 s por repetição; alvos ficam entre 5% e 100%.

---

## Arquitetura

O app é dividido em uma biblioteca de domínio testável e uma casca WPF fina.

```
PedalTrainer.Core   domínio puro: entrada, referência, treino, drills, telemetria, persistência
PedalTrainer.App    WPF: janela, views, view models, controles de desenho e tema
PedalTrainer.Tests  xUnit, 172 testes cobrindo o núcleo sem precisar de hardware
PedalTrainer.Probe  ferramenta de console para inspecionar controladores e eixos
```

Ponto importante: **o motor roda em thread própria a 200 Hz** e publica um quadro imutável
(`TrainingFrame`); a interface apenas lê esse quadro a 60 Hz. Nenhum cálculo de treino
acontece na thread de UI, e nenhuma chamada de UI acontece na thread do motor.

| Componente | Papel |
| --- | --- |
| `TrainingEngine` | Laço de 200 Hz: lê pedal e carro, alimenta a sessão, publica o quadro. |
| `PedalSignalProcessor` | Converte eixo bruto em percentual: faixa, inversão, zona morta, curva, filtro. |
| `DirectInputPedalDevice` | Aquisição pelo DirectInput, com reconexão e taxa de amostragem medida. |
| `AutoAxisMapper` | Descobre qual eixo é o freio e qual é o acelerador observando o movimento. |
| `TrainingSession` | Máquina de estados: armado → desafio → resultado, com estatísticas. |
| `BrakingZoneExtractor` | Encontra as zonas de frenagem da referência a partir do gatilho configurado. |
| `DistanceReferenceAligner` / `TimeReferenceAligner` | Casam a sua frenagem com a referência por posição na pista ou por tempo. |
| `BrakingScorer` | Erro instantâneo, RMSE e nota do trecho. |
| `DrillRunner` / `DrillProgramStore` | Execução dos programas de exercício e biblioteca em disco. |
| `SimTelemetrySource` | Laço de 60 Hz que percorre os leitores e publica o quadro de telemetria. |
| `SimTelemetrySourceFactory` | Monta os leitores conforme o simulador escolhido (ou todos, no modo automático). |
| `SimulatorCatalog` | Lista dos simuladores suportados, com nome exibido e dica de configuração. |
| `AccTelemetryReader` / `AcEvoTelemetryReader` | Memória compartilhada do ACC e do AC EVO (`acpmf_*`, `acevo_pmf_*`). |
| `Automobilista2TelemetryReader` | Struct `pcars2APIStruct` publicada pelo AMS2 no formato PCARS 2. |
| `LeMansUltimateTelemetryReader` | Plugin `rFactor 2 Shared Memory Map` usado pelo LMU. |
| `IRacingTelemetryReader` | Adapta o leitor do iRacing à interface comum. |
| `LapRecorder` | Transforma uma volta do simulador em volta de referência. |
| `SettingsStore` / `CalibrationStore` | Persistência em JSON no perfil do usuário. |

---

## Onde ficam os arquivos

Tudo em `%APPDATA%\PedalTrainer` (nada é gravado ao lado do .exe):

```
%APPDATA%\PedalTrainer\
├── settings.json            preferências (simulador, rigor, gráfico, controlador, modelo de pedal)
├── calibration.json         faixa dos eixos, zona morta, curva, filtro
├── drills\
│   ├── freio-progressivo.drill.json
│   └── aceleracao-progressiva.drill.json
└── samples\
    ├── demo-lap.csv                       volta demo gerada pelo app
    └── volta-20260101-153000.csv          voltas gravadas do simulador conectado
```

Apagar `settings.json`/`calibration.json` restaura os padrões. Apagar a pasta `drills`
faz o app recriar os programas embutidos na próxima abertura.

---

## Conjuntos de pedais reconhecidos

O catálogo aplica curva, zona morta e filtro conforme a tecnologia do freio (Load Cell,
Hall ou potenciômetro). O modelo é detectado pelo nome que o DirectInput informa.

| Marca | Modelos |
| --- | --- |
| Fanatec | ClubSport V3, ClubSport V2, CSL Elite V2, CSL Pedals |
| Moza Racing | SR-P Lite, SR-P, CRP2, CRP |
| Simagic | P2000, P1000 |
| Heusinkveld | Sim Pedals Ultimate+, Sim Pedals Sprint |
| Asetek SimSports | Invicta, Forte, La Prima |
| Logitech | G Pro Racing Pedals, G25/G27/G29/G920/G923 |
| Thrustmaster | T-LCM, T3PA / T3PA-Pro |
| Simsonn | Pro Pedals (Load Cell) |
| SimJack | Pro / UT Pedals |
| VRS | DirectForce Pro Pedals |
| Cammus | LC100 Pedals |
| Genérico | Kit DIY / Arduino + Load Cell |

Qualquer outro controlador funciona no modo **"não listado"**: a calibração manual continua
valendo. Se o nome não casar com nada, a tela de calibração mostra os eixos brutos para você
escolher à mão (e o `PedalTrainer.Probe` ajuda a inspecionar o hardware).

---

## Compilar a partir do código

Requisitos: Windows 10/11 x64 e o **.NET SDK 8 ou superior**. Os projetos miram `net8.0`.
O arquivo de solução usa o formato `.slnx`, que pede SDK 9.0.200+ — com o SDK 8, compile o
projeto do app diretamente.

```powershell
# compilar tudo (SDK 9.0.200+)
dotnet build PedalTrainer.slnx -c Release

# alternativa com o SDK 8
dotnet build src/PedalTrainer.App/PedalTrainer.App.csproj -c Release

# rodar os testes (172 testes)
dotnet test tests/PedalTrainer.Tests/PedalTrainer.Tests.csproj -c Release

# rodar a partir do código
dotnet run --project src/PedalTrainer.App/PedalTrainer.App.csproj
```

Gerar o executável único autocontido (o mesmo que está em `dist/`):

```powershell
dotnet publish src/PedalTrainer.App/PedalTrainer.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none `
  -o dist/publish
```

### Assinatura digital

O workflow de release assina o executável quando existe um certificado de code signing
configurado. O passo roda
[`tools/signing/Sign-Binary.ps1`](tools/signing/Sign-Binary.ps1), que assina com SHA-256,
aplica carimbo de tempo RFC 3161 (para a assinatura continuar válida depois que o
certificado expirar) e confere a cadeia com `signtool verify /pa`.

Configure dois secrets no repositório (*Settings → Secrets and variables → Actions*):

| Secret | Conteúdo |
| --- | --- |
| `PEDALTRAINER_PFX_BASE64` | O `.pfx` do certificado em base64, em uma única linha. |
| `PEDALTRAINER_PFX_PASSWORD` | A senha do `.pfx`. |

```powershell
# gera o valor do secret a partir do .pfx
[Convert]::ToBase64String([IO.File]::ReadAllBytes('C:\caminho\pedaltrainer.pfx'))
```

Sem esses secrets o build continua funcionando: ele apenas avisa no log que o binário saiu
sem assinatura.

Para assinar localmente depois de um `dotnet publish`:

```powershell
$env:PEDALTRAINER_PFX_BASE64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes('C:\caminho\pedaltrainer.pfx'))
$env:PEDALTRAINER_PFX_PASSWORD = 'senha'
powershell -ExecutionPolicy Bypass -File tools/signing/Sign-Binary.ps1 -Path dist/publish/PedalTrainer.exe
```

Qual certificado resolve o aviso:

- **OV (Organization Validation)** — mais barato (ordem de US$ 100–300 por ano), valida o CNPJ
  ou a identidade do titular. A assinatura é aceita, mas o SmartScreen ainda pode avisar até o
  binário acumular downloads; a reputação cresce com o tempo.
- **EV (Extended Validation)** — mais caro e com validação mais rígida, exige token físico ou
  HSM em nuvem. Dá reputação de confiança imediata no SmartScreen.
- **Azure Trusted Signing** — assinatura gerenciada pela Microsoft, bem mais barata que um
  certificado tradicional, porém exige uma organização com histórico verificável.
- **Enquanto não houver certificado** — é possível enviar cada binário em
  [Microsoft Security Intelligence File Submission](https://www.microsoft.com/en-us/wdsi/filesubmission)
  como *software developer*; o aviso é removido para aquele arquivo específico, e é preciso
  repetir a cada versão.

Um certificado autoassinado **não** resolve para quem baixa o arquivo: ele só é aceito na
máquina onde o certificado está instalado como raiz confiável, então serve para teste local,
não para distribuição.

`dist/PedalTrainer.exe` (o binário versionado no repositório) precisa ser assinado e regravado
sempre que for republicado, usando o mesmo script.

Atalho para inspecionar controladores e eixos sem abrir a interface:

```powershell
dotnet run --project tools/PedalTrainer.Probe/PedalTrainer.Probe.csproj
```

### Ícone do aplicativo

O ícone (tile escuro com o símbolo do pedal em verde, nas cores do tema) sai de um único
arquivo e aparece em dois lugares:

- **No executável** (`ApplicationIcon`), que é o que o Explorer mostra e o que a barra de
  tarefas usa quando o app é fixado.
- **Na janela**, barra de título, barra de tarefas e Alt+Tab. Aqui o ícone **não** vem do
  `Icon` do XAML: no `dotnet publish -p:PublishSingleFile=true` a resolução do pack URI
  devolve vazio e a janela ficava sem ícone nenhum. [`Services/AppIconProvider.cs`](src/PedalTrainer.App/Services/AppIconProvider.cs)
  lê o ícone do próprio `.exe` em tempo de execução (`ExtractIconEx`), entrega os tamanhos
  nativos de 16 e 32 px por `WM_SETICON` e só cai para o recurso WPF quando o processo não
  tem ícone próprio — caso de rodar via `dotnet PedalTrainer.dll`.

- Fonte vetorial: [`assets/branding/pedaltrainer-icon.svg`](assets/branding/pedaltrainer-icon.svg)
- Binário usado no build: [`src/PedalTrainer.App/Assets/pedaltrainer.ico`](src/PedalTrainer.App/Assets/pedaltrainer.ico)
  — 16, 20, 24, 32, 40, 48, 64, 128 e 256 px em um único arquivo.

Para regenerar o `.ico` depois de mexer no SVG ou na paleta:

```powershell
powershell -ExecutionPolicy Bypass -File tools/branding/New-AppIcon.ps1
```

---

## Limitações conhecidas

- **Somente Windows.** A aquisição usa DirectInput e a telemetria usa memória compartilhada
  do jogo — ambos exclusivos da plataforma.
- **A leitura da telemetria é passiva.** O app não escreve nada no simulador e não é
  detectado como cheat; ele só lê os canais que o jogo publica (velocidade, freio,
  acelerador, volta e posição na pista).
- **Sem o simulador aberto**, o alinhamento cai para o modo tempo (o desafio começa quando
  você pisa no freio e segue a curva por tempo, não por posição na pista).
- **Automobilista 2 e Le Mans Ultimate dependem do jogo estar configurado** para publicar
  memória compartilhada (veja *Simuladores suportados*); sem isso eles aparecem como
  "simulador não encontrado".
- **Binário sem assinatura digital** enquanto não houver certificado de code signing
  configurado, então o SmartScreen avisa na primeira execução. O pipeline já sabe assinar
  (veja [Assinatura digital](#assinatura-digital)); falta apenas o certificado.
- Voltas gravadas precisam cobrir pelo menos 90% da pista; voltas parciais são descartadas de
  propósito, para não gerar referência incompleta.

---

## Histórico

### 1.2.0

- **Ícone volta a aparecer na janela**: barra de título, barra de tarefas e Alt+Tab usam o
  logo do programa. O `Icon` declarado no XAML não sobrevivia ao
  `dotnet publish -p:PublishSingleFile=true`, então a janela caía no ícone genérico do
  Windows; agora o ícone é lido do próprio executável em tempo de execução.
- Versão visível no rodapé (`v1.2.0`).

### 1.1.0

- **Cinco simuladores** em vez de um: iRacing, Assetto Corsa Competizione, Assetto Corsa EVO,
  Automobilista 2 e Le Mans Ultimate, com detecção automática e escolha manual em
  *Configurações → Simulador*.
- **Leitura por memória compartilhada** organizada em leitores independentes
  (`AccTelemetryReader`, `AcEvoTelemetryReader`, `Automobilista2TelemetryReader`,
  `LeMansUltimateTelemetryReader`, `IRacingTelemetryReader`) atrás de uma interface comum.
- **Falha silenciosa virou diagnóstico**: a barra superior mostra qual simulador está
  selecionado e o que falta para ele publicar telemetria.
- **Combos corrigidos**: simulador e controlador aparecem pelo nome, não pelo texto interno
  do registro.
- **Barra superior não estoura mais** em janelas estreitas: os botões de gravação e de
  carregamento têm espaço garantido a partir de 1120 px.
- **Versão visível no rodapé** (`v1.1.0`), além dos metadados do executável.
- 172 testes (eram 128), incluindo os deslocamentos de cada struct de memória compartilhada.

### 1.0.0

- Primeira versão pública: treino de frenagem com iRacing, calibração de pedais,
  programas de exercício e gravação de volta de referência.

---

## Licença

MIT — veja [LICENSE](LICENSE).

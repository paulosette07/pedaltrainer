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
6. **Sabe em que pista você está.** Catálogo de circuitos reais com o traçado desenhado no
   mapa, volta ideal gerada a partir da geometria de cada um e uma pasta de referências por
   pista — a sua volta do iRacing em Interlagos não se mistura com a de Monza.

---

## Telas

| Tela | Para quê |
| --- | --- |
| **Treino** | Barra de freio/acelerador ao vivo, gráfico de traço contra a curva de referência, medidor de precisão, nota ao vivo, zonas da volta e estatísticas da sessão. A coluna da direita é o cartão da pista: seletor de circuito, **mapa do traçado** com as zonas de frenagem e a posição do carro e da referência, e a escolha da referência (volta ideal do sistema ou um arquivo seu). Fora de um desafio o gráfico principal já desenha a próxima frenagem em âmbar; a faixa **PRÓXIMA CURVA**, no topo, mantém esse desenho na tela o tempo todo — inclusive enquanto você faz a curva atual. |
| **Programas** | Biblioteca de treinos do tipo "pise rápido até 20%, segure e solte devagar", com alvo sorteado por repetição. O painel **PRÓXIMA CURVA** desenha a próxima repetição antes, durante e depois de cada uma. Programas são arquivos JSON editáveis. |
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
3. **Escolha a pista** — aba *Treino*, seletor **PISTA**. O app abre em **Interlagos** com a
   volta ideal do sistema já carregada: dá para treinar sem simulador aberto, sem arquivo
   nenhum.
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

## Pistas e voltas de referência

A aba *Treino* trabalha sempre **dentro de uma pista**. O cartão da direita tem o seletor de
circuito, o mapa e a lista de referências daquela pista.

- **Catálogo embutido.** 52 circuitos já vêm no executável:
  - *Américas*: Interlagos, Goiânia, Curitiba, Cascavel, Tarumã, Road America, Road Atlanta,
    Sebring, COTA, Laguna Seca, Sonoma, Mosport, Watkins Glen, Mid-Ohio.
  - *Europa*: Imola, Mugello, Monza, Vallelunga, Velo Città, Misano, Spa,
    Zolder, Zandvoort, Assen, Nürburgring, Hockenheim, Oschersleben, Brands Hatch,
    Silverstone, Donington, Oulton Park, Barcelona, Jerez, Valência, Aragón, Estoril,
    Magny-Cours, Dijon, Red Bull Ring, Hungaroring, Most.
  - *Ásia e Oceania*: Suzuka, Fuji, Motegi, Okayama, Sepang, Buriram, Phillip Island,
    Mount Panorama, Bahrain, Yas Marina, Lusail.

  O app abre em **Interlagos**.
- **Mapa do traçado.** O desenho vem da geometria real do circuito (levantamento do
  OpenStreetMap, costurado numa volta fechada, escalado para o comprimento oficial e
  reamostrado a cada 6 m). As zonas de frenagem aparecem pintadas sobre o asfalto: verde na
  que está sendo treinada, âmbar na próxima e vinho nas demais. O carro (triângulo verde) e a
  posição da referência (anel azul) andam pelo desenho quando o simulador publica a posição
  na pista.
- **Volta ideal de cada pista.** Nenhuma telemetria é obrigatória: o app calcula uma volta de
  referência a partir do raio de cada curva, com limite lateral, frenagem, aceleração e
  velocidade máxima de um carro de GT. Ela é a opção *Volta ideal do sistema* da lista e
  serve de ponto de partida em qualquer circuito.
- **Referências por pista.** A sua volta gravada, ou um arquivo importado, fica guardado na
  pasta daquela pista (`tracks\references\<pista>`). A lista mostra o sistema primeiro e, em
  seguida, os seus arquivos — dá para alternar entre a referência de ontem e a de hoje sem
  procurar arquivo no disco.
- **Importar traçado.** *Importar traçado* aceita um `.json` no formato abaixo e passa a usar
  esse desenho no lugar do aproximado. Um traçado importado com o mesmo `id` de um circuito
  do sistema **substitui** o do sistema, mantendo o nome e o lugar na lista.

### Formato do arquivo de traçado

```json
{
  "id": "minha-pista",
  "name": "Minha Pista",
  "city": "Interior",
  "country": "Brasil",
  "countryCode": "BR",
  "lengthMeters": 3200,
  "points": [0, 0, 120, 0, 240, 40, 300, 160],
  "corners": [ { "name": "Curva 1", "fraction": 0.12 } ]
}
```

| Campo | Obrigatório | Significado |
| --- | --- | --- |
| `id` | sim | Identificador; é o nome da pasta de referências da pista. |
| `points` | sim | Lista plana `x, y` em **metros** (pelo menos três pares). |
| `corners[].fraction` | não | Onde a curva começa na volta (0 = linha de chegada). Dá nome às zonas. |
| `lengthMeters` | não | Comprimento oficial; o traçado é escalado para bater com ele. |

O arquivo é reamostrado ao carregar: os pontos podem vir em qualquer espaçamento, e o app
fecha a volta, alisa os cantos e ancora a linha de chegada no ponto mais próximo do primeiro
ponto informado.

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
  "description": "Pise rápido até o alvo, segure e solte devagar.",
  "version": 2,
  "restPercent": 4,
  "steps": [
    {
      "label": "Freio: pisar rápido em 20% e soltar devagar",
      "pedal": "brake",
      "targetPercent": 20,
      "tolerancePercent": 4,
      "repetitions": 10,
      "segments": [
        { "kind": "ramp", "seconds": 0.3 },
        { "kind": "hold", "seconds": 1.0 },
        { "kind": "release", "seconds": 2.0, "toPercent": 0 }
      ]
    },
    {
      "label": "Freio em alvo sorteado (20 a 85%)",
      "pedal": "brake",
      "targetPercent": 50,
      "tolerancePercent": 7,
      "repetitions": 12,
      "randomTargetMinPercent": 20,
      "randomTargetMaxPercent": 85,
      "segments": [
        { "kind": "ramp", "seconds": 0.4 },
        { "kind": "hold", "seconds": 1.0 },
        { "kind": "release", "seconds": 5.0, "toPercent": 0 }
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

O cronômetro da repetição começa no quadro em que o pé **sai do repouso**, não no instante em
que o app confirma o movimento: a curva-alvo nasce no percentual em que o pedal estava parado
e sobe dali para o alvo. Assim o traçado do piloto e a curva saem do mesmo ponto, e o exercício
de pisada curta (0,3 s) não começa com o pé já alguns pontos acima do traçado.

Regras de validação: cada etapa precisa de pelo menos um segmento, uma repetição e duração
total de até 120 s por repetição; alvos ficam entre 5% e 100%.

Nos programas embutidos o `ramp` é curto de propósito — 0,3 a 0,5 s do repouso até o alvo,
que é a pisada que um carro de verdade pede — e quase todo o tempo da repetição está na
`release`, onde o pé precisa modular. A única subida longa é a de 50% até 100% do programa
de acelerador, que treina a aplicação progressiva na saída de curva.

Arquivos da versão 1 que ainda estão idênticos ao padrão antigo (pisada lenta) são regravados
com a pegada nova na abertura da tela. Arquivo renomeado, com percentuais ajustados ou com
descrição própria é preservado; o botão *Regravar programas padrão* força a regravação de
todos.

### Ver a curva antes de começar

O gráfico do exercício nunca fica vazio. Com um programa escolhido e o treino parado ele já
desenha a **prévia da primeira repetição** (tracejada, com marcadores de largada e de pico e
a etiqueta `pico 20% · 3,3 s`). Ao iniciar, o estágio *PRONTO* mostra a **próxima curva**
exatamente como ela vai ser cobrada — mesma altura de alvo, mesma banda de tolerância — e a
tira `PLANO DA REPETIÇÃO` lista os trechos na ordem:

```
SUBIR  até 20%  0,3 s   →   SEGURAR  em 20%  1,0 s   →   SOLTAR  até 0%  2,0 s
```

Durante a repetição o trecho em execução fica destacado, e quando a repetição é a última da
etapa o app avisa qual curva vem depois. Etapas com alvo sorteado aparecem com o meio da faixa
na prévia: a forma da curva é sempre a mesma, só a altura muda.

### O próximo gráfico fica na tela o tempo todo

As duas telas de treino têm um painel **PRÓXIMA CURVA** que nunca some, nem no meio da
repetição:

| Onde | O que ele mostra |
| --- | --- |
| **Treino** | Com o desafio parado, o gráfico principal inteiro passa a ser a próxima frenagem — em âmbar tracejado, com banda de tolerância, marcadores de largada e de pico e a etiqueta `PRÓXIMA CURVA · pico 95% · 2,5 s`. Ao frear, ele volta para a curva de referência em azul contra a sua entrada, e a faixa larga do topo continua desenhando a frenagem seguinte, com nome, pico, duração e a miniatura da curva. A curva vem da posição na pista ou, sem posição, da ordem das zonas. |
| **Programas** | A próxima repetição do exercício: a mesma curva da etapa (*PRÓXIMA REPETIÇÃO*, com a contagem `REP 3/10`) ou a primeira da etapa seguinte (*PRÓXIMA ETAPA*), com o nome da etapa e o alvo. Na última repetição do programa o painel avisa que o treino acabou. |

Cada painel traz nome/título, pico e duração (`pico 82% · 1,4 s`), alvo e o desenho em
miniatura — dá para conferir a forma da curva sem tirar o olho do gráfico principal. Etapas
com alvo sorteado aparecem tracejadas no painel: a altura só é conhecida quando a repetição
começa.

---

## Arquitetura

O app é dividido em uma biblioteca de domínio testável e uma casca WPF fina.

```
PedalTrainer.Core   domínio puro: entrada, referência, treino, drills, telemetria, persistência
PedalTrainer.App    WPF: janela, views, view models, controles de desenho e tema
PedalTrainer.Tests  xUnit, 220 testes cobrindo o núcleo sem precisar de hardware
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
| `TrainingSession` / `ChallengeDetector` | Máquina de estados (armado → desafio → resultado, com estatísticas) e escolha da zona; informam também a **próxima zona** (`PeekNext`/`PeekAfter`) sem mexer na fila do treino. |
| `BrakingZoneExtractor` | Encontra as zonas de frenagem da referência a partir do gatilho configurado. |
| `DistanceReferenceAligner` / `TimeReferenceAligner` | Casam a sua frenagem com a referência por posição na pista ou por tempo. |
| `BrakingScorer` | Erro instantâneo, RMSE e nota do trecho. |
| `DrillRunner` / `DrillProgramStore` | Execução dos programas de exercício e biblioteca em disco. |
| `DrillUpcomingCurveFactory` | Monta a prévia da curva seguinte do programa: mesma etapa ou etapa nova, com alvo e tolerância. |
| `SimTelemetrySource` | Laço de 60 Hz que percorre os leitores e publica o quadro de telemetria. |
| `SimTelemetrySourceFactory` | Monta os leitores conforme o simulador escolhido (ou todos, no modo automático). |
| `SimulatorCatalog` | Lista dos simuladores suportados, com nome exibido e dica de configuração. |
| `AccTelemetryReader` / `AcEvoTelemetryReader` | Memória compartilhada do ACC e do AC EVO (`acpmf_*`, `acevo_pmf_*`). |
| `Automobilista2TelemetryReader` | Struct `pcars2APIStruct` publicada pelo AMS2 no formato PCARS 2. |
| `LeMansUltimateTelemetryReader` | Plugin `rFactor 2 Shared Memory Map` usado pelo LMU. |
| `IRacingTelemetryReader` | Adapta o leitor do iRacing à interface comum. |
| `LapRecorder` | Transforma uma volta do simulador em volta de referência. |
| `TrackLibrary` / `TrackCatalog` | Catálogo de circuitos embutidos (JSON no assembly) e fusão com os traçados importados. |
| `TrackPath` / `TrackPathBuilder` | Traçado fechado reamostrado em passos iguais, com fração de volta, direção e ponto mais próximo. |
| `LapSpeedProfile` / `IdealLapFactory` | Volta ideal a partir do raio de cada curva, com frenagem, aceleração e arrasto. |
| `ReferenceLibrary` / `TrackStore` | Referências por pista (sistema e arquivos do piloto) e traçados importados em disco. |
| `TrackMap` / `TrackMapPainter` | Controle WPF que desenha o circuito, as zonas de frenagem e os marcadores do carro e da referência. |
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
├── samples\
│   └── volta-20260101-153000.csv          voltas gravadas do simulador conectado
└── tracks\
    ├── maps\                              traçados importados pelo piloto
    │   └── minha-pista.json
    └── references\                        uma pasta por pista
        ├── interlagos\
        │   └── volta-interlagos-20260101-153000.csv
        └── monza\
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

# rodar os testes (220 testes)
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
- **O traçado do mapa é esquemático.** Sai do eixo da pista levantado no OpenStreetMap,
  costurado e escalado para o comprimento oficial — serve para localizar a curva e as zonas
  de frenagem, não para medir largura de pista ou zebra. Se você tem um desenho melhor,
  importe pelo botão *Importar traçado*.
- **Alguns circuitos da lista de referência não têm levantamento utilizável** no OpenStreetMap
  (traçado desenhado como área, só parcialmente mapeado ou ausente) e por isso não entram no
  executável: são 52 dos 58 catalogados, e a lista é regenerada a cada versão. Nesses casos,
  importe um arquivo de traçado — o app aceita qualquer pista no formato descrito acima.

---

## Histórico

### 1.6.0

- **Catálogo de pistas com mapa**: a aba *Treino* ganhou a coluna da direita com o seletor de
  circuito, o desenho do traçado e a lista de referências da pista. O app abre em
  **Interlagos** já com a volta ideal do sistema carregada — dá para treinar sem simulador e
  sem arquivo. São 52 circuitos com geometria levantada no OpenStreetMap, costurada numa
  volta fechada e escalada para o comprimento oficial.
- **Volta ideal gerada do traçado**: sem telemetria, o app calcula a referência de qualquer
  pista a partir do raio de cada curva (limite lateral, frenagem, aceleração e arrasto de um
  carro de GT). Interlagos sai em ~1:25 contra 1:08 do recorde real, com uma dúzia de
  frenagens — o suficiente para treinar a pisada em qualquer circuito.
- **Uma pasta de referências por pista**: a volta gravada no simulador, ou um arquivo
  importado, fica em `tracks\references\<pista>` em vez de uma pasta única. A lista mostra a
  volta do sistema e as suas, e a escolha é lembrada na próxima abertura.
- **Mapa com as zonas de frenagem**: verde na zona em treino, âmbar na próxima e vinho nas
  demais, com o carro (triângulo) e a referência (anel) andando pelo desenho quando o jogo
  publica a posição na pista.
- **Importar traçado**: um `.json` com a lista de pontos substitui o desenho aproximado de
  qualquer circuito (ou cria um novo).
- **Frenagem só conta se tirar velocidade**: a detecção de zonas passou a exigir queda real de
  velocidade quando a volta traz a coluna `Speed`, o que corta toques de pedal no meio da reta.
- **A repetição começa onde o pé estava**: o cronômetro do exercício passou a ser ancorado no
  quadro em que o pedal sai do repouso (antes ele começava alguns pontos acima, quando o app
  confirmava o movimento), e a curva-alvo nasce nesse mesmo percentual em vez de zero. Numa
  pisada de 0,3 s isso tirava até 5% de curso do começo da curva e fazia o piloto aparecer
  fora da banda no primeiro quadro; agora o traçado e a curva partem juntos.
- 220 testes (eram 201), incluindo a geometria do traçado, o catálogo inteiro (com um teste
  que recusa volta costurada com vão longo), a biblioteca de referências e o alinhamento do
  início da repetição.

### 1.5.0

- **O próximo gráfico fica na tela o tempo todo**: a tela *Treino* ganhou a faixa
  **PRÓXIMA CURVA** no topo, que desenha a frenagem seguinte da referência — pela posição na
  pista ou pela ordem das zonas — inclusive enquanto você executa a curva atual. Antes a
  curva de referência só aparecia durante o desafio. O cartão do desafio em andamento
  desceu para a coluna da direita, liberando a largura do topo para o desenho da próxima
  curva aparecer inteiro.
- **A tela *Programas* também mostra a curva seguinte**: um painel permanente à direita
  desenha a próxima repetição (`PRÓXIMA REPETIÇÃO`, com a contagem e o alvo) ou a primeira
  repetição da etapa seguinte (`PRÓXIMA ETAPA`), e avisa quando o programa acabou. Antes, ao
  começar a repetição, o gráfico trocava para a curva atual e a próxima só reaparecia no fim.
- **A próxima curva ocupa o gráfico principal**: no *Treino*, enquanto você não está freando,
  o gráfico grande inteiro mostra a frenagem seguinte em âmbar tracejado (com banda de
  tolerância, largada, pico e etiqueta), em vez de ficar vazio esperando o primeiro toque no
  pedal. É o mesmo desenho da prévia dos programas, em outra cor para não se confundir com a
  referência azul nem com a entrada verde/vermelha.
- Etapas com alvo sorteado aparecem tracejadas no painel: a altura só é conhecida quando a
  repetição começa.
- 201 testes (eram 184).

### 1.4.0

- **A curva aparece antes de começar**: com o treino parado o gráfico já mostra a prévia da
  primeira repetição do programa escolhido, e no estágio *PRONTO* mostra a próxima curva —
  tracejada, com marcadores de largada e de pico e a etiqueta "pico 20% · 3,3 s". Antes o
  gráfico ficava vazio até o primeiro toque no pedal.
- **Tira do plano da repetição**: o cartão do exercício lista os trechos na ordem
  (`SUBIR até 20% · 0,3 s`, `SEGURAR em 20% · 1,0 s`, `SOLTAR até 0% · 2,0 s`), destacando o
  trecho em execução. Dá para saber o formato do exercício só de olhar, sem decorar o arquivo.
- **Aviso de troca de curva**: na última repetição de uma etapa, o app avisa qual é a etapa
  seguinte, que muda o desenho do gráfico.
- A prévia de etapas com alvo sorteado usa o meio da faixa (20 a 85% → desenho em 53%).
- 184 testes (eram 176).

### 1.3.0

- **Pisada rápida, soltura gradual**: nos programas embutidos o pedal vai do repouso ao alvo
  em 0,3–0,5 s e todo o tempo do exercício está na soltura, que é a parte que o pé precisa
  modular. Os programas de freio e de acelerador foram refeitos nessa pegada.
- **Arquivos de treino antigos se atualizam sozinhos**: se o `*.drill.json` da pasta de
  treinos ainda estiver idêntico ao padrão da versão 1, o app o regrava com a pegada nova
  (o formato passou para a versão 2). Arquivo renomeado, com percentuais próprios ou
  descrição própria é preservado.
- **Lista de etapas mostra a pegada**: cada etapa aparece como "pisa rápido, solta
  gradualmente", em vez de mostrar só a velocidade da pisada.
- 176 testes (eram 172).

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

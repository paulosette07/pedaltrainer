# Pedal Trainer

Treinador de frenagem para iRacing. Lê o seu conjunto de pedais pelo DirectInput, compara a
sua pisada com a de uma volta de referência e devolve uma nota de precisão curva a curva —
em vez de "eu acho que freie mais tarde", você vê o erro em pontos percentuais.

Aplicativo desktop para Windows, escrito em C# / .NET 8 com WPF. Não é um plugin: roda em
paralelo ao iRacing, lê a memória compartilhada do simulador e funciona mesmo com o jogo
fechado (modo simulado, para treinar em qualquer lugar).

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
  assinatura digital). Use "Mais informações → Executar assim mesmo".
- Não há instalador: baixe, coloque numa pasta e execute. Os dados ficam em
  `%APPDATA%\PedalTrainer` — nada é escrito ao lado do executável.

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
5. **Grava a sua própria referência.** Um clique grava uma volta do iRacing e a transforma
   na referência do treino.

---

## Telas

| Tela | Para quê |
| --- | --- |
| **Treino** | Barra de freio/acelerador ao vivo, gráfico de traço contra a curva de referência, medidor de precisão, nota ao vivo, zonas da volta e estatísticas da sessão. |
| **Programas** | Biblioteca de treinos do tipo "pise até 20%, segure, solte devagar", com alvo sorteado por repetição. Programas são arquivos JSON editáveis. |
| **Calibração** | Descobre qual eixo do controlador é o freio e qual é o acelerador, aprende a faixa bruta do pedal e ajusta zona morta, curva e suavização. |
| **Configurações** | Rigor do treino (gatilho de frenagem, banda de tolerância), janela do gráfico, alinhamento por distância, escolha do controlador e do modelo de pedal. |

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
4. **Abra o iRacing** (opcional, mas recomendado). Com o simulador publicando telemetria, o
   app alinha a frenagem pela **distância percorrida** (`LapDistPct`) em vez do tempo, então
   você não precisa frear no mesmo instante da referência — só no mesmo lugar da pista.

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

### Gravar a referência do iRacing

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
PedalTrainer.Core   domínio puro: entrada, referência, treino, drills, iRacing, persistência
PedalTrainer.App    WPF: janela, views, view models, controles de desenho e tema
PedalTrainer.Tests  xUnit, 128 testes cobrindo o núcleo sem precisar de hardware
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
| `IRacingSharedMemoryReader` | Leitura da memória compartilhada do iRacing por deslocamento de variável. |
| `LapRecorder` | Transforma uma volta do simulador em volta de referência. |
| `SettingsStore` / `CalibrationStore` | Persistência em JSON no perfil do usuário. |

---

## Onde ficam os arquivos

Tudo em `%APPDATA%\PedalTrainer` (nada é gravado ao lado do .exe):

```
%APPDATA%\PedalTrainer\
├── settings.json            preferências (rigor, gráfico, controlador, modelo de pedal)
├── calibration.json         faixa dos eixos, zona morta, curva, filtro
├── drills\
│   ├── freio-progressivo.drill.json
│   └── aceleracao-progressiva.drill.json
└── samples\
    ├── demo-lap.csv                       volta demo gerada pelo app
    └── volta-20260101-153000.csv          voltas gravadas do iRacing
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

# rodar os testes (128 testes)
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

Atalho para inspecionar controladores e eixos sem abrir a interface:

```powershell
dotnet run --project tools/PedalTrainer.Probe/PedalTrainer.Probe.csproj
```

---

## Limitações conhecidas

- **Somente Windows.** A aquisição usa DirectInput e a telemetria usa memória compartilhada
  do iRacing — ambos exclusivos da plataforma.
- **A leitura do iRacing é passiva.** O app não escreve nada no simulador e não é detectado
  como cheat; ele só lê os canais publicados (`Speed`, `Brake`, `Throttle`, `LapDistPct`,
  `SessionTime`, `Lap`).
- **Sem o iRacing aberto**, o alinhamento cai para o modo tempo (o desafio começa quando você
  pisa no freio e segue a curva por tempo, não por posição na pista).
- **Binário sem assinatura digital**, então o SmartScreen avisa na primeira execução.
- Voltas gravadas precisam cobrir pelo menos 90% da pista; voltas parciais são descartadas de
  propósito, para não gerar referência incompleta.

---

## Licença

MIT — veja [LICENSE](LICENSE).

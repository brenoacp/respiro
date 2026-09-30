# Respiro

App de bandeja para Windows que lembra você de fazer pausas — para os olhos, a postura e para levantar da cadeira.

Um pequeno anel ao lado do relógio vai se enchendo enquanto você trabalha e muda de cor conforme a pausa se aproxima. Quando chega a hora, o Respiro avisa com jeito e, se você ignorar, insiste um pouco mais.

## Recursos

- **Ícone que "respira"** — anel de progresso na bandeja: verde → âmbar → vermelho. O tooltip mostra quanto falta ("Pausa em 12 min").
- **Aviso escalonado** (padrão)
  1. Cartão discreto no canto, acima do relógio, sem roubar o foco.
  2. Ignorou por 2 min? A tela escurece em todos os monitores.
  3. Ignorou por mais 3 min? Bloqueio de 30 s, com botão **Emergência: pular**.
- **Modo só notificação** — apenas o cartão, repetido a cada 5 min, sem escurecer nem bloquear.
- **Dicas de saúde** — cada alerta sugere uma micro-ação: regra 20-20-20, alongar pescoço, ombros, punhos e costas, levantar e caminhar, beber água, respiração 4-7-8. Pode desligar e mostrar só "Faça uma pausa".
- **Som** — um som curto do Windows a cada aviso (desligável).
- **Detecta pausa real** — ficar 5 min sem mexer no mouse e no teclado conta como pausa; o ciclo recomeça sozinho. Suspender ou hibernar o PC também conta.
- **Não interrompe reuniões** — o alerta espera enquanto há um app em tela cheia, modo apresentação ou o microfone em uso (Teams, Zoom, Meet…).
- **Silenciar** por 1 hora ou até amanhã, **adiar** 15 min, **pausar agora**.
- **Iniciar com o Windows** opcional.

## Requisitos

- Windows 10 ou 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (para rodar o `.exe`)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (para compilar)

## Instalação

```bash
git clone https://github.com/brenoacp/respiro.git
cd respiro
dotnet publish src/Alerta -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Execute `publish/Respiro.exe`. O ícone aparece na bandeja, perto do relógio (se não aparecer, olhe na seta `^` de ícones ocultos e arraste-o para fora).

Para abrir junto com o Windows: clique com o botão direito no ícone → **Iniciar com o Windows**.

## Uso

Clique com o botão direito no ícone da bandeja:

| Item | O que faz |
|---|---|
| Pausar agora | Inicia a pausa imediatamente |
| Adiar 15 min | Empurra a próxima pausa em 15 min |
| Silenciar | Por 1 hora ou até amanhã (ícone fica cinza) |
| Retomar lembretes | Aparece enquanto estiver silenciado |
| Modo | Escalonado ou Só notificação |
| Mostrar dicas de saúde | Liga/desliga as sugestões de exercício |
| Tocar som | Liga/desliga o som dos avisos |
| Configurações... | Tempos de trabalho, pausa, ociosidade e escalonamento (também com duplo clique no ícone) |
| Iniciar com o Windows | Liga/desliga a inicialização automática |
| Sair | Fecha o app |

No cartão de alerta: **Iniciar pausa** (contagem regressiva do tempo de pausa), **Adiar 5 min** ou **Pular**.

## Configuração

As configurações ficam em `%APPDATA%\Respiro\settings.json` e podem ser editadas pela tela de Configurações ou à mão (valores fora do intervalo são corrigidos ao carregar).

| Campo | Padrão | Significado |
|---|---|---|
| `WorkSeconds` | 2700 (45 min) | Tempo de trabalho até a pausa |
| `BreakSeconds` | 300 (5 min) | Duração da pausa |
| `IdleResetSeconds` | 300 (5 min) | Tempo parado que conta como pausa |
| `Stage2AfterSeconds` | 120 (2 min) | Aviso ignorado até escurecer a tela |
| `Stage3AfterSeconds` | 180 (3 min) | Tela escura ignorada até bloquear |
| `LockSeconds` | 30 | Duração do bloqueio |
| `NotifyRepeatSeconds` | 300 (5 min) | Repetição no modo só notificação |
| `BusyRecheckSeconds` | 60 | Intervalo para checar reunião/tela cheia |
| `Mode` | `Escalating` | `Escalating` ou `NotifyOnly` |
| `ShowHealthTips` | `true` | Mostrar dicas de exercício |
| `PlaySound` | `true` | Tocar som nos avisos |

## Privacidade

O Respiro não acessa a internet e não grava nada além do próprio arquivo de configurações. Para saber se você está em reunião, ele consulta o estado de notificações do Windows (tela cheia/apresentação) e lê no registro **apenas se** algum app está usando o microfone neste momento — nunca o áudio.

## Desenvolvimento

```bash
dotnet build
dotnet test                                  # testes xUnit
dotnet run --project src/Alerta -- --demo    # intervalos curtos: trabalho 60 s, estágios a cada 20 s
```

No modo `--demo` as configurações não são salvas e o tooltip mostra `[demo]`.

### Estrutura

```
src/Alerta/
  Core/       lógica pura e testável: BreakScheduler (máquina de estados), Settings,
              ExerciseCatalog, SoundCue, textos e cores
  Platform/   Windows: ociosidade (GetLastInputInfo), reunião/tela cheia, iniciar com o Windows
  UI/         bandeja, cartão de alerta, escurecimento, tela de configurações
tests/Alerta.Tests/
docs/superpowers/   especificação e plano de implementação
```

O `BreakScheduler` não conhece a interface: recebe relógio, ociosidade e "ocupado" por interfaces e devolve um snapshot imutável a cada tick de 1 s. A UI só desenha esse snapshot — por isso quase toda a lógica é coberta por testes sem abrir janelas.

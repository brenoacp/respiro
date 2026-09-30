# Pausa — app de bandeja para lembrar de descansar (Windows)

## Context
O usuário quer um app Windows que fique ao lado do relógio (bandeja) e avise a hora de descansar. O foco é **saúde física**: olhos, postura e sedentarismo. A pasta `C:\temp\alerta` existe e está vazia; não é repositório git. O .NET 8 SDK está instalado.

**O que o usuário escolheu:**
- Insistência **escalonada** por padrão, com opção de usar o modo **só notificação**
- Ícone que "respira", detecção de pausa real, não interromper reunião, sugestões de exercício
- Stack: C# .NET 8 WinForms

**Padrões assumidos (ajustáveis nas Configurações):**
- Ciclo padrão: 45 min de trabalho e 5 min de pausa, configurável
- Ficar 5 min parado conta como pausa
- Interface em pt-BR
- Uso pessoal: sem instalador, um .exe só

## Design

### Comportamento
1. **Ciclo:** o timer de trabalho corre enquanto há atividade. Se você fica 5 min parado (`GetLastInputInfo`), a pausa conta como feita e o ciclo reinicia.
2. **Ícone que respira:** o ícone é desenhado em tempo de execução via GDI+. É um anel de progresso cuja cor vai de verde para âmbar e depois vermelho. O tooltip mostra "Pausa em 12 min". Atualiza a cada 30 s.
3. **Escalonamento** (modo padrão):
   - **Estágio 1**, quando chega a hora: popup próprio, sem borda, no canto inferior direito, logo acima do relógio. Mostra a sugestão de exercício e os botões `Iniciar pausa`, `Adiar 5 min` e `Pular`.
   - **Estágio 2**, se o estágio 1 for ignorado por 2 min: overlay escuro semitransparente (~40%) em todos os monitores, com o mesmo cartão.
   - **Estágio 3**, se ignorado por mais 3 min: overlay opaco com contagem regressiva de 30 s e o botão discreto `Emergência: pular`.
4. **Modo só notificação:** apenas o estágio 1, repetido a cada 5 min enquanto você não reagir.
5. **Tela de pausa:** o cartão do exercício com contagem regressiva do tempo de pausa. Ao terminar, o ciclo reinicia.
6. **Não interrompe reunião:** o alerta é adiado (reavaliado a cada 1 min) em dois casos:
   - `SHQueryUserNotificationState` indica tela cheia, apresentação ou "ocupado"
   - o microfone está em uso. A detecção lê o registro `HKCU\...\CapabilityAccessManager\ConsentStore\microphone\**\LastUsedTimeStop == 0`, o que cobre Teams, Zoom e Meet.

   Quando a reunião termina, o alerta sai em até 1 min.
7. **Exercícios:** catálogo interno em pt-BR com rotação sem repetir o anterior. Inclui 20-20-20 para os olhos, alongamento de pescoço, punhos e ombros, levantar e caminhar, beber água e respiração 4-7-8.
8. **Menu da bandeja:**
   - Pausar agora
   - Adiar 15 min
   - Silenciar (1 h / até amanhã)
   - Modo (Escalonado / Só notificação)
   - Configurações
   - Iniciar com o Windows
   - Sair

### Arquitetura (`C:\temp\alerta`)
```
Alerta.sln
src/Alerta/            (net8.0-windows, WinForms)
  Core/BreakScheduler.cs     máquina de estados pura: Working → Due(stage) → OnBreak / Snoozed / Suspended
  Core/IClock.cs, IIdleSource.cs, IBusySource.cs   interfaces injetáveis
  Core/ExerciseCatalog.cs    lista e rotação
  Core/Settings.cs + SettingsStore.cs   JSON em %APPDATA%\Alerta\settings.json
  Platform/IdleMonitor.cs    GetLastInputInfo (P/Invoke)
  Platform/BusyDetector.cs   SHQueryUserNotificationState + registro do microfone
  Platform/StartupRegistration.cs   HKCU\...\Run
  UI/TrayController.cs       NotifyIcon, menu, timer de 1 s que chama o scheduler
  UI/TrayIconRenderer.cs     progresso + cor → Icon (desenha o anel)
  UI/ToastForm.cs, DimOverlayForm.cs, LockOverlayForm.cs, BreakForm.cs, SettingsForm.cs
  Program.cs                 instância única (Mutex), ApplicationContext
tests/Alerta.Tests/    xUnit
```
- `BreakScheduler` não conhece a UI. Recebe `Tick(now, idle, busy)` e emite eventos ou estados. A UI só reage a eles. Isso permite testar tudo sem janelas.
- A interpolação de cor do renderer fica numa função pura, também testável.

### Erros e casos de borda
- Suspensão e hibernação: um salto de relógio maior que o limite de ociosidade conta como pausa.
- Settings corrompidos: carrega os padrões e regrava o arquivo.
- Segunda instância: o Mutex impede e a segunda sai sem fazer nada.
- Falha ao ler o registro do microfone: trata como "não ocupado".
- Multimonitor: um overlay por `Screen.AllScreens`.

### Testes
- xUnit sobre `BreakScheduler` com relógio falso e fontes de ociosidade e reunião falsas. Cobre:
  - escalonamento 1→2→3 nos tempos certos
  - modo só notificação repete o estágio 1
  - ociosidade reinicia o ciclo
  - reunião adia e libera o alerta ao terminar
  - adiar e silenciar
  - salto por suspensão
- Testes de `ExerciseCatalog` (sem repetição seguida) e da interpolação de cor.

## Próximos passos
1. Rodar `git init` em `C:\temp\alerta`.
2. Escrever a spec em `docs/superpowers/specs/2026-09-30-pausa-tray-design.md` e fazer o commit.
3. Você revisa a spec.
4. Skill `writing-plans` para o plano de implementação em TDD. Você escolhe o método de execução.

## Verificação
- `dotnet test` verde.
- `dotnet run --project src/Alerta` com intervalos curtos (config de debug: trabalho de 1 min, estágios a cada 20 s). Conferir:
  - cor do ícone e tooltip
  - toast perto do relógio, depois overlay escuro, depois bloqueio de 30 s
  - ficar parado reinicia o ciclo
  - com vídeo em tela cheia ou microfone aberto num teste do Teams, o alerta é adiado
- `dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true` gera um único .exe.

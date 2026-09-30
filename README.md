# Alerta

App de bandeja do Windows que lembra você de fazer pausas.

- Ícone ao lado do relógio muda de verde → âmbar → vermelho conforme a pausa se aproxima.
- Modo **Escalonado**: aviso discreto → tela escurecida → bloqueio de 30 s (com "Emergência: pular").
- Modo **Só notificação**: apenas o aviso, repetido a cada 5 min.
- Ficar 5 min sem mexer no computador conta como pausa.
- Não interrompe em tela cheia, apresentação ou com o microfone em uso (Teams, Zoom, Meet).

## Rodar

```bash
dotnet run --project src/Alerta            # normal
dotnet run --project src/Alerta -- --demo  # intervalos curtos para teste
dotnet test
```

## Publicar

```bash
dotnet publish src/Alerta -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Configurações: `%APPDATA%\Alerta\settings.json`.

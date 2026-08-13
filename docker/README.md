# Ollama-Setup

Der Ordner enthaelt die Container-Konfiguration fuer das lokale Sprachmodell.
SysDiag-AI funktioniert auch ohne diesen Teil: ohne erreichbares Ollama gibt
`sysdiag explain` einen Hinweis aus, alle anderen Befehle laufen normal weiter.

## Starten (CPU)

```bash
docker compose -f docker/docker-compose.yml up -d
docker exec sysdiag-ollama ollama pull llama3.2:3b
```

Das Modell belegt rund 2 GB und laeuft auf einer CPU in wenigen Sekunden pro
Antwort. Es ist bewusst klein gewaehlt, damit der Standardfall auf jedem Laptop
funktioniert.

## Starten (NVIDIA-GPU)

```bash
docker compose -f docker/docker-compose.yml -f docker/docker-compose.gpu.yml up -d
```

Voraussetzung ist das NVIDIA Container Toolkit auf dem Host.

## Modell wechseln

Der Modellname steht in `src/SysDiag.Cli/appsettings.json` unter
`Ollama.Model` und muss mit einem per `ollama pull` geladenen Tag
uebereinstimmen.

## Stoppen

```bash
docker compose -f docker/docker-compose.yml down
```

Die heruntergeladenen Modelle bleiben im Volume `ollama-models` erhalten.

# Sonorización del bar (TP VR)

Un bar común, sin guion: el lugar donde alguien jugaba al pool con su papá o a los dardos con amigos.
El sonido hace que el bar esté vivo aunque todavía no haya personas, y deja preparados los juegos para cuando sean jugables.
Todos los sonidos son grabaciones existentes con licencia CC0 o CC BY (ver `CREDITOS_AUDIO.md`).

## Cómo se usa

- En Unity, menú **Bar Audio → 2. Construir audio en la escena**: crea (o recrea) el objeto `Audio (Bar)` con todas las fuentes.
- **Bar Audio → 1. Reimportar sonidos**: vuelve a aplicar los ajustes de importación optimizados.
- Para probar sin celular: en Play, mantener **clic derecho** en la vista Game y mover el mouse para girar la cabeza.
- Para escuchar el "recuerdo": mirar fijo la mesa de pool, el tablero de dardos o el arcade ~2 segundos.

## Qué cubre cada punto de la consigna

| Punto | Dónde está |
|---|---|
| **Foley** | Canilla de cerveza, botellas, vasos (9 tomas), hielo, caja registradora, banquetas, puertas, bolas de pool, dardos. Cada disparo varía clip, volumen, pitch y posición (`RandomOneShots`). |
| **Voz** | Murmullo (*walla*) de fondo y en tres mesas, risas, festejos de los jugadores de pool. |
| **Música diegética** | Dos parlantes del bar (`MusicPlaylist`) sincronizados con `PlayScheduled`, filtrados como parlante (sin graves profundos ni agudos extremos). |
| **Música extradiegética** | *Sad Trio* en 2D (no sale de ningún lugar del bar), solo durante el recuerdo (`MemoryLayer`). |
| **Silencio** | Entre canción y canción hay 6–12 s sin música: aparecen el neón, la heladera y la lluvia. En el recuerdo el bar baja 10 dB y se filtra (snapshot *Recuerdo*): un silencio relativo. |
| **Sonido diegético / fuera de campo** | Lluvia de la calle por las ventanas y la puerta, cocina detrás de su puerta, gente que entra y sale (se escucha y no se ve). |
| **Espacialización** | Todas las fuentes de objetos son 3D con atenuación logarítmica y distancias ajustadas al salón; *spread* amplio para ventanas y murmullo, puntual para objetos. Reverb del salón (`AudioReverbZone`, ladrillo + madera, ~1,1 s). Oclusión simple: lo que está detrás de una puerta suena bajo y filtrado, y se abre cuando la puerta se abre (`DoorAudio`). |
| **Optimización** | Ver abajo. |

## Mixer (`Assets/Sounds/BarMixer.mixer`)

```
Master
├── Bar  (Lowpass Simple)   ← se aleja en el recuerdo
│   ├── Musica
│   ├── Ambiente
│   ├── Foley
│   └── Voces
├── Juegos                  ← pool, dardos, arcade: siguen claros en el recuerdo
└── Recuerdo                ← música extradiegética
```

Snapshots: **Normal** (Bar 0 dB, filtro abierto) y **Recuerdo** (Bar −10 dB, filtro a 1100 Hz, Juegos −2 dB). `MemoryLayer` mezcla los dos según hacia dónde mira el participante.

## Optimización

- **Importación** (`BarAudioImporter`): música en *Streaming* (no ocupa RAM), ambientes en *Compressed In Memory* Vorbis, foley y voces en *Decompress On Load* ADPCM (latencia mínima, casi sin CPU). Todo a 44,1 kHz (había originales a 96 kHz) y en **mono** salvo la cama estéreo del murmullo: una fuente 3D se colapsa a un punto, el estéreo solo gasta.
- Loops con crossfade (no hay clic en el punto de loop) y arranque en un punto al azar: un mismo archivo sirve para varias fuentes sin sonar "en fase".
- One-shots con 1–2 `AudioSource` reutilizables (nada de `PlayClipAtPoint`, que crea y destruye objetos).
- La música del recuerdo solo se decodifica mientras suena (se pausa al salir).
- Doppler en 0 (nada se mueve rápido) y ~25 voces sonando como máximo (el límite real es 32).
- Material nivelado antes de importar: ambientes a −23 LUFS, one-shots con pico a −1 dBFS, canciones igualadas entre sí.

## Cuando los juegos sean jugables

- **Pool**: apagar `simulateGames` en `Juegos/Pool` y llamar a `OnBallHit(punto, velocidad)` / `OnBallPocketed(punto)` desde las bolas.
- **Dardos / vasos / bolas**: `CollisionSound` reproduce un sonido al chocar con volumen según la velocidad.
- **Puertas**: `DoorAudio.OpenAndClose()` cuando entra un personaje. Para animar la hoja hay que sacarle *Static* a la puerta y agruparla con la manija.
- **HRTF**: si se instala un spatializer (Steam Audio, Resonance, Meta XR Audio) en *Project Settings → Audio*, al volver a construir las fuentes 3D lo usan solas.

## Archivos

- `Assets/Sounds/` — Musica, Ambiente, Foley, Voces (procesados) + `BarMixer.mixer`.
- `Assets/Scripts/Audio/` — `LoopEmitter`, `RandomOneShots`, `MusicPlaylist`, `PoolGameSimulator`, `DoorAudio`, `MemoryLayer`, `CollisionSound`, `EditorMouseLook`.
- `Assets/Scripts/Audio/Editor/` — `BarAudioImporter`, `BarAudioSceneBuilder`.
- `Audio_Originales/` (no se sube a git) — descargas originales; se pueden volver a bajar con los links de `CREDITOS_AUDIO.md`.

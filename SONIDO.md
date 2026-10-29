# Sonorización del bar (TP VR) — versión simple con Steam Audio

Un bar común, a última hora: suena la música del parlante, llueve afuera, la cocina trabaja detrás de su puerta
y la barra hace sus ruidos de siempre. Por ahora no hay voces.
Todos los sonidos son grabaciones existentes con licencia CC0 o CC BY (ver `CREDITOS_AUDIO.md`).

## Cómo funciona

- **Steam Audio** es el spatializer del proyecto (*Project Settings → Audio → Spatializer Plugin: Steam Audio Spatializer*).
  El `Steam Audio Manager` se crea solo al dar Play; no hay que agregar nada a la cámara.
- En la escena, todo el sonido está bajo el objeto **`Audio (Bar)`**, en cuatro grupos: `Musica`, `Ambiente`, `Foley` y `Juegos`.
- Cada sonido es un GameObject con tres componentes:
  1. **AudioSource**: 3D, *Spatialize* activado, rolloff logarítmico con *Min/Max Distance* según el tamaño del salón, Doppler en 0.
  2. **Steam Audio Source**: HRTF binaural, atenuación por distancia (*Curve Driven*: usa la curva del AudioSource) y absorción del aire.
  3. **BarSound** (`Assets/Scripts/Audio/BarSound.cs`): qué clips suenan y cuándo.
     - `loop` activado: ambiente que suena siempre, arranca en un punto al azar del archivo.
     - `loop` desactivado: elige un clip al azar (sin repetir el anterior), lo reproduce y espera `gap` segundos.
     - `volume` y `pitch`: rango al azar en cada disparo, para que no se note la repetición.
- Además queda una `AudioReverbZone` ("Reverb del salon", ladrillo + madera, ~1,1 s).

## Fuentes

| Grupo | Objeto | Sonido | Cómo suena |
|---|---|---|---|
| Musica | Parlante (musica del bar) | M1, M2, M3 | Playlist al azar desde la esquina trasera derecha, 4–8 s de silencio entre temas |
| Ambiente | Lluvia ventana izq. / der. | A3 | Loop, apagada "a través del vidrio" |
| Ambiente | Cocina (detras de la puerta) | A4 | Loop, apagada "a través de la puerta" |
| Ambiente | Heladera de cervezas | A6 | Loop, zumbido detrás de la barra |
| Ambiente | Neon palmera / Neon cartel EXIT | A5 | Loop, cada uno con otro pitch |
| Ambiente | Arcade fuera de servicio | A7 | Loop, zumbido eléctrico |
| Foley | Canilla, Abrir botella, Vasos, Hielo, Caja registradora, Botellas, Banquetas | F6, F7, F8, F9, F10, F11, F13 | Cada tanto, desde la barra |
| Foley | Puerta entrada / Puerta cocina | F4, F5 | Alguien entra o sale (se escucha, no se ve) |
| Juegos | Pool | F1, F2, F3 | Apertura, choques y bola a la tronera |
| Juegos | Dardos | F12 | Dardo que se clava en el tablero |

### Sonidos "del otro lado" (oclusión de Steam Audio sin geometría)

La lluvia y la cocina usan la **oclusión y transmisión de Steam Audio con valores fijos** (*Occlusion Input* y
*Transmission Input* en *User Defined*), así no hace falta exportar la geometría de la escena:

| Fuente | Occlusion Value | Transmission (low / mid / high) |
|---|---|---|
| Lluvia (vidrio) | 0 | 0.8 / 0.45 / 0.2 |
| Cocina (puerta de madera) | 0 | 0.45 / 0.15 / 0.05 |

Occlusion 0 = el camino directo está tapado; la transmisión dice cuánto pasa en graves, medios y agudos.
Para que suenen más o menos apagadas, se cambian esos tres valores en el Steam Audio Source.

## Agregar un sonido nuevo

1. Crear un objeto vacío dentro del grupo que corresponda y ubicarlo donde está la fuente.
2. *Add Component → BarSound*: agrega solo el AudioSource y el Steam Audio Source ya configurados.
3. Arrastrar los clips a `clips`, elegir `loop` o un `gap`, y ajustar *Volume* y *Min/Max Distance* en el AudioSource.

Ajustes de importación recomendados (los que ya tienen los `.meta` de `Assets/Sounds`):
música en *Streaming* + Vorbis, ambientes en *Compressed In Memory* + Vorbis, foley en *Decompress On Load* + ADPCM,
todo a 44,1 kHz y en **mono** (una fuente 3D se colapsa a un punto: el estéreo solo gasta).

## Probar

- Con auriculares (el HRTF solo se percibe así). En Play, mantener **clic derecho** en la vista Game y mover el mouse
  para girar la cabeza (`EditorMouseLook`).
- Para silenciar un sonido mientras se prueba, desactivar su GameObject.

## Qué cubre de la consigna

| Punto | Dónde está |
|---|---|
| **Foley** | Canilla, botella, vasos (9 tomas), hielo, caja, botellas, banquetas, puertas, pool y dardos, con variación de clip, volumen y pitch. |
| **Música diegética** | El parlante del bar (fuente 3D). |
| **Silencio** | Entre canción y canción quedan 4–8 s sin música: aparecen el neón, la heladera y la lluvia. |
| **Fuera de campo** | Lluvia de la calle, cocina detrás de su puerta y gente que entra o sale. |
| **Espacialización** | Steam Audio: HRTF, distancia, absorción del aire y oclusión/transmisión. Reverb del salón. |
| **Optimización** | 19 fuentes (límite real 32), sin mixer, `BarSound` sin Update (solo corrutinas), importación por tipo de sonido y todo en mono. |
| **Voz / música extradiegética** | Fuera por ahora. La versión anterior (voces, murmullo y capa de "recuerdo") queda en la rama `sound-test`. |

## Archivos

- `VR-Bar-Game/Assets/Sounds/` — Musica, Ambiente y Foley (procesados).
- `VR-Bar-Game/Assets/Scripts/Audio/` — `BarSound` y `EditorMouseLook`.
- `VR-Bar-Game/Assets/Plugins/SteamAudio/` — plugin Steam Audio 4.8.1 (configuración en `Resources/SteamAudioSettings.asset`).
- `Audio_Originales/` (no se sube a git) — descargas originales; se pueden volver a bajar con los links de `CREDITOS_AUDIO.md`.

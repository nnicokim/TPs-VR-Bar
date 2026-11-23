# Prueba en Cardboard — "La última ronda"

Cómo llevar el bar al celular y cómo probarlo con personas que no conocen el proyecto. La prueba es lo que justifica
las decisiones de diseño en el informe (`ENTREGA.md`): qué entendieron solos, dónde dudaron, si se marearon.

---

## 1. Lo que hay que arreglar antes de compilar

Revisando el proyecto encontré que **todavía no está configurado para Cardboard en Android**:

| Qué | Cómo está | Cómo tiene que quedar |
|---|---|---|
| XR Plug-in Management | Cardboard está tildado **solo en iOS**; en Android no hay ningún proveedor (OpenXR está solo en PC). | Pestaña **Android** → tildar **Cardboard XR Plugin**. |
| Orientación | *Auto Rotation* (se da vuelta y rompe la vista estéreo). | **Landscape Left**. |
| Punto de entrada | *GameActivity*. | **Activity** (Cardboard no anda con GameActivity). |
| Plantillas de Gradle | Desactivadas. | Activar *Custom Main Gradle Template* y *Custom Gradle Properties Template* (ver abajo). |
| Identificador | `com.UnityTechnologies.com.unity.template.urpblank` (el de la plantilla). | Uno propio, por ejemplo `com.itba.vrbar`. |

### Android (paso a paso)

1. **File → Build Profiles** → Android → *Switch Platform* (tarda la primera vez).
2. **Edit → Project Settings → XR Plug-in Management** → pestaña Android → tildar **Cardboard XR Plugin**.
3. **Project Settings → Player → Android**:
   - *Resolution and Presentation*: **Default Orientation = Landscape Left**.
   - *Other Settings*:
     - **Application Entry Point = Activity** (destildar GameActivity).
     - **Scripting Backend = IL2CPP**, **Target Architectures = ARM64** (ya está así).
     - **Graphics APIs**: dejar **OpenGLES3** primero (hoy está Vulkan primero). Si el celular anda bien con Vulkan,
       se puede volver a probar después; con OpenGLES3 hay menos sorpresas en celulares de gama media.
     - **Minimum API Level**: 25 (ya está). **Package Name**: el propio.
     - *Texture compression*: ETC2.
   - *Publishing Settings → Build*: tildar **Custom Main Gradle Template** y **Custom Gradle Properties Template**.
4. En `Assets/Plugins/Android/mainTemplate.gradle`, dentro de `dependencies { … }`, agregar las dependencias que pide
   la guía oficial de Cardboard (`androidx.appcompat:appcompat`, `com.google.android.gms:play-services-vision`, y las
   que liste la versión actual de la guía). En `Assets/Plugins/Android/gradleTemplate.properties` agregar:
   ```
   android.enableJetifier=true
   android.useAndroidX=true
   ```
   Guía: <https://developers.google.com/cardboard/develop/unity/quickstart>
5. Celular con **Opciones de desarrollador → Depuración USB**, conectado por cable → **Build And Run**.
6. La primera vez, Cardboard pide escanear el **QR del visor** (está en el visor o en su caja). Sin QR usa un perfil
   genérico que se ve igual pero con la distorsión menos precisa.

### iPhone

Cardboard ya está tildado para iOS. Hace falta una Mac con Xcode: *Switch Platform* a iOS → Build → abrir el proyecto en
Xcode, firmar con una cuenta de Apple y correr en el iPhone. Orientación **Landscape Left** también.

### Chequeos en el celular antes de probar con gente

- [ ] Se ven dos imágenes (una por ojo) y el retículo está en el centro de las dos.
- [ ] Girar la cabeza mueve la vista sin atraso; no hay saltos.
- [ ] El toque en la pantalla (o el botón del visor) elige.
- [ ] Los textos de la pizarra y las etiquetas se leen sin entrecerrar los ojos.
- [ ] Va fluido. Si se traba: bajar **Render Scale** del asset de URP a 0,7–0,8, apagar sombras en tiempo real
      (las luces ya están horneadas) y sacar post-procesado si lo hubiera.
- [ ] Batería por encima del 50 % y brillo alto (el celular se calienta y baja el rendimiento a los 10–15 min).
- [ ] El celular en modo avión (que no entren notificaciones en medio de la prueba).

---

## 2. Protocolo de la prueba

**Quiénes:** 4 a 6 personas que no hayan visto el proyecto, ideal con distinta experiencia en VR. Una sesión por
persona, ~15 min en total (8–10 dentro del visor).

**Roles:** uno guía (habla lo justo), otro observa y anota. Si se puede, grabar la pantalla del celular (la grabación
de Android sirve) y la persona desde afuera — sirve para el video.

### Antes (2 min)

- Explicar que se prueba **el bar, no a la persona**: si algo no se entiende, es un problema del diseño.
- Preguntar si se marea en autos o barcos y si usó VR antes. Avisar que puede sacarse el visor cuando quiera.
- Consigna única, siempre igual: *"Sos el barman de un bar en la última noche de lluvia. Mirá alrededor y hacé lo que
  te parezca. Para elegir algo, miralo fijo o tocá la pantalla."*
- Pedirle que **piense en voz alta** ("ahora estoy buscando…", "no sé qué es esto").

### Durante (8–10 min)

No explicar nada; solo responder "¿qué te parece que podés hacer?". Si alguien queda trabado más de 60 s en lo mismo,
dar una pista y anotarla. Tareas que se observan (en el orden en que la escena las va proponiendo):

| # | Tarea | Qué se mira |
|---|---|---|
| T1 | Llegar a la barra | ¿Descubre los aros del piso? ¿Cuánto tarda? ¿Entiende el teletransporte o se sorprende? |
| T2 | Servir el primer pedido | ¿Lee la pizarra? ¿Encuentra la canilla o la botella? ¿Sabe entregar el vaso? |
| T3 | Hora pico (dos pedidos a la vez) | ¿Se confunde de bebida? ¿Se estresa o se engancha? |
| T4 | El habitué | ¿Entiende que es una decisión? ¿Qué elige y por qué? |
| T5 | Cerrar el bar | ¿Usa la pizarra como lista? ¿Encuentra el tablero de luces y el parlante? |
| T6 | Libre: dardos, pool, arcade | ¿Los prueba sin que se lo digan? ¿Entiende que mirar más lejos es tirar más fuerte en el pool? ¿Alguien descubre el arcade? |

### Planilla de observación (una por persona)

| Momento | Qué hizo / dijo | Dudas (s) | Pista dada | Comodidad (gestos, quejas) |
|---|---|---|---|---|
| T1 |  |  |  |  |
| … |  |  |  |  |

### Después (4 min)

Cuestionario corto (1 = nada, 5 = mucho):

1. Sabía qué tenía que hacer en cada momento.
2. Me di cuenta enseguida de qué cosas se podían usar.
3. Elegir con la mirada fue cómodo.
4. Sentí que estaba en el bar.
5. El tiempo se me pasó rápido / estaba metido en lo que hacía.
6. Me sentí mareado o con náuseas. (≥ 3 → anotarlo y revisar qué estaba haciendo en ese momento)
7. Me cansé la vista o el cuello.

Preguntas abiertas: *¿Qué fue lo más raro? ¿En qué momento te perdiste? ¿Qué te dio ganas de seguir? ¿Qué cambiarías?*
¿Qué contó de su noche? (Si cuenta la historia — "cerré el bar", "le serví la última a Don Julio" — el significado llegó.)

---

## 3. Métricas del registro de sesión

Cada partida guarda un CSV (`tiempo_s;categoria;evento;detalle`) en:

- **Android:** `Android/data/<package name>/files/sesiones/` (se copia por cable desde la PC o con
  `adb pull /sdcard/Android/data/<package name>/files/sesiones`).
- **Editor (Windows):** `%USERPROFILE%\AppData\LocalLow\DefaultCompany\VR-Bar-Game\sesiones`.

Renombrar cada archivo con el número de participante y correr:

```
python analizar_sesiones.py carpeta_con_los_csv
```

Da por sesión y en promedio:

| Métrica | Qué dice | Eje |
|---|---|---|
| `primera_seleccion_s`, `primer_teletransporte_s` | Cuánto tarda en descubrir cómo se interactúa. | Ofrecimientos |
| `seg_por_pedido`, `errores` | Si la tarea se aprende (debería bajar de la apertura a la hora pico) y si las botellas se distinguen. | Flow / ofrecimientos |
| `sel_mirada` vs `sel_toque` | Qué forma de elegir prefieren; si casi todo es toque, el dwell es lento (o al revés). | Patrones |
| `teletransportes` | Si explora o se queda en la barra. | Significado |
| `tareas_cierre`, `llego_al_final`, `habitue` | Si termina la historia y qué decide. | Significado |
| `dardos`, `pool_tiros`, `pool_embocadas`, `arcade_revivio` | Si los extras se descubren y enganchan. | Flow / ofrecimientos escondidos |
| `duracion_min` | Duración real (para el confort). | Confort |

---

## 4. Confort (lo que ya está resuelto y lo que hay que vigilar)

| Riesgo | Cómo está resuelto | Qué vigilar en la prueba |
|---|---|---|
| Mareo por movimiento | No hay desplazamiento continuo: teletransporte con fundido a negro. La cámara nunca se mueve sola. | Preguntar justo después de cada teletransporte en las primeras sesiones. |
| Cuello | Lo importante está a la altura de la vista (pizarra, canillas, botellas con cartel). | Si alguien se queja de mirar para arriba (parlante) o para abajo (tablero de luces). |
| Ojos | Textos con tamaño angular fijo; destellos de relámpago cortos. | Si los relámpagos molestan, bajar su intensidad en `LightningStorm`. |
| Elecciones sin querer | Dwell de 1,2 s (por defecto) con anillo que se llena; en el pool, el tiro necesita 1,6 s quieto. | Elecciones que la persona dice que no quiso (anotarlas). |
| Calor / batería | Escena con luces horneadas. | Rendimiento a los 10 min. |

**Ajustes rápidos si la prueba lo pide:** `dwellTime` en cada objeto (o `defaultDwellTime` del `GazePointer`), `aimDwell`
y `fullPowerDistance` en `PoolGame`, tamaños de etiqueta en `GazeInteractable`.

# Interacción del bar (TP VR) — Etapa 1: mirada, teletransporte y canillas

**Concepto: "La última ronda".** El participante es el barman en el último turno de una noche de lluvia.
Los clientes siguen fuera de campo (se oyen, no se ven, como en `SONIDO.md`). En esta etapa queda armada la base de la
interacción para Cardboard (mirada + un toque) y la primera acción completa: **servir una cerveza**.

## Estado

Las etapas 1, 2 y 3 están armadas y guardadas en `SampleScene`: alcanza con abrir la escena y dar Play.
Cada etapa se armó con un menú temporal de Editor (*VR Bar → Armar Etapa N*) que se corrió una vez y se borró;
quedan en el historial de git por si hiciera falta rearmar algo. La etapa 4 (pool) se arma igual: ver al final.

Para probar en el celular ver `PRUEBA_CARDBOARD.md`; para el video y el informe, `ENTREGA.md`.

## Cómo probar (etapa 1)

- **En el Editor:** Play. *Clic derecho + mouse* para mirar (`EditorMouseLook`), *clic izquierdo* o *Espacio* para elegir,
  o simplemente mirar fijo ~1 s.
- **En el celular (Cardboard):** apuntar con la cabeza; tocar la pantalla o mirar fijo.

Recorrido de prueba: desde la entrada, mirar el aro **Barra** en el piso → aparece detrás de la barra mirando las canillas →
mirar una manija (se contornea y dice *Rubia*, *Roja*, *Negra* o *IPA*) → la jarra entra, la manija baja, sale el chorro,
la jarra se llena con espuma, la manija vuelve → la jarra "brilla" → mirarla (*Servir*) → se desliza hacia los clientes y
suena la caja. Probar también los aros **Pool**, **Dardos**, **Arcade**, **Ventana** y **Entrada**. Cada aro tiene arriba un indicador
flotante (a 1,55 m) que se ve por encima del mostrador y también se puede mirar para ir.

## Qué tiene la escena (etapa 1)

| Dónde | Qué |
|---|---|
| `Main Camera` | `GazePointer` (rayo de mirada + dwell + toque), `GazeReticle` (retículo), `ScreenFader` (fundido). |
| `XR Origin (VR)` | `GazeTeleporter`. |
| `Bar/Counter/beer_tap (1)` y `(2)` | La malla de manijas (una sola para las dos) se oculta y se reemplaza por dos mitades móviles: `[Etapa1] Canilla …` (pivote en la base, con `BeerTap`), `[Etapa1] Boca …` y `[Etapa1] Apoyo jarra …`. Se les saca *Batching Static* para que puedan moverse. |
| `Interaccion (Etapa 1)` | `Estacion de cerveza` (`PourStation`), `Puntos de teletransporte` (6 aros) y `Light probes barra`. |
| `Audio (Bar)/Foley/Canilla de cerveza` | `automatic = false`: ya no suena sola, la dispara la interacción (la mueve a la boca que se usa). `Vasos` y `Caja registradora` también se disparan al servir, sin dejar de sonar solos. |
| Vasos decorativos bajo las canillas | Se ocultan (la jarra de servicio ocupa ese lugar). |
| `Assets/Materials/Interaccion/` | Contorno, cerveza, espuma, vidrio de la jarra y marcador. |
| `Assets/Models/Tap/Generado/` | Las dos mitades de la malla de manijas. |

## Scripts (`Assets/Scripts/Interaction/`)

| Script | Qué hace |
|---|---|
| `IGazeable` | Contrato de "algo que se usa con la mirada": `CanInteract`, `DwellTime`, entrar / salir / elegir. |
| `GazeInteractable` | Base con los ofrecimientos comunes: contorno al mirar, pulso *attract* para "lo próximo", etiqueta flotante de tamaño angular constante. |
| `GazePointer` | Raycast desde la cámara, dwell (1,2 s por defecto) y toque. No re-elige por dwell lo mismo si se lo sigue mirando. |
| `GazeReticle` | Punto → anillo que crece sobre algo usable → relleno radial del dwell. Se ubica a la distancia de lo mirado y se dibuja encima de todo. Sin assets. |
| `ScreenFader`, `GazeTeleporter`, `TeleportPoint` | Teletransporte con fundido a negro, manteniendo la altura de los ojos y girando hacia donde apunta el punto. El punto donde uno está se oculta. |
| `BeerTap`, `PourStation`, `ServeMug` | La canilla, la secuencia servir → entregar y la jarra. `PourStation.BeerServed` queda listo para los pedidos de la etapa 2. |
| `Shaders/GazeOutline.shader` | Contorno *inverted hull* para URP. |
| `Audio/BarSound.cs` (cambio) | Nuevo `automatic`, `PlayOnce()`, `PlayOnceAt(pos)` y `FadeOut(s)`. Los sonidos existentes no cambian. |

## Qué cubre de la consigna (esta etapa)

| Eje | Dónde está |
|---|---|
| **Significado para el participante** | Un rol (barman) y una acción con sentido en el lugar: elegir qué cerveza servir y entregarla. El sonido de la canilla ahora es consecuencia de lo que hace el participante, no ambiente. |
| **Patrones de diseño** | *Selección:* apuntar con la cabeza + confirmación por toque o dwell. *Control del punto de vista:* teletransporte a lugares fijos con fundido (sin movimiento continuo, que marea). *Manipulación indirecta:* se elige la canilla y la acción se anima sola. *Control indirecto diegético:* nada de menús; las etiquetas aparecen solo al mirar. |
| **Ofrecimientos** | Retículo que cambia sobre lo usable, contorno cálido, aros que "respiran", etiquetas al mirar, jarra que brilla cuando hay que servirla. **Restricciones visibles:** mientras se sirve, o con una jarra esperando, las canillas no se resaltan. Mapeo natural: la manija se tira hacia uno. |
| **Flow** | Meta clara (llenar → servir), feedback inmediato (palanca, chorro, nivel, espuma, sonido, caja), cada paso dura poco y encadena con el siguiente, sin pantallas que saquen de la escena. |
| **Animación** | Manija con *ease-out* al tirar y rebote al soltar, jarra que entra y se va deslizando, chorro que crece y se corta desde arriba, llenado con espuma que se asienta, aros que respiran, retículo y fundidos. |

---

# Etapa 2: la noche (pedidos y actos), tragos, puertas y tormenta

## Cómo probar

Arrancar en la entrada: el aro **Barra** late. Al llegar a la barra:

1. **Apertura.** Suena la campanita, se abre la puerta de entrada y la pizarra (colgada sobre la barra) se balancea con
   el primer pedido: una cerveza. La canilla pedida late. Después, un **whisky**: la botella está **detrás** (en el estante);
   late y tintinea desde ahí para que el participante se dé vuelta, y su cartel dice **WHISKY**. Se mira la botella (o su cartel) → vuela al vaso con hielo, sirve y vuelve.
   Se mira el vaso → se entrega.
2. **Hora pico.** Sube la música; los clientes entran más seguido; hasta dos pedidos a la vez; sin pistas.
3. **Cierre.** Un relámpago marca el cambio, baja la música, los últimos se van y la pizarra muestra cómo fue la noche.

Durante toda la noche: la puerta de la cocina se abre cuando suena; al abrirse la de entrada sube la lluvia; cada
25–55 s un relámpago ilumina el salón. Servir algo que nadie pidió se anota como error en la pizarra (sin castigo).

## Qué agrega a la escena

| Dónde | Qué |
|---|---|
| `Interaccion (Etapa 2)/Pizarra de pedidos` | `OrderBoard`: pizarra colgada de cadenas, de frente al barman. Se arma al dar Play. |
| `Interaccion (Etapa 2)/Estacion de tragos` | `ShotStation` + `Apoyo vaso` a la izquierda de las canillas. El vaso (con hielo) se arma por código. |
| `Bar/Shelf Bottles/` Chivas, Absolut Vodka, Beefeater, Jagermeister, Olmeca, Havana_Club | `BottleSource` (Whisky, Vodka, Gin, Jäger, Tequila, Ron): las del estante de abajo, justo detrás del barman. Sin *Batching Static*. Cada una tiene un **cartel** en el borde del estante (WHISKY, GIN…), siempre visible, que distingue las usables de las decorativas y también se puede mirar para elegirla. |
| `Doors/Door Entrance` y `Door Kitchen` | `[Etapa2] Bisagra` con `DoorSwing` (la hoja y el picaporte cuelgan de ahí) + un *vano* detrás (noche / luz de cocina) para que al abrir no se vea la pared. |
| `Interaccion (Etapa 2)/Relampago (luz en tiempo real)` | `LightningStorm` + luz direccional que solo se enciende durante el destello. |
| `Interaccion (Etapa 2)/Pedidos y actos` | `OrderManager`: la noche en tres actos. Pedidos, nombres de clientes, música por acto y pistas. |
| `Audio (Bar)/…/[Etapa2] Trueno (ventana)` | Trueno (A8, 3 tomas): copia la configuración de *Lluvia ventana izq.* (suena apagado por el vidrio). |
| `Audio (Bar)/Foley` | `Puerta entrada`, `Abrir botella` y `Hielo` ya no suenan solos: los disparan los clientes y el participante. |

**Sonido:** no se agregó ni se generó ningún sonido. La pista de la apertura usa *Botellas (heladera)* (vuelve a su lugar).
**Trueno:** grabación real CC0 (BlueDelta, Freesound), editada en 3 tomas `A8_trueno_0X.wav` (ver `CREDITOS_AUDIO.md`).

## Scripts nuevos (`Assets/Scripts/Interaction/`)

| Script | Qué hace |
|---|---|
| `IDrinkStation` | Contrato común de las estaciones (cerveza y tragos): preparar, entregar, eventos `DrinkStarted` / `DrinkServed`. |
| `ShotStation`, `BottleSource` | Botella que se levanta, vuela en arco (Bézier), se inclina y sirve; vaso torneado por código con hielo. Cartel del estante por botella. |
| `GazeProxy` | Reenvía la mirada a otro objeto (mirar el cartel = mirar la botella). |
| `OrderManager`, `OrderBoard` | Los actos y los pedidos; la pizarra (texto de tiza, tachado al servir, nota de error, balanceo). |
| `DoorSwing` | Abre la puerta cuando suena su `BarSound` (evento `Played`) y sube la lluvia mientras está abierta. |
| `LightningStorm` | Destellos (2–3 pulsos) y trueno demorado 0,8–3 s. |
| `BarSound` (cambios) | Evento `Played`, `SetVolumeScale` (música por acto, lluvia con la puerta) y `ConfigureDefaults`. |

## Qué cubre de la consigna (etapa 2)

| Eje | Dónde está |
|---|---|
| **Significado** | Clientes con nombre que piden algo concreto; el participante es quien los atiende. La noche tiene un arco (abrir, llenarse, cerrar) y un cierre con el resumen de *su* noche. Lo que antes era fuera de campo (la puerta, la gente) ahora tiene consecuencias. |
| **Patrones** | Interfaz diegética (pizarra colgada, nada de HUD). Manipulación indirecta con animación (la botella "obedece"). El espacio 360° como parte del desafío: las botellas están detrás. Selección por mirada con restricciones (una preparación por estación). |
| **Ofrecimientos** | Carteles en el estante (WHISKY, GIN…) que marcan qué botellas se usan. Pistas solo al principio: lo pedido late y la botella **suena desde su lugar** (el sonido espacial guía la mirada). La pizarra se balancea cuando llega algo. El vaso lleno late hasta que se entrega. |
| **Flow** | Desafío creciente con la habilidad: apertura guiada de a un pedido → hora pico sin pistas y con dos pedidos a la vez → descompresión en el cierre. Sin tiempo límite ni castigo (el error se anota y se corrige), feedback inmediato en cada paso, música que acompaña la intensidad. |
| **Animación** | Botella en vuelo e inclinación, puertas con rebote al cerrarse, pizarra con balanceo amortiguado, relámpagos, vasos que entran y se van. |

---

# Etapa 3: cerrar el bar, el habitué, dardos y el arcade

## Cómo probar

- **El habitué.** Al terminar la hora pico, después de que se van los últimos, queda Don Julio: la pizarra pregunta
  *"¿Me servís la última, pibe?"* con dos opciones escritas con tiza que se eligen con la mirada: **Servirle** (pide un
  whisky; al servirlo, "Gracias, pibe. Hasta mañana.") o **Cerramos** (se pone el piloto y sale a la lluvia). La
  decisión aparece en la tarjeta final.
- **Cerrar el bar.** La pizarra pasa a ser una lista de tareas, en cualquier orden:
  - **Apagar la música:** mirar el parlante (caja con LED verde, arriba en la esquina): la música baja y el LED se apaga.
  - **Apagar los neones:** mirar el cartel EXIT y la palmera: parpadean (el zumbido tartamudea) y se apagan.
  - **Cerrar las cortinas:** mirar cada ventana: las cortinas se despliegan hasta el centro.
  - **Apagar las luces:** el **tablero de luces** junto a la puerta de la cocina (detrás de la barra) tiene tres llaves:
    SALÓN, JUEGOS y BARRA. Cada una apaga las bombillas de su zona y oscurece la vista un escalón (las luces están
    horneadas, así que la oscuridad es un velo sobre la cámara que se abre con cada relámpago).
  - Al terminar: "Buenas noches", oscuridad, un último relámpago ilumina el bar vacío y aparece la **tarjeta final** con
    el resumen de la noche y *"Mirá acá para volver a empezar"* (recarga la escena).
- **Dardos** (en cualquier momento, desde el aro *Dardos*): mirar el tablero y elegir → el dardo sale de la mano y se clava
  donde se mira, con un desvío (la puntería humana). Después de cada tiro aparece una **vista de cerca**: un diagrama del tablero
  (como en las transmisiones) con los dardos de la ronda —el último en amarillo, latiendo— y la jugada ("T20 · +60")
  durante ~3 s; además un anillo destella donde se clavó. Es un dibujo, no una segunda cámara: en URP + XR una cámara
  extra a textura ensucia el render del visor y es cara en el celular. Puntaje reglamentario (sectores, doble, triple, bull) en la pizarrita
  del costado. Tres tiros por ronda; después, mirar el tablero de nuevo junta los dardos (vuelan a la mesa). Alterna
  azules y rojos. La mejor ronda aparece en la tarjeta final.
- **El arcade "fuera de servicio"**: no tiene cartel ni contorno; solo el retículo delata que responde. Elegirlo (tocar, o
  mirarlo 2,5 s) es darle un golpe: se sacude y el zumbido tartamudea. Al tercero se cae el cartel, la pantalla se prende
  como un tubo viejo **sobre la pantalla real del modelo** (la busca por su material `MAT_Screen`, con su inclinación) y
  arranca un Pong que se juega solo ("INSERT COIN"); el zumbido roto se calma.

## Qué agrega a la escena

| Dónde | Qué |
|---|---|
| `Interaccion (Etapa 3)/[Etapa3] Parlante` | `SpeakerBox`: caja con cono y LED donde sale la música. |
| `Bar/Signs/Exit_Sign`, `Neon_Palm_Tree` | `NeonSign` + collider. Sin *Batching Static*. |
| `Windows/Window Left|Right/Cortinas` | `CurtainPair` + `[Etapa3] Zona cortinas` (collider sobre la ventana). |
| `Interaccion (Etapa 3)/[Etapa3] Tablero de luces` | Tres `LightSwitch` (placa, tecla y rótulo se arman solos) con las bombillas y luces de cada zona. |
| `Interaccion (Etapa 3)/Cierre del bar` | `ClosingSequence`: la lista de tareas, la oscuridad por escalones y el final. |
| `Bar/Darts/dartboard_1k` | `DartGame` + collider; usa los 6 dardos de `Darts Set (Generated)`. Pizarrita al costado. |
| `Bar/Arcade/Arcade` | `ArcadeSecret` + collider; usa el cartel `Out of Order`. |
| `Pedidos y actos` | Referencias a `Cierre del bar` y a los dardos (para el resumen). |
| `Audio (Bar)` | `Dardos` ya no suena solo (ahora tira el participante). La música y los zumbidos de neón se apagan con `BarSound.Silence`. |

## Scripts nuevos

| Script | Qué hace |
|---|---|
| `ClosingTask` | Base de las tareas de cierre: solo se pueden usar cuando el cierre las "arma"; laten mientras están pendientes. |
| `SpeakerBox`, `NeonSign`, `CurtainPair`, `LightSwitch` | Las cuatro tareas (con su animación). |
| `ClosingSequence`, `DarknessOverlay`, `EndCard` | El cierre, la oscuridad que se abre con los relámpagos y la tarjeta final con reinicio. |
| `Emission` | Prende/apaga la emisión sin tocar materiales (URP Lit y glTFast). |
| `DartGame` | Tiro apuntado con desvío gaussiano, dardo que tiembla al clavarse, puntaje reglamentario, juntar dardos, vista de cerca (diagrama del tablero dibujado por código con los dardos de la ronda) y anillo de impacto. |
| `ArcadeSecret`, `ArcadeScreen` | Golpes, cartel que cae, pantalla que se prende y Pong en modo demo. |
| `OrderBoard` (cambios) | Lista de tareas (`SetChecklist`) y opciones con la mirada (`ShowChoice`, `BoardButton`). |
| `OrderManager` (cambios) | El habitué y el paso al cierre; resumen de la noche. |
| `BarSound` (cambio) | `Silence(s)`: apaga y no deja que arranque el clip siguiente. |

## Qué cubre de la consigna (etapa 3)

| Eje | Dónde está |
|---|---|
| **Significado** | El participante cierra *su* bar: el final lo hace él, tarea por tarea. El habitué es una decisión sin respuesta correcta (¿la última o cerrar?) que queda en el resumen. La tarjeta final cuenta *su* noche. |
| **Patrones** | Lista de tareas diegética (pizarra) con orden libre; tablero de luces con llaves (mapeo natural: una llave, una zona); apuntar con la cabeza como mecánica de juego (dardos); reinicio diegético con la mirada. |
| **Ofrecimientos** | Lo pendiente late; el LED del parlante dice "prendido"; las llaves tienen la tecla arriba (prendida); el zumbido del neón guía hacia él. El arcade es un ofrecimiento **escondido**: solo el retículo delata que responde. |
| **Flow** | Descompresión después de la hora pico: tareas simples, sin tiempo, con feedback fuerte (oscuridad que avanza, silencio que crece). Los dardos son un desafío opcional con puntaje y "mejor ronda". |
| **Animación** | Parpadeo del neón, cortinas que se despliegan, teclas, oscuridad por escalones, último relámpago, vuelo y temblor del dardo, golpes al arcade, cartel que cae con rebote, tubo que se prende, Pong. |

---

# Etapa 4: pool con física, registro de sesión y prueba en Cardboard

## Dónde abrirla

El pool ya está armado en `SampleScene`. Abrí la escena y dale Play. El menú **VR Bar → Armar Etapa 4 (pool)**
reconstruye la mesa a partir del modelo importado si necesitás regenerarla.

## Cómo probar

- Ir al aro **Pool**. La bola blanca se contornea al mirar la mesa (*Jugar al pool*).
- Elegí la mesa mirando fijo o tocando. El taco aparece detrás de la blanca mientras apuntás y se guarda al golpear
  o al apartar la mirada. El anillo marca el punto al que apuntás
  sobre el paño, y la línea guía llega hasta el primer choque o tronera que encuentra en esa dirección.
- Para cambiar la fuerza sin cambiar la dirección, mové el retículo más cerca o más lejos de la blanca por la misma
  línea. El porcentaje indica la velocidad con la que saldrá la blanca respecto del máximo. También aparece en la
  barra del marcador. Mirar a menos de 6 cm de la blanca pausa el apuntado y reinicia el progreso del anillo.
- Para tirar, tocá la pantalla o sostené la mirada en un punto válido hasta que el anillo se llene (1,6 s).
  El taco retrocede, golpea y la blanca sale. Al perder el punto, el anillo vuelve a empezar. Mirar fuera de la mesa
  durante 1,5 s cancela el tiro.
- Una bola se emboca cuando su centro cruza la boca de la tronera. Si se emboca la blanca, se repone cuando termina
  el tiro, en su lugar de salida o en el punto libre más cercano.
- El marcador muestra las lisas (1 a 7), las rayadas (9 a 15) y la bola 8 por separado. La blanca no suma.
- **[ Rearmar mesa ]** (cartel sobre la banda del lado del aro) vuelve a armar el triángulo y pone en cero el marcador.
- Los tiros y las bolas embocadas aparecen en la tarjeta final.

## Qué agrega a la escena

| Dónde | Qué |
|---|---|
| `[Etapa4] Pool (juego)` | `PoolGame` en el centro de la mesa, con un collider para el paño, 6 bandas, bordes de seguridad y 6 bocas de tronera (`PoolPocket`). |
| `[Etapa4] Pool (juego)/Bola …` | 16 bolas físicas (`PoolBall`: `Rigidbody` + `SphereCollider`) con la malla y el material de las originales. |
| `[Etapa4] Pool (juego)/[Etapa4] Taco` | El taco que se usa para tirar (copia del que estaba sobre la mesa). |
| `[Etapa4] Pool (juego)/[Etapa4] Rearmar mesa` | `PoolResetButton`. |
| `pool_table_scene` | Las bolas originales y los tacos decorativos quedan desactivados. Las bandas físicas se alinean con el borde visible del modelo. |
| `Assets/Materials/Interaccion/Pool *.physicMaterial` | Paño (frena), banda (rebota 0,78) y bola (rebota 0,93, casi sin roce). |
| Fuente `Pool` (sonido) | Sirve de plantilla para las fuentes espaciales de apertura, choques y troneras. Pueden sonar a la vez desde donde ocurre cada evento. |
| `Pedidos y actos` | Referencia al pool (para el resumen). |

## Scripts nuevos

| Script | Qué hace |
|---|---|
| `PoolGame` | Proyecta la mirada sobre el plano del paño y usa el mismo punto para la guía y para confirmar por toque o mirada. Supervisa la física en pasos fijos, baja temporalmente el umbral de rebote durante el rodaje y lo restaura al terminar. Ilumina bolas y taco con armónicos esféricos configurables (`lampColor`, `lampIntensity`, `fillColor`). |
| `PoolBall`, `PoolPocket`, `PoolResetButton` | La bola se anima al caer y se puede reponer. La tronera evalúa el segmento recorrido por el centro de la bola entre pasos de física. El cartel repone el triángulo. |
| `SessionLog` | Registro de la sesión en CSV para la prueba con personas (ver abajo). |
| `BarSound` (cambio) | `PlaySpecific(clip, posición, volumen)`: toca un clip puntual en un lugar. |
| Varios (cambios) | `GazePointer`, `GazeTeleporter`, `OrderManager`, `ClosingTask`, `DartGame`, `ArcadeSecret` y `EndCard` anotan eventos en el registro. |

## Registro de sesión (para la prueba)

Cada sesión escribe `sesion_AAAAMMDD_HHMMSS.csv` en `Application.persistentDataPath/sesiones`
(en el Editor de Windows: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\VR-Bar-Game\sesiones`; en Android:
`/sdcard/Android/data/<id de la app>/files/sesiones`). Columnas: `tiempo_s;categoria;evento;detalle`.
Anota: cada elección (por mirada o por toque), teletransportes, pedidos (llegada, servido con los segundos que tardó,
errores), actos, el habitué, tareas del cierre, dardos, golpes al arcade, tiros y embocadas del pool, y el final.
Con eso salen las métricas de `PRUEBA_CARDBOARD.md` sin cronometrar a mano.

## Qué cubre de la consigna (etapa 4)

| Eje | Dónde está |
|---|---|
| **Significado** | El pool es el "después del turno": un momento propio del barman en su bar vacío o entre pedidos. Queda en el resumen de la noche. |
| **Patrones** | Apuntar con la cabeza + dwell (la mirada es el taco); la distancia de la mirada es la fuerza (un solo canal de entrada, dos parámetros); cancelar mirando afuera; reinicio diegético. |
| **Ofrecimientos** | La blanca contorneada dice "por acá se empieza"; la línea y el anillo muestran adónde va el tiro antes de tirar; el anillo que se llena anticipa el disparo (y da tiempo a arrepentirse). |
| **Flow** | Desafío abierto y sin fin: la habilidad (apuntar y medir la fuerza) mejora tiro a tiro; feedback inmediato (sonido de cada choque, bola que cae). Sin puntaje que presione. |
| **Animación** | Física de bolas, taco que se alinea, retrocede y pega, bola que cae en la tronera, blanca que reaparece. |

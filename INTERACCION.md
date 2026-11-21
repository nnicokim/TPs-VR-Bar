# Interacción del bar (TP VR) — Etapa 1: mirada, teletransporte y canillas

**Concepto: "La última ronda".** El participante es el barman en el último turno de una noche de lluvia.
Los clientes siguen fuera de campo (se oyen, no se ven, como en `SONIDO.md`). En esta etapa queda armada la base de la
interacción para Cardboard (mirada + un toque) y la primera acción completa: **servir una cerveza**.

## Estado

La etapa 1 ya está armada y guardada en `SampleScene` (el menú que la armaba se borró: queda en el historial de git).
La etapa 2 se arma con un menú temporal (ver más abajo) que también se borra una vez guardada la escena.

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

## Cómo armarla (una sola vez)

1. Abrir `SampleScene` y esperar a que compile.
2. Menú **VR Bar → Armar Etapa 2 (pedidos, tragos, puertas y tormenta)**. La Consola muestra un resumen
   (en naranja, lo que no encontró).
3. Guardar la escena (Ctrl+S). No hace falta rehornear la iluminación.
4. Probar, commitear la escena y **borrar** `Assets/Scripts/Editor/SetupEtapa2.cs` (correrlo de nuevo rehace los objetos
   y pierde los ajustes manuales).

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
| `Audio (Bar)/…/[Etapa2] Trueno (ventana)` | Solo si hay una grabación de trueno en `Assets/Sounds`: copia la configuración de *Lluvia ventana izq.* (suena apagado por el vidrio). |
| `Audio (Bar)/Foley` | `Puerta entrada`, `Abrir botella` y `Hielo` ya no suenan solos: los disparan los clientes y el participante. |

**Sonido:** no se agregó ni se generó ningún sonido. La pista de la apertura usa *Botellas (heladera)* (vuelve a su lugar).
**Trueno:** no hay grabación todavía; el relámpago es mudo hasta agregar una con licencia libre (ver `CREDITOS_AUDIO.md`)
y volver a correr el menú.

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

## Próxima etapa

3. Juegos (dardos y pool), arcade "secreto", el pedido opcional del habitué ("¿una más?") y la secuencia de cierre
   (neones, cortinas y luces) con Timeline.

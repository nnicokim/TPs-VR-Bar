# Interacción del bar (TP VR) — Etapa 1: mirada, teletransporte y canillas

**Concepto: "La última ronda".** El participante es el barman en el último turno de una noche de lluvia.
Los clientes siguen fuera de campo (se oyen, no se ven, como en `SONIDO.md`). En esta etapa queda armada la base de la
interacción para Cardboard (mirada + un toque) y la primera acción completa: **servir una cerveza**.

## Cómo armarlo en la escena (una sola vez)

1. Abrir `SampleScene` y esperar a que Unity compile los scripts nuevos.
2. Menú **VR Bar → Armar Etapa 1 (mirada, teletransporte y canilla)**. La Consola muestra un resumen de lo que hizo
   (y avisos en naranja si algo no lo encontró o si un punto de teletransporte se superpone con un mueble).
3. **Guardar la escena** (Ctrl+S).
4. **Recomendado:** *Window → Rendering → Lighting → Generate Lighting*. Las luces del bar son *Baked* y lo que se mueve
   (manijas, jarra) no está en el lightmap: el armado agrega light probes en la barra, pero necesitan un horneado para tener luz.

El menú se puede correr de nuevo: borra lo que armó antes y lo rearma. Todo queda en un solo *Deshacer* (Ctrl+Z).

## Cómo probar

- **En el Editor:** Play. *Clic derecho + mouse* para mirar (`EditorMouseLook`), *clic izquierdo* o *Espacio* para elegir,
  o simplemente mirar fijo ~1 s.
- **En el celular (Cardboard):** apuntar con la cabeza; tocar la pantalla o mirar fijo.

Recorrido de prueba: desde la entrada, mirar el aro **Barra** en el piso → aparece detrás de la barra mirando las canillas →
mirar una manija (se contornea y dice *Rubia*, *Roja*, *Negra* o *IPA*) → la jarra entra, la manija baja, sale el chorro,
la jarra se llena con espuma, la manija vuelve → la jarra "brilla" → mirarla (*Servir*) → se desliza hacia los clientes y
suena la caja. Probar también los aros **Pool**, **Dardos**, **Arcade**, **Ventana** y **Entrada**.

## Qué agrega el menú a la escena

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
| `Editor/SetupEtapa1.cs` | El menú que arma todo. |
| `Audio/BarSound.cs` (cambio) | Nuevo `automatic`, `PlayOnce()`, `PlayOnceAt(pos)` y `FadeOut(s)`. Los sonidos existentes no cambian. |

## Qué cubre de la consigna (esta etapa)

| Eje | Dónde está |
|---|---|
| **Significado para el participante** | Un rol (barman) y una acción con sentido en el lugar: elegir qué cerveza servir y entregarla. El sonido de la canilla ahora es consecuencia de lo que hace el participante, no ambiente. |
| **Patrones de diseño** | *Selección:* apuntar con la cabeza + confirmación por toque o dwell. *Control del punto de vista:* teletransporte a lugares fijos con fundido (sin movimiento continuo, que marea). *Manipulación indirecta:* se elige la canilla y la acción se anima sola. *Control indirecto diegético:* nada de menús; las etiquetas aparecen solo al mirar. |
| **Ofrecimientos** | Retículo que cambia sobre lo usable, contorno cálido, aros que "respiran", etiquetas al mirar, jarra que brilla cuando hay que servirla. **Restricciones visibles:** mientras se sirve, o con una jarra esperando, las canillas no se resaltan. Mapeo natural: la manija se tira hacia uno. |
| **Flow** | Meta clara (llenar → servir), feedback inmediato (palanca, chorro, nivel, espuma, sonido, caja), cada paso dura poco y encadena con el siguiente, sin pantallas que saquen de la escena. |
| **Animación** | Manija con *ease-out* al tirar y rebote al soltar, jarra que entra y se va deslizando, chorro que crece y se corta desde arriba, llenado con espuma que se asienta, aros que respiran, retículo y fundidos. |

## Próximas etapas

2. Pedidos (comandera + pizarra) y los tres actos (apertura, hora pico y cierre), botellas del estante, puertas animadas
   en sincronía con su sonido y relámpago con trueno.
3. Juegos (dardos y pool), arcade "secreto" y la secuencia de cierre (neones, cortinas y luces) con Timeline.

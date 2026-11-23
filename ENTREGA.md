# Entrega — "La última ronda" (TP de interactividad y animación)

Guía para el video y el informe. Lo técnico de cada etapa está en `INTERACCION.md`, el sonido en `SONIDO.md` y la
prueba con personas en `PRUEBA_CARDBOARD.md`.

---

## 1. Video (3–4 min)

Grabar **desde el celular en el visor** (grabación de pantalla de Android: se ven los dos ojos, que es lo honesto para
Cardboard) y algunas tomas **desde afuera** de alguien jugando (ayuda a ver el toque y el movimiento de cabeza). Para
planos limpios se puede grabar también el Game view del Editor en una sola vista; aclararlo en el video.

| Tiempo | Plano | Qué se ve | Qué se dice (voz en off o cartel) |
|---|---|---|---|
| 0:00–0:15 | Entrada, lluvia, neones | Llegada al bar vacío, relámpago por la ventana. | "Sos el barman. Es la última noche de lluvia antes de cerrar." |
| 0:15–0:40 | Aro **Barra** → teletransporte | Retículo sobre el aro, anillo que se llena, fundido. | Mirada + un toque; teletransporte con fundido para no marear. |
| 0:40–1:15 | Primer pedido (apertura) | Pizarra que se balancea, lo pedido late, la botella suena desde su lugar, servir. | Ofrecimientos: lo que se puede usar late, el sonido guía la mirada. |
| 1:15–1:50 | Hora pico | Dos pedidos, tragos con botellas rotuladas, un error y su corrección. | Flow: sube la exigencia cuando ya se aprendió; sin castigo. |
| 1:50–2:15 | El habitué | Las dos opciones con tiza; elegir una. | Significado: una decisión sin respuesta correcta que queda en la historia. |
| 2:15–2:50 | Cerrar el bar | Música que baja, neón que parpadea, cortinas, llaves; oscuridad por escalones; último relámpago. | El participante termina *su* noche; lista de tareas diegética. |
| 2:50–3:20 | Juegos | Dardo con vista de cerca; tiro de pool apuntando con la mirada; golpes al arcade y Pong. | Patrones: apuntar con la cabeza; el arcade como ofrecimiento escondido. |
| 3:20–3:40 | Tarjeta final | Resumen de *su* noche, reinicio con la mirada. | — |
| 3:40–4:00 | Prueba con personas | 2–3 tomas cortas de participantes + un dato de la prueba. | "Lo probaron N personas: …" |

Consejos: grabar cada parte por separado (es más fácil repetir), sin música encima del sonido del bar (el sonido es
parte del trabajo), y con subtítulos cortos en vez de mucha voz.

---

## 2. Informe (estructura sugerida)

1. **Concepto** (½ página). La última ronda: quién es el participante, qué pasa, por qué un bar (lugar conocido,
   acciones con las manos que se pueden traducir a mirada + toque).
2. **Restricciones de la plataforma.** Cardboard: 3 grados de libertad, sin controles, una sola entrada (toque o
   mirada sostenida), celular que se calienta. Cada decisión de diseño parte de acá.
3. **Los cuatro ejes**, cada uno con ejemplos concretos y, si hay, lo que se vio en la prueba:

   | Eje | Qué contar | Evidencia de la prueba |
   |---|---|---|
   | **Significado para el participante** | Rol (barman), historia en actos (apertura → hora pico → habitué → cierre), decisiones que quedan en el resumen, el final lo hace él. | ¿Cómo contaron su noche? ¿Qué eligieron con el habitué? `llego_al_final`. |
   | **Patrones de diseño** | Mirada + dwell con anillo, teletransporte a puntos fijos con fundido, etiquetas de tamaño angular fijo, pizarra como interfaz diegética (pedidos, lista de tareas, opciones), apuntar con la cabeza (dardos, pool), reinicio diegético. | `sel_mirada` vs `sel_toque`, `primer_teletransporte_s`. |
   | **Ofrecimientos** | Contorno al mirar, lo pendiente late, pistas solo al principio, sonido espacial que guía, carteles de las botellas, llaves con la tecla arriba, la línea del pool; el arcade como ofrecimiento escondido (solo el retículo lo delata). | `primera_seleccion_s`, `errores`, quién descubrió el arcade, dudas anotadas. |
   | **Flow** | Desafío que crece con la habilidad (apertura guiada → hora pico → descompresión), sin tiempo límite ni castigo, feedback inmediato, extras opcionales con puntaje (dardos, pool). | `seg_por_pedido` de la apertura vs la hora pico, respuestas 1, 5 del cuestionario. |

4. **Animación.** Lista breve por tipo: procedurales (balanceo de la pizarra, puertas con rebote, botella en vuelo,
   taco), físicas (pool), de estado (neón, cortinas, oscuridad, tubo del arcade), de feedback (anillo de impacto,
   retículo). Por qué se hizo por código (curvas de `Ease`) y no con Animator: todo reacciona a lo que hace el participante.
5. **Sonido.** Resumen de `SONIDO.md`: Steam Audio, sonidos que salen de donde pasa la acción, mezcla por acto. Créditos
   en `CREDITOS_AUDIO.md` (no se usó ningún sonido generado).
6. **Prueba con personas.** Cuántas, cómo (protocolo de `PRUEBA_CARDBOARD.md`), tabla de métricas (salida de
   `analizar_sesiones.py`), promedios del cuestionario, 3–5 hallazgos y **qué se cambió por cada uno** (o qué se
   cambiaría).
7. **Limitaciones y trabajo futuro.** Luces horneadas (la oscuridad es un velo), clientes que se oyen pero no se ven,
   3 grados de libertad (no se puede inclinar sobre la barra), lo que haya salido de la prueba.
8. **Créditos.** Modelos, sonidos (ver `CREDITOS_AUDIO.md`), plugins (Cardboard XR Plugin, Steam Audio, TextMeshPro).

---

## 3. Lista de entrega

- [ ] Escena guardada con las 4 etapas; sin menús temporales (`Assets/Scripts/Editor` borrada).
- [ ] Build para el celular probada (APK o el proyecto de Xcode).
- [ ] CSV de las sesiones de prueba y la salida de `analizar_sesiones.py`.
- [ ] Planillas de observación y cuestionarios.
- [ ] Video.
- [ ] Informe.
- [ ] Repositorio con todo commiteado (sin `Audio_Originales/`, que no se sube).

using System.Collections;
using System.Collections.Generic;
using BarAudio;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BarInteraccion
{
    /// <summary>
    /// Pool con física, pensado para Cardboard (mirada + un toque):
    ///   1. Mirar la mesa y elegir → se toma el taco: aparece detrás de la bola blanca.
    ///   2. Mirar un punto del paño: el taco y una línea guía apuntan hacia ahí. Más lejos = más fuerte.
    ///   3. Tocar (o sostener la mirada quieta ~1,6 s, el anillo se llena) → el taco retrocede y golpea.
    ///   Mirar fuera de la mesa un momento cancela el tiro.
    /// Las bolas son cuerpos rígidos esféricos (el modelo original está estirado en altura y se ocultó);
    /// bandas con rebote, troneras que se tragan las bolas, sonidos de choque, apertura y tronera.
    /// La blanca embocada vuelve a su lugar. "Rearmar" vuelve a poner el triángulo.
    /// </summary>
    public class PoolGame : GazeInteractable
    {
        public enum Phase { Idle, Aiming, Rolling }

        [Header("Mesa")]
        public float feltY = 0.99f;
        [Tooltip("Mitad del campo de juego (x, z) en el mundo, centrado en este objeto.")]
        public Vector2 halfField = new Vector2(1.17f, 0.56f);
        public PoolBall cueBall;
        public PoolBall[] balls;

        [Header("Taco")]
        public Transform cue;
        public Vector3 cueTipLocal = new Vector3(0f, 0f, 0.74f);
        public Vector3 cueBackLocal = new Vector3(0f, 0f, -0.74f);

        [Header("Tiro")]
        public float minSpeed = 0.5f;
        public float maxSpeed = 3.4f;
        [Tooltip("Distancia del punto apuntado (desde la blanca) que da la fuerza máxima.")]
        public float fullPowerDistance = 1.2f;
        [Tooltip("Segundos de mirada quieta para tirar (además del toque).")]
        public float aimDwell = 1.6f;
        public float maxPlayDistance = 3.2f;

        [Header("Sonido (fuente 'Pool' de la escena)")]
        public BarSound sound;
        public AudioClip breakClip, hitClip, pocketClip;

        [Header("Visual")]
        public Material lineMaterial;

        public Phase State { get; private set; } = Phase.Idle;
        public int Shots { get; private set; }
        public int Pocketed { get; private set; }

        Vector3 aimPoint, aimDir = Vector3.right, lastStable;
        float dwell, offTable, lastHitSound;
        bool firstBreakDone;
        Vector3 cueRestPos;
        Quaternion cueRestRot;
        Transform guide;
        RectTransform marker;
        Image markerFill;
        float idleDwell;

        void Awake()
        {
            // Física de billar: rebotes también a baja velocidad y pasos chicos (bolas de 5,7 cm).
            Physics.bounceThreshold = 0.15f;
            Time.fixedDeltaTime = 0.01f;
            if (cue != null) { cueRestPos = cue.position; cueRestRot = cue.rotation; }
            foreach (var b in balls) if (b != null) b.game = this;
            BuildGuide();
            if (string.IsNullOrEmpty(label)) label = "Jugar al pool";
            if (highlightRoot == null && cueBall != null) highlightRoot = cueBall.transform;
            idleDwell = dwellTime > 0f ? dwellTime : 1.2f;
            dwellTime = idleDwell;
        }

        protected override bool IsAvailable()
        {
            if (State == Phase.Rolling || cueBall == null) return false;
            var cam = GazePointer.Instance != null ? GazePointer.Instance.transform : null;
            return cam != null && Vector3.Distance(Flat(cam.position), Flat(transform.position)) <= maxPlayDistance + halfField.x;
        }

        protected override Vector3 LabelPosition => State == Phase.Aiming
            ? aimPoint + Vector3.up * 0.14f
            : (cueBall != null ? cueBall.transform.position : transform.position) + Vector3.up * 0.3f;

        public override void OnSelect(GazePointer pointer)
        {
            if (!IsAvailable()) return;
            if (State == Phase.Idle) StartAiming(pointer);
            else if (State == Phase.Aiming) StartCoroutine(Shoot());
        }

        // ---------- Apuntar ----------

        void StartAiming(GazePointer pointer)
        {
            State = Phase.Aiming;
            dwellTime = 999f; // mientras se apunta, el tiro lo dispara el anillo propio (o un toque)
            aimPoint = OnFelt(pointer.HitPoint);
            lastStable = aimPoint;
            dwell = 0f; offTable = 0f;
            guide.gameObject.SetActive(true);
            marker.gameObject.SetActive(true);
            SessionLog.Event("pool", "apuntar");
        }

        void CancelAiming()
        {
            State = Phase.Idle;
            dwellTime = idleDwell;
            guide.gameObject.SetActive(false);
            marker.gameObject.SetActive(false);
            StartCoroutine(ReturnCue());
            label = "Jugar al pool";
        }

        void Update()
        {
            if (State != Phase.Aiming) return;
            var pointer = GazePointer.Instance;
            if (pointer == null) return;

            bool onTable = ReferenceEquals(pointer.Current, this);
            if (!onTable)
            {
                offTable += Time.deltaTime;
                if (offTable > 1.5f) CancelAiming();
                return;
            }
            offTable = 0f;

            Vector3 p = OnFelt(pointer.HitPoint);
            Vector3 c = cueBall.transform.position;
            Vector3 d = Flat(p - c);
            if (d.magnitude > 0.06f) { aimPoint = p; aimDir = d.normalized; }
            float power = Mathf.Clamp01(Flat(aimPoint - c).magnitude / fullPowerDistance);
            label = $"Tirar · {Mathf.RoundToInt(Mathf.Lerp(15f, 100f, power))}%";

            // Dwell propio: el puntero ya "eligió" la mesa, así que la quietud de la mirada dispara el tiro.
            if ((aimPoint - lastStable).magnitude < 0.03f) dwell += Time.deltaTime;
            else { dwell = 0f; lastStable = aimPoint; }
            markerFill.fillAmount = Mathf.Clamp01(dwell / aimDwell);

            PlaceCue(0.03f + 0.04f * Mathf.PingPong(Time.time * 1.5f, 1f)); // el taco "respira" mientras se apunta
            UpdateGuide(c);
            marker.position = new Vector3(aimPoint.x, feltY + 0.004f, aimPoint.z);
            marker.rotation = Quaternion.LookRotation(Vector3.down, aimDir);

            if (dwell >= aimDwell) StartCoroutine(Shoot());
        }

        IEnumerator Shoot()
        {
            if (State != Phase.Aiming) yield break;
            State = Phase.Rolling;
            Shots++;
            float power = Mathf.Clamp01(Flat(aimPoint - cueBall.transform.position).magnitude / fullPowerDistance);
            guide.gameObject.SetActive(false);
            marker.gameObject.SetActive(false);
            label = "";

            // El taco retrocede y golpea.
            float back = Mathf.Lerp(0.08f, 0.22f, power);
            yield return Ease.Run(0.3f, t => PlaceCue(Mathf.Lerp(0.05f, back, Ease.OutCubic(t))));
            yield return Ease.Run(0.06f, t => PlaceCue(Mathf.Lerp(back, 0.005f, Ease.InCubic(t))));
            float speed = Mathf.Lerp(minSpeed, maxSpeed, power);
            cueBall.Body.WakeUp();
            cueBall.Body.linearVelocity = aimDir * speed;
            PlayHit(cueBall.transform.position, Mathf.Lerp(0.3f, 0.8f, power));
            SessionLog.Event("pool", "tiro", $"{Mathf.RoundToInt(power * 100)}%");
            StartCoroutine(ReturnCue());

            // Esperar a que todo se quede quieto.
            float still = 0f, elapsed = 0f;
            yield return new WaitForSeconds(0.3f);
            while (still < 0.6f && elapsed < 15f)
            {
                elapsed += Time.deltaTime;
                bool moving = false;
                foreach (var b in balls)
                    if (b != null && !b.Pocketed && b.Body.linearVelocity.sqrMagnitude > 0.0009f) { moving = true; break; }
                still = moving ? 0f : still + Time.deltaTime;
                yield return null;
            }
            foreach (var b in balls) if (b != null && !b.Pocketed) b.Stop();

            if (cueBall.Pocketed) yield return RespawnCueBall();
            State = Phase.Idle;
            dwellTime = idleDwell;
            label = "Jugar al pool";
        }

        void PlaceCue(float gap)
        {
            if (cue == null) return;
            Vector3 c = cueBall.transform.position;
            Vector3 dir = (aimDir + Vector3.down * 0.08f).normalized; // levemente inclinado, como un taco real
            Vector3 tip = c - dir * (cueBall.radius + gap);
            Vector3 axis = (cueTipLocal - cueBackLocal).normalized;
            Quaternion rot = Quaternion.FromToRotation(axis, dir);
            cue.SetPositionAndRotation(tip - rot * cueTipLocal, rot);
        }

        IEnumerator ReturnCue()
        {
            if (cue == null) yield break;
            yield return new WaitForSeconds(0.5f);
            Vector3 p0 = cue.position; Quaternion r0 = cue.rotation;
            yield return Ease.Run(0.6f, t =>
            {
                float e = Ease.InOutCubic(t);
                cue.SetPositionAndRotation(Vector3.Lerp(p0, cueRestPos, e), Quaternion.Slerp(r0, cueRestRot, e));
            });
        }

        // ---------- Bolas ----------

        public void OnBallCollision(PoolBall ball, Collision collision)
        {
            if (collision.collider.GetComponent<PoolBall>() == null) return;
            float v = collision.relativeVelocity.magnitude;
            if (v < 0.12f || Time.time - lastHitSound < 0.04f) return;
            if (!firstBreakDone && v > 1.4f && breakClip != null)
            {
                firstBreakDone = true;
                Play(breakClip, collision.GetContact(0).point, 1f);
                return;
            }
            PlayHit(collision.GetContact(0).point, Mathf.Clamp01(v / 2.5f));
        }

        public void OnPocket(PoolBall ball, Vector3 pocketCenter)
        {
            if (ball.Pocketed) return;
            ball.Pocket(pocketCenter, feltY);
            Play(pocketClip, pocketCenter, 0.9f);
            if (!ball.isCue)
            {
                Pocketed++;
                SessionLog.Event("pool", "embocada", ball.name);
            }
            else SessionLog.Event("pool", "blanca embocada");
        }

        IEnumerator RespawnCueBall()
        {
            yield return new WaitForSeconds(0.4f);
            Vector3 spot = cueBall.StartPosition;
            for (int i = 0; i < 12 && Occupied(spot, cueBall); i++) spot += Vector3.right * (cueBall.radius * 2.2f) * (i % 2 == 0 ? 1 : -1) * (i + 1);
            cueBall.Respawn(spot);
        }

        bool Occupied(Vector3 p, PoolBall except)
        {
            foreach (var b in balls)
                if (b != null && b != except && !b.Pocketed && (b.transform.position - p).magnitude < b.radius * 2.1f) return true;
            return false;
        }

        /// <summary>Vuelve a armar el triángulo y la blanca.</summary>
        public void ResetTable()
        {
            if (State == Phase.Rolling) return;
            if (State == Phase.Aiming) CancelAiming();
            foreach (var b in balls) if (b != null) b.Respawn(b.StartPosition);
            firstBreakDone = false;
            SessionLog.Event("pool", "rearmar");
        }

        void PlayHit(Vector3 at, float loudness)
        {
            lastHitSound = Time.time;
            Play(hitClip, at, loudness);
        }

        void Play(AudioClip clip, Vector3 at, float loudness)
        {
            if (sound != null && clip != null) sound.PlaySpecific(clip, at, loudness);
        }

        // ---------- Guía visual ----------

        void BuildGuide()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = "Linea guia";
            Destroy(g.GetComponent<Collider>());
            g.AddComponent<NoGazeOutline>();
            guide = g.transform;
            var r = g.GetComponent<Renderer>();
            if (lineMaterial != null) r.sharedMaterial = lineMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            var col = new Color(1f, 1f, 1f, 1f);
            block.SetColor("_BaseColor", col); block.SetColor("_Color", col);
            r.SetPropertyBlock(block);
            g.SetActive(false);

            var mgo = new GameObject("Punto apuntado", typeof(RectTransform));
            var canvas = mgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            marker = (RectTransform)mgo.transform;
            marker.sizeDelta = new Vector2(100f, 100f);
            marker.localScale = Vector3.one * 0.0007f; // 7 cm
            var ring = UiChild(marker, "Anillo", GazeReticle.CircleSprite(0.8f));
            ring.color = new Color(1f, 1f, 1f, 0.6f);
            markerFill = UiChild(marker, "Progreso", GazeReticle.CircleSprite(0.6f));
            markerFill.type = Image.Type.Filled;
            markerFill.fillMethod = Image.FillMethod.Radial360;
            markerFill.color = new Color(1f, 0.8f, 0.35f, 1f);
            markerFill.fillAmount = 0f;
            mgo.SetActive(false);
        }

        static Image UiChild(RectTransform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        void UpdateGuide(Vector3 from)
        {
            // Hasta el primer choque (bola o banda) o hasta el punto apuntado.
            float len = Flat(aimPoint - from).magnitude;
            if (Physics.SphereCast(from, cueBall.radius * 0.95f, aimDir, out RaycastHit hit, len + 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponent<PoolBall>() != cueBall) len = hit.distance;
            len = Mathf.Max(0.02f, len);
            Vector3 a = new Vector3(from.x, feltY + 0.003f, from.z);
            guide.position = a + aimDir * (len * 0.5f);
            guide.rotation = Quaternion.FromToRotation(Vector3.up, aimDir);
            guide.localScale = new Vector3(0.004f, len * 0.5f, 0.004f);
        }

        Vector3 OnFelt(Vector3 p)
        {
            Vector3 c = transform.position;
            p.x = Mathf.Clamp(p.x, c.x - halfField.x, c.x + halfField.x);
            p.z = Mathf.Clamp(p.z, c.z - halfField.y, c.z + halfField.y);
            p.y = feltY;
            return p;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }

    /// <summary>Una bola: cuerpo rígido esférico; avisa los choques y se deja embocar.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class PoolBall : MonoBehaviour
    {
        public bool isCue;
        public float radius = 0.0285f;
        [HideInInspector] public PoolGame game;

        public Rigidbody Body { get; private set; }
        public bool Pocketed { get; private set; }
        public Vector3 StartPosition { get; private set; }

        Vector3 startScale;
        Quaternion startRotation;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            StartPosition = transform.position;
            startRotation = transform.rotation;
            startScale = transform.localScale;
        }

        void OnCollisionEnter(Collision c) { if (game != null) game.OnBallCollision(this, c); }

        public void Stop()
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        public void Pocket(Vector3 pocketCenter, float feltY)
        {
            Pocketed = true;
            StartCoroutine(Fall(pocketCenter, feltY));
        }

        IEnumerator Fall(Vector3 pocketCenter, float feltY)
        {
            Body.isKinematic = true;
            GetComponent<Collider>().enabled = false;
            Vector3 p0 = transform.position;
            Vector3 p1 = new Vector3(pocketCenter.x, feltY - 0.12f, pocketCenter.z);
            yield return Ease.Run(0.3f, t =>
            {
                transform.position = Vector3.Lerp(p0, p1, Ease.InCubic(t));
                transform.localScale = startScale * Mathf.Lerp(1f, 0.7f, t);
            });
            gameObject.SetActive(false);
        }

        public void Respawn(Vector3 position)
        {
            StopAllCoroutines();
            gameObject.SetActive(true);
            Pocketed = false;
            transform.SetPositionAndRotation(position, startRotation);
            transform.localScale = startScale;
            GetComponent<Collider>().enabled = true;
            Body.isKinematic = false;
            Stop();
        }
    }

    /// <summary>Tronera: lo que entra en su zona se cae.</summary>
    [RequireComponent(typeof(SphereCollider))]
    public class PoolPocket : MonoBehaviour
    {
        public PoolGame game;

        void OnTriggerEnter(Collider other)
        {
            var ball = other.GetComponent<PoolBall>();
            if (ball != null && game != null) game.OnPocket(ball, transform.position);
        }
    }

    /// <summary>"Rearmar mesa": texto de tiza sobre la banda que se elige con la mirada.</summary>
    public class PoolResetButton : GazeInteractable
    {
        public PoolGame game;
        TMPro.TextMeshPro text;
        Color baseColor = new Color(0.95f, 0.93f, 0.85f);

        void Awake()
        {
            text = GetComponentInChildren<TMPro.TextMeshPro>();
            if (dwellTime <= 0f) dwellTime = 1.2f;
        }

        protected override bool IsAvailable() => game != null && game.State != PoolGame.Phase.Rolling;
        public override void OnSelect(GazePointer pointer) { if (game != null) game.ResetTable(); }

        protected override void ApplyHighlight(float amount)
        {
            if (text != null) text.color = Color.Lerp(baseColor, highlightColor, amount);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using BarAudio;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BarInteraccion
{
    /// <summary>Pool para Cardboard. La mirada marca la dirección. Mantener apretado carga la fuerza y soltar tira.</summary>
    public class PoolGame : GazeInteractable
    {
        public enum Phase { Idle, Aiming, Power, Rolling }

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
        [Min(0.01f)] public float minSpeed = 0.5f;
        [Min(0.01f)] public float maxSpeed = 3.4f;
        [Tooltip("Segundos manteniendo apretado para llegar a la fuerza máxima.")]
        public float chargeTime = 0.9f;
        [Tooltip("Qué tan rápido el taco sigue a la cabeza. Más alto, más pegado.")]
        public float aimSmoothing = 8f;
        public float maxPlayDistance = 3.2f;

        const float MinAimDistance = 0.06f;
        const float CancelDelay = 3.5f;
        const float AimMargin = 0.28f;
        const float CueReadyGap = 0.04f;
        const float RestSpeed = 0.03f;
        const float RestTime = 0.5f;
        const float MaxRollTime = 15f;

        [Header("Sonido (fuente 'Pool' de la escena)")]
        public BarSound sound;
        public AudioClip breakClip, hitClip, pocketClip;

        [Header("Visual")]
        public Material lineMaterial;

        [Header("Luz de las bolas y el taco")]
        [Tooltip("Las lámparas de la mesa son de área (solo horneadas) y no hay sondas de luz sobre la mesa: las bolas, " +
                 "que se mueven, se iluminan con esta luz de arriba en vez de con las sondas. Se puede ajustar en Play.")]
        public Color lampColor = new Color(1f, 0.94f, 0.82f);
        [Range(0f, 4f)] public float lampIntensity = 1.6f;
        public Color fillColor = new Color(0.30f, 0.27f, 0.24f);

        public Phase State { get; private set; } = Phase.Idle;
        public int Shots { get; private set; }
        public int Pocketed { get; private set; }

        Vector3 aimPoint, lockedDirection;
        float offTable, idleDwell, lastHitSound, powerTime;
        bool hasAim, firstBreakDone, shotInMotion, waitForRelease;
        float rollingTime, stillTime, originalBounceThreshold;
        Transform guide;
        RectTransform marker;
        Image markerFill;
        PoolScoreboard scoreboard;
        PoolPocket[] pockets;
        Vector3[] previousPositions;
        readonly RaycastHit[] guideHits = new RaycastHit[32];
        BarSound[] hitVoices, pocketVoices;
        BarSound breakVoice;

        void Awake()
        {
            SetCueVisible(false);
            if (balls == null) balls = System.Array.Empty<PoolBall>();
            foreach (var ball in balls)
                if (ball != null) ball.game = this;
            pockets = GetComponentsInChildren<PoolPocket>();
            previousPositions = new Vector3[balls.Length];
            BuildGuide();
            scoreboard = new PoolScoreboard(transform,
                new Vector3(transform.position.x, feltY + 0.43f, transform.position.z + halfField.y - 0.03f));
            RefreshScoreboard();
            if (string.IsNullOrEmpty(label)) label = "Jugar al pool";
            if (highlightRoot == null && cueBall != null) highlightRoot = cueBall.transform;
            idleDwell = dwellTime;
        }

        void Start()
        {
            ApplyLighting();
            foreach (var ball in balls)
                if (ball != null) ball.Settle();
            if (sound != null)
            {
                hitVoices = new[] { sound, CreateVoice("Choques de pool") };
                breakVoice = CreateVoice("Apertura de pool");
                pocketVoices = new[] { CreateVoice("Tronera de pool"), CreateVoice("Tronera de pool adicional") };
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopAllCoroutines();
            EndMotion();
            if (State == Phase.Rolling)
            {
                foreach (var ball in balls)
                {
                    if (ball == null) continue;
                    if (ball.Pocketed) ball.FinishPocket();
                    else ball.Settle();
                }
                RestoreWhiteBall();
            }
            SetIdle();
        }

        void OnValidate() { if (Application.isPlaying && balls != null) ApplyLighting(); }

        /// <summary>Luz "de lámpara" para lo que se mueve sobre la mesa (armónicos esféricos propios).</summary>
        void ApplyLighting()
        {
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(fillColor);
            sh.AddDirectionalLight(Vector3.up, lampColor, lampIntensity);
            var coefficients = new[] { sh };
            var block = new MaterialPropertyBlock();
            var roots = new List<Transform>();
            foreach (var b in balls) if (b != null) roots.Add(b.transform);
            if (cue != null) roots.Add(cue);
            foreach (var root in roots)
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.GetComponent<NoGazeOutline>() != null) continue;
                    r.lightProbeUsage = LightProbeUsage.CustomProvided;
                    r.GetPropertyBlock(block);
                    block.CopySHCoefficientArraysFrom(coefficients);
                    r.SetPropertyBlock(block);
                }
        }

        protected override bool IsAvailable()
        {
            var pointer = GazePointer.Instance;
            return State != Phase.Rolling && cueBall != null && pointer != null && IsNearTable(pointer.transform.position);
        }

        bool IsNearTable(Vector3 camera)
        {
            return Flat(camera - ClampToField(camera)).sqrMagnitude <= maxPlayDistance * maxPlayDistance;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            var pointer = GazePointer.Instance;
            if (pointer == null) return;
            Vector3 viewer = pointer.transform.position;
            scoreboard.Face(viewer, IsNearTable(viewer));
        }

        protected override Vector3 LabelPosition => State == Phase.Aiming || State == Phase.Power
            ? aimPoint + Vector3.up * 0.14f
            : (cueBall != null ? cueBall.transform.position : transform.position) + Vector3.up * 0.3f;

        public override void OnSelect(GazePointer pointer)
        {
            if (!IsAvailable()) return;

            if (State == Phase.Idle)
            {
                if (!TryAimPoint(pointer, out Vector3 point) || !TryDirection(point, out Vector3 direction)) return;
                State = Phase.Aiming;
                dwellTime = float.PositiveInfinity;
                offTable = 0f;
                lockedDirection = direction;
                hasAim = true;
                waitForRelease = GazeInput.Held();
                scoreboard.SetPower(-1);
                ShowAim(direction, 0f);
                label = "Mantené apretado para tirar";
                SessionLog.Event("pool", "apuntar");
            }
        }

        void Update()
        {
            if (State == Phase.Aiming) UpdateAim();
            else if (State == Phase.Power) UpdatePower();
        }

        void UpdateAim()
        {
            var pointer = GazePointer.Instance;
            if (TryAimPoint(pointer, out Vector3 point, false) && TryDirection(point, out Vector3 direction))
            {
                offTable = 0f;
                lockedDirection = hasAim
                    ? Vector3.Slerp(lockedDirection, direction, 1f - Mathf.Exp(-aimSmoothing * Time.deltaTime)).normalized
                    : direction;
                hasAim = true;
                label = "Mantené apretado para tirar";
                scoreboard.SetPower(-1);
                ShowAim(lockedDirection, 0f);
                if (waitForRelease)
                {
                    if (!GazeInput.Held()) waitForRelease = false;
                    return;
                }
                if (GazeInput.Held()) BeginCharge();
                return;
            }

            offTable += Time.deltaTime;
            if (!hasAim || offTable >= CancelDelay)
                SetIdle();
        }

        void BeginCharge()
        {
            State = Phase.Power;
            powerTime = 0f;
            SessionLog.Event("pool", "fuerza");
        }

        void UpdatePower()
        {
            if (GazeInput.Held())
            {
                powerTime += Time.deltaTime;
                float power = ChargePower();
                int percent = PowerPercent(power);
                label = $"Fuerza · {percent}%";
                scoreboard.SetPower(percent);
                ShowAim(lockedDirection, power);
                return;
            }

            StartCoroutine(Shoot(lockedDirection, ChargePower()));
        }

        float ChargePower()
        {
            return Mathf.Clamp01(powerTime / Mathf.Max(0.15f, chargeTime));
        }

        void ShowAim(Vector3 direction, float fill)
        {
            Vector3 origin = cueBall.Body.position;
            origin.y = feltY;
            aimPoint = origin + direction * 0.4f;
            PlaceCue(direction, CueReadyGap);
            UpdateGuide(cueBall.Body.position, direction, origin + direction * (halfField.magnitude * 2f));
            marker.position = aimPoint + Vector3.up * 0.004f;
            marker.rotation = Quaternion.LookRotation(Vector3.down, direction);
            markerFill.fillAmount = fill;
            guide.gameObject.SetActive(true);
            marker.gameObject.SetActive(true);
        }

        bool TryAimPoint(GazePointer pointer, out Vector3 point, bool requireGaze = true)
        {
            point = default;
            if (pointer == null || pointer.Locked) return false;
            if (requireGaze && !ReferenceEquals(pointer.Current, this)) return false;
            var ray = new Ray(pointer.transform.position, pointer.transform.forward);
            var plane = new Plane(Vector3.up, Vector3.up * feltY);
            if (!plane.Raycast(ray, out float distance) || distance > pointer.maxDistance) return false;
            point = ray.GetPoint(distance);
            return IsInsideField(point, -AimMargin);
        }

        bool TryDirection(Vector3 point, out Vector3 direction)
        {
            Vector3 offset = Flat(point - cueBall.Body.position);
            direction = offset.normalized;
            return offset.magnitude >= MinAimDistance;
        }

        int PowerPercent(float power) => Mathf.RoundToInt(100f * Mathf.Lerp(Mathf.Min(minSpeed, maxSpeed), maxSpeed, power) / maxSpeed);

        void ClearAim()
        {
            hasAim = false;
            waitForRelease = false;
            powerTime = 0f;
            scoreboard?.SetPower(-1);
            if (guide != null) guide.gameObject.SetActive(false);
            if (marker != null) marker.gameObject.SetActive(false);
        }

        void SetIdle()
        {
            State = Phase.Idle;
            dwellTime = idleDwell;
            label = "Jugar al pool";
            ClearAim();
            SetCueVisible(false);
        }

        IEnumerator Shoot(Vector3 direction, float power)
        {
            State = Phase.Rolling;
            ClearAim();
            label = "";

            float back = Mathf.Lerp(0.08f, 0.22f, power);
            yield return Ease.Run(0.3f, t => PlaceCue(direction, Mathf.Lerp(CueReadyGap, back, Ease.OutCubic(t))));
            yield return Ease.Run(0.06f, t => PlaceCue(direction, Mathf.Lerp(back, 0f, Ease.InCubic(t))));

            for (int i = 0; i < balls.Length; i++)
                if (balls[i] != null) previousPositions[i] = balls[i].Body.position;
            stillTime = 0f;
            rollingTime = 0f;
            lastHitSound = float.NegativeInfinity;
            // PhysX aplica este umbral a toda la escena. Se restaura al terminar el rodaje.
            originalBounceThreshold = Physics.bounceThreshold;
            Physics.bounceThreshold = Mathf.Min(originalBounceThreshold, 0.15f);
            shotInMotion = true;

            cueBall.Body.WakeUp();
            cueBall.Body.linearVelocity = direction * Mathf.Lerp(Mathf.Min(minSpeed, maxSpeed), maxSpeed, power);
            Shots++;
            PlayHit(cueBall.Body.position, Mathf.Lerp(0.3f, 0.8f, power));
            SessionLog.Event("pool", "tiro", $"{PowerPercent(power)}%");
            SetCueVisible(false);
        }

        void PlaceCue(Vector3 direction, float gap)
        {
            if (cue == null) return;
            Vector3 dir = (direction + Vector3.down * 0.08f).normalized;
            Vector3 tip = cueBall.transform.position - dir * (cueBall.CollisionRadius + gap);
            Quaternion rotation = Quaternion.FromToRotation((cueTipLocal - cueBackLocal).normalized, dir);
            cue.SetPositionAndRotation(tip - rotation * cueTipLocal, rotation);
            SetCueVisible(true);
        }

        void SetCueVisible(bool visible)
        {
            if (cue != null && cue.gameObject.activeSelf != visible)
                cue.gameObject.SetActive(visible);
        }

        void FixedUpdate()
        {
            if (!shotInMotion) return;
            bool moving = false;
            for (int i = 0; i < balls.Length; i++)
            {
                var ball = balls[i];
                if (ball == null || ball.Pocketed) continue;

                Vector3 from = previousPositions[i];
                Vector3 to = ball.Body.position;
                if (TryPocket(ball, from, to))
                {
                    moving = true;
                    continue;
                }

                if (to.y < feltY - ball.CollisionRadius || !IsInsideField(to, -0.2f))
                {
                    ReturnBall(ball, from);
                    to = ball.Body.position;
                }

                previousPositions[i] = to;
                moving |= ball.Body.linearVelocity.sqrMagnitude > RestSpeed * RestSpeed
                    || ball.Body.angularVelocity.sqrMagnitude * ball.CollisionRadius * ball.CollisionRadius > RestSpeed * RestSpeed;
            }

            rollingTime += Time.fixedDeltaTime;
            stillTime = moving ? 0f : stillTime + Time.fixedDeltaTime;
            if (stillTime < RestTime && rollingTime < MaxRollTime) return;

            EndMotion();
            foreach (var ball in balls)
                if (ball != null && !ball.Pocketed) ball.Settle();
            RestoreWhiteBall();
            SetIdle();
        }

        void EndMotion()
        {
            if (!shotInMotion) return;
            Physics.bounceThreshold = originalBounceThreshold;
            shotInMotion = false;
        }

        bool TryPocket(PoolBall ball, Vector3 from, Vector3 to)
        {
            foreach (var pocket in pockets)
            {
                if (pocket == null || !pocket.isActiveAndEnabled
                    || !pocket.TryGetEntry(from, to, ball.CollisionRadius, out float entry))
                    continue;
                Vector3 center = pocket.transform.position;
                ball.Pocket(Vector3.Lerp(from, to, entry), center, feltY);
                PlayAvailable(pocketVoices, pocketClip, center, 0.9f);
                if (ball.IsCue) SessionLog.Event("pool", "blanca embocada");
                else
                {
                    Pocketed++;
                    RefreshScoreboard();
                    SessionLog.Event("pool", "embocada", ball.name);
                }
                return true;
            }
            return false;
        }

        void RefreshScoreboard()
        {
            int solids = 0, stripes = 0, eight = 0;
            foreach (var ball in balls)
            {
                if (ball == null || ball.IsCue || !ball.Pocketed) continue;
                if (ball.number < 8) solids++;
                else if (ball.number > 8) stripes++;
                else eight++;
            }
            scoreboard.SetScore(solids, stripes, eight);
        }

        void RestoreWhiteBall()
        {
            if (cueBall != null && cueBall.Pocketed) ReturnBall(cueBall, cueBall.StartPosition);
        }

        void ReturnBall(PoolBall ball, Vector3 preferred)
        {
            preferred.y = ball.StartPosition.y;
            if (IsFreeSpot(preferred, ball))
            {
                ball.Respawn(preferred);
                return;
            }

            float padding = ball.CollisionRadius + 0.003f;
            float step = padding * 2f;
            Vector3 center = transform.position;
            Vector3 best = ball.StartPosition;
            float distance = float.PositiveInfinity;
            for (float x = -halfField.x + padding; x <= halfField.x - padding; x += step)
            {
                for (float z = -halfField.y + padding; z <= halfField.y - padding; z += step)
                {
                    var candidate = new Vector3(center.x + x, preferred.y, center.z + z);
                    float sqrDistance = (candidate - preferred).sqrMagnitude;
                    if (sqrDistance >= distance || !IsFreeSpot(candidate, ball)) continue;
                    best = candidate;
                    distance = sqrDistance;
                }
            }
            ball.Respawn(best);
        }

        bool IsFreeSpot(Vector3 point, PoolBall ball)
        {
            if (!IsInsideField(point, ball.CollisionRadius + 0.002f)) return false;
            foreach (var pocket in pockets)
                if (pocket != null && pocket.isActiveAndEnabled
                    && pocket.TryGetEntry(point, point, ball.CollisionRadius, out _)) return false;

            foreach (var other in balls)
            {
                if (other == null || other == ball || other.Pocketed) continue;
                float separation = ball.CollisionRadius + other.CollisionRadius + 0.002f;
                if (Flat(point - other.Body.position).sqrMagnitude < separation * separation) return false;
            }
            return true;
        }

        public void ResetTable()
        {
            if (State == Phase.Rolling) return;
            if (State == Phase.Aiming || State == Phase.Power) SetIdle();
            foreach (var ball in balls)
                if (ball != null) ball.Respawn(ball.StartPosition);
            RefreshScoreboard();
            firstBreakDone = false;
            SessionLog.Event("pool", "rearmar");
        }

        public void OnBallCollision(Collision collision)
        {
            if (!shotInMotion || collision.contactCount == 0 || !collision.collider.TryGetComponent<PoolBall>(out _)) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 0.12f || Time.time - lastHitSound < 0.04f) return;

            Vector3 contact = collision.GetContact(0).point;
            if (!firstBreakDone && speed > 1.4f && breakVoice != null && breakClip != null)
            {
                firstBreakDone = true;
                lastHitSound = Time.time;
                breakVoice.PlaySpecific(breakClip, contact);
                return;
            }
            PlayHit(contact, Mathf.Clamp01(speed / 2.5f));
        }

        BarSound CreateVoice(string objectName)
        {
            var voice = Instantiate(sound, transform);
            voice.name = objectName;
            voice.automatic = false;
            return voice;
        }

        void PlayHit(Vector3 at, float loudness)
        {
            if (PlayAvailable(hitVoices, hitClip, at, loudness)) lastHitSound = Time.time;
        }

        static bool PlayAvailable(BarSound[] voices, AudioClip clip, Vector3 at, float loudness)
        {
            if (voices == null || clip == null) return false;
            foreach (var voice in voices)
            {
                if (voice.IsPlaying) continue;
                voice.PlaySpecific(clip, at, loudness);
                return true;
            }
            return false;
        }

        // ---------- Guía visual ----------

        void BuildGuide()
        {
            var g = RuntimeMaterials.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = "Linea guia";
            g.transform.SetParent(transform, false);
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
            marker.SetParent(transform, false);
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

        void UpdateGuide(Vector3 from, Vector3 direction, Vector3 point)
        {
            float len = float.PositiveInfinity;
            float radius = Mathf.Max(0.001f, cueBall.CollisionRadius - 0.001f);
            float reach = halfField.magnitude * 2f;
            int count = Physics.SphereCastNonAlloc(from, radius, direction, guideHits, reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = guideHits[i];
                if (hit.distance <= 0f || hit.collider == cueBall.Shape || !hit.transform.IsChildOf(transform)) continue;
                len = Mathf.Min(len, hit.distance);
            }

            Vector3 end = from + direction * reach;
            foreach (var pocket in pockets)
                if (pocket != null && pocket.isActiveAndEnabled
                    && pocket.TryGetEntry(from, end, cueBall.CollisionRadius, out float entry))
                    len = Mathf.Min(len, entry * reach);

            if (float.IsPositiveInfinity(len)) len = Flat(point - from).magnitude;
            Vector3 start = new Vector3(from.x, feltY + 0.003f, from.z);
            guide.position = start + direction * (len * 0.5f);
            guide.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            guide.localScale = new Vector3(0.004f, len * 0.5f, 0.004f);
        }

        bool IsInsideField(Vector3 point, float padding = 0f)
        {
            Vector3 offset = point - transform.position;
            return Mathf.Abs(offset.x) <= halfField.x - padding && Mathf.Abs(offset.z) <= halfField.y - padding;
        }

        Vector3 ClampToField(Vector3 point)
        {
            Vector3 center = transform.position;
            point.x = Mathf.Clamp(point.x, center.x - halfField.x, center.x + halfField.x);
            point.z = Mathf.Clamp(point.z, center.z - halfField.y, center.z + halfField.y);
            return point;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}

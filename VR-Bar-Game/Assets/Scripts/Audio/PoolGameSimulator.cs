using System.Collections;
using UnityEngine;

namespace BarAudio
{
    /// <summary>
    /// Mesa de pool: simula partidas "de fondo" con grabaciones reales (apertura, choques, bola a la tronera)
    /// y alguna reacción de los jugadores.
    /// Cuando el pool sea jugable, apagar simulateGames y llamar a OnBallHit() desde las colisiones de las bolas.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PoolGameSimulator : MonoBehaviour
    {
        public AudioClip breakClip;
        public AudioClip[] hitClips;
        public AudioClip[] pocketClips;

        [Tooltip("Reacciones de los jugadores (risas / festejo). Opcional.")]
        public RandomOneShots reactions;

        public bool simulateGames = true;
        public Vector2 pauseBetweenGames = new Vector2(20f, 60f);
        public Vector2Int shotsPerGame = new Vector2Int(5, 10);
        public Vector2 timeBetweenShots = new Vector2(5f, 12f);
        [Range(0f, 1f)] public float pocketChance = 0.35f;
        [Range(0f, 1f)] public float reactionChance = 0.35f;

        [Tooltip("Mitad del ancho (X) y del largo (Z) de la mesa, en metros. Los golpes suenan en puntos al azar dentro.")]
        public Vector2 tableHalfSize = new Vector2(0.6f, 1.1f);

        AudioSource[] voices;
        int nextVoice;
        Vector3 center;

        void Awake()
        {
            voices = GetComponents<AudioSource>();
            foreach (var v in voices) { v.playOnAwake = false; v.loop = false; }
            center = transform.position;
        }

        void OnEnable()
        {
            if (simulateGames) StartCoroutine(GamesLoop());
        }

        IEnumerator GamesLoop()
        {
            yield return new WaitForSeconds(Random.Range(4f, 12f));
            while (true)
            {
                yield return PlayGame();
                yield return new WaitForSeconds(Random.Range(pauseBetweenGames.x, pauseBetweenGames.y));
            }
        }

        IEnumerator PlayGame()
        {
            // Apertura desde una de las cabeceras de la mesa.
            float side = Random.value < 0.5f ? -0.8f : 0.8f;
            if (tableHalfSize.x > tableHalfSize.y) MoveTo(tableHalfSize.x * side, 0f);
            else MoveTo(0f, tableHalfSize.y * side);
            Play(breakClip, 1f, Random.Range(0.97f, 1.03f));
            yield return new WaitForSeconds(Random.Range(timeBetweenShots.x, timeBetweenShots.y));

            int shots = Random.Range(shotsPerGame.x, shotsPerGame.y + 1);
            for (int i = 0; i < shots; i++)
            {
                MoveTo(Random.Range(-tableHalfSize.x, tableHalfSize.x) * 0.8f,
                       Random.Range(-tableHalfSize.y, tableHalfSize.y) * 0.8f);

                if (pocketClips != null && pocketClips.Length > 0 && Random.value < pocketChance)
                {
                    Play(Pick(pocketClips), Random.Range(0.75f, 1f), Random.Range(0.96f, 1.04f));
                    if (reactions != null && Random.value < reactionChance)
                    {
                        yield return new WaitForSeconds(Random.Range(1.2f, 2.2f));
                        reactions.PlayNow();
                    }
                }
                else
                {
                    Play(Pick(hitClips), Random.Range(0.5f, 1f), Random.Range(0.9f, 1.1f));
                    int extra = Random.Range(0, 3); // rebotes / bolas que se tocan después
                    for (int k = 0; k < extra; k++)
                    {
                        yield return new WaitForSeconds(Random.Range(0.15f, 0.6f));
                        Play(Pick(hitClips), Random.Range(0.2f, 0.5f), Random.Range(0.85f, 1.15f));
                    }
                }
                yield return new WaitForSeconds(Random.Range(timeBetweenShots.x, timeBetweenShots.y));
            }
        }

        /// <summary>Para el pool jugable: llamar desde OnCollisionEnter de las bolas.</summary>
        public void OnBallHit(Vector3 worldPoint, float relativeSpeed)
        {
            float k = Mathf.InverseLerp(0.1f, 3f, relativeSpeed);
            if (k <= 0f) return;
            transform.position = worldPoint;
            Play(Pick(hitClips), k * k, Random.Range(0.92f, 1.08f));
        }

        /// <summary>Para el pool jugable: llamar cuando una bola entra en una tronera.</summary>
        public void OnBallPocketed(Vector3 worldPoint)
        {
            transform.position = worldPoint;
            Play(Pick(pocketClips), 1f, Random.Range(0.96f, 1.04f));
        }

        void MoveTo(float x, float z)
        {
            transform.position = center + new Vector3(x, 0f, z);
        }

        void Play(AudioClip clip, float vol, float p)
        {
            if (clip == null || voices == null || voices.Length == 0) return;
            AudioSource v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            v.pitch = p;
            v.PlayOneShot(clip, vol);
        }

        static AudioClip Pick(AudioClip[] a)
        {
            return (a == null || a.Length == 0) ? null : a[Random.Range(0, a.Length)];
        }

        void OnDrawGizmosSelected()
        {
            Vector3 c = Application.isPlaying ? center : transform.position;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Gizmos.DrawWireCube(c, new Vector3(tableHalfSize.x * 2f, 0.05f, tableHalfSize.y * 2f));
        }
    }
}

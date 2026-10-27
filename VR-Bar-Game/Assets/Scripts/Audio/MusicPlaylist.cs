using UnityEngine;

namespace BarAudio
{
    /// <summary>
    /// Música DIEGÉTICA: sale de los parlantes del bar (fuentes 3D), sincronizados con PlayScheduled
    /// para que todos los parlantes arranquen en el mismo sample.
    /// Entre canción y canción deja un silencio corto: ahí aparecen los sonidos chicos del lugar
    /// (neón, heladera, lluvia), que con música no se notan.
    /// </summary>
    public class MusicPlaylist : MonoBehaviour
    {
        public AudioSource[] speakers;
        public AudioClip[] songs;

        [Tooltip("Silencio entre canciones (mín, máx) en segundos.")]
        public Vector2 silenceBetweenSongs = new Vector2(6f, 12f);

        public bool shuffle = true;
        public float firstSongDelay = 2f;

        [Header("Solo lectura")]
        public string nowPlaying;

        int current = -1;
        double currentEndDsp = -1;

        void Start()
        {
            if (songs == null || songs.Length == 0 || speakers == null || speakers.Length == 0)
            {
                enabled = false;
                return;
            }
            foreach (var s in speakers) { s.playOnAwake = false; s.loop = false; }
            ScheduleNext(AudioSettings.dspTime + firstSongDelay);
        }

        void Update()
        {
            if (currentEndDsp > 0 && AudioSettings.dspTime >= currentEndDsp)
                ScheduleNext(AudioSettings.dspTime + Random.Range(silenceBetweenSongs.x, silenceBetweenSongs.y));
        }

        void ScheduleNext(double startDsp)
        {
            current = NextIndex();
            AudioClip clip = songs[current];
            foreach (var s in speakers)
            {
                if (s == null) continue;
                s.clip = clip;
                s.PlayScheduled(startDsp);
            }
            currentEndDsp = startDsp + (double)clip.samples / clip.frequency;
            nowPlaying = clip.name;
        }

        int NextIndex()
        {
            if (songs.Length == 1) return 0;
            if (!shuffle) return (current + 1) % songs.Length;
            int i;
            do { i = Random.Range(0, songs.Length); } while (i == current);
            return i;
        }
    }
}

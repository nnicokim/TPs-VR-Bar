using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BarInteraccion
{
    /// <summary>
    /// Registro de cada sesión, para la prueba con usuarios: qué hizo el participante y cuándo.
    /// Escribe un CSV (separado por ";") en Application.persistentDataPath/sesiones/ :
    ///   - Windows (Editor): %USERPROFILE%/AppData/LocalLow/&lt;empresa&gt;/&lt;producto&gt;/sesiones
    ///   - Android: Android/data/&lt;paquete&gt;/files/sesiones (se ve conectando el celular por USB)
    ///   - iPhone: carpeta Documents de la app (Finder → el iPhone → Archivos, si está habilitado el acceso)
    /// Columnas: tiempo_s;categoria;evento;detalle. Se guarda cada 20 eventos, al pausar/salir y al final.
    /// </summary>
    public static class SessionLog
    {
        static readonly List<string> lines = new List<string>();
        static float start;
        static string path;
        static bool started;
        static int dwellSelections, tapSelections;

        public static string FilePath => path;
        public static int DwellSelections => dwellSelections;
        public static int TapSelections => tapSelections;
        public static float Elapsed => started ? Time.time - start : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            lines.Clear();
            started = false;
            path = null;
            dwellSelections = tapSelections = 0;
        }

        public static void Event(string category, string what, string detail = "")
        {
            EnsureStarted();
            lines.Add($"{Time.time - start:0.00};{Clean(category)};{Clean(what)};{Clean(detail)}");
            if (lines.Count % 20 == 0) Flush();
        }

        /// <summary>Una selección hecha con la mirada sostenida (dwell) o con el toque.</summary>
        public static void Selection(string target, bool byDwell)
        {
            if (byDwell) dwellSelections++; else tapSelections++;
            Event("seleccion", target, byDwell ? "mirada" : "toque");
        }

        public static void Flush()
        {
            if (!started || string.IsNullOrEmpty(path)) return;
            try { File.WriteAllLines(path, lines); }
            catch (Exception e) { Debug.LogWarning($"SessionLog: no pude guardar {path}: {e.Message}"); }
        }

        static void EnsureStarted()
        {
            if (started) return;
            started = true;
            start = Time.time;
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "sesiones");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, $"sesion_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            catch (Exception e) { Debug.LogWarning($"SessionLog: {e.Message}"); }
            lines.Add("tiempo_s;categoria;evento;detalle");
            lines.Add($"0.00;sesion;inicio;{DateTime.Now:yyyy-MM-dd HH:mm:ss} · {SystemInfo.deviceModel} · {Application.platform}");
            var keeper = new GameObject("Registro de sesion");
            keeper.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(keeper);
            keeper.AddComponent<SessionLogKeeper>();
            Debug.Log($"Registro de la sesión: {path}");
        }

        static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(";", ",").Replace("\n", " ");
    }

    /// <summary>Guarda el registro cuando la app se pausa (Android/iPhone) o se cierra.</summary>
    public class SessionLogKeeper : MonoBehaviour
    {
        void OnApplicationPause(bool paused) { if (paused) SessionLog.Flush(); }
        void OnApplicationQuit() { SessionLog.Event("sesion", "fin de la app"); SessionLog.Flush(); }
    }
}

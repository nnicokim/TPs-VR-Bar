using UnityEditor;
using UnityEngine;

namespace BarAudio.EditorTools
{
    /// <summary>
    /// Reglas de importación de los sonidos de Assets/Sounds (optimización para celular / VR):
    ///  - Musica:   Streaming + Vorbis. No ocupa RAM: se decodifica de a poco mientras suena.
    ///  - Ambiente: Compressed In Memory + Vorbis, mono (salvo la cama estéreo A1). Loops largos, poca RAM.
    ///  - Foley y Voces: Decompress On Load + ADPCM, mono. Clips cortos: latencia mínima y casi nada de CPU.
    ///  - Todo a 44,1 kHz (algunos originales venían a 96 kHz: el doble de memoria sin beneficio audible).
    ///  - Mono en todo lo que se espacializa: una fuente 3D se "colapsa" a un punto igual, así que el estéreo solo gasta.
    /// </summary>
    public class BarAudioImporter : AssetPostprocessor
    {
        const string Root = "Assets/Sounds/";

        public override uint GetVersion() { return 1; }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (AudioImporter)assetImporter;
            var s = importer.defaultSampleSettings;
            s.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            s.sampleRateOverride = 44100;
            string file = System.IO.Path.GetFileName(assetPath);

            if (assetPath.StartsWith(Root + "Musica/"))
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                importer.forceToMono = false;
                importer.loadInBackground = true;
            }
            else if (assetPath.StartsWith(Root + "Ambiente/"))
            {
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.5f;
                importer.forceToMono = !file.StartsWith("A1_");
                importer.loadInBackground = true;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
                importer.forceToMono = true;
                importer.loadInBackground = false;
            }
            importer.defaultSampleSettings = s;
        }
    }
}

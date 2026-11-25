using System.Collections;
using BarAudio;
using UnityEngine;
using UnityEngine.Rendering;

namespace BarInteraccion
{
    /// <summary>
    /// El parlante del bar (de donde sale la música). La caja se arma sola: gabinete, cono y un LED verde
    /// que dice "prendido". Apagarlo corta la música con un fundido y apaga el LED.
    /// El objeto mira (eje Z azul) hacia el salón.
    /// </summary>
    public class SpeakerBox : ClosingTask
    {
        public BarSound music;
        public Material cabinetMaterial;
        public Vector3 size = new Vector3(0.3f, 0.44f, 0.26f);

        Renderer led;

        void Awake()
        {
            Build();
            if (string.IsNullOrEmpty(label)) label = "Apagar la música";
        }

        protected override IEnumerator Perform()
        {
            if (music != null) music.Silence(2.5f);
            yield return new WaitForSeconds(0.2f);
            SetLed(false);
            yield return new WaitForSeconds(2.3f);
        }

        void Build()
        {
            var body = Part(PrimitiveType.Cube, "Gabinete", Vector3.zero, size, new Color(0.08f, 0.08f, 0.09f), true);
            if (cabinetMaterial != null) body.GetComponent<Renderer>().sharedMaterial = cabinetMaterial;
            var col = body.GetComponent<BoxCollider>();
            if (col != null) { col.isTrigger = true; col.size = Vector3.one * 1.3f; }

            float front = size.z * 0.5f + 0.002f; // el frente es +Z local (hacia el salón)
            var woofer = Part(PrimitiveType.Cylinder, "Cono", new Vector3(0f, -0.06f, front), new Vector3(size.x * 0.72f, 0.004f, size.x * 0.72f), new Color(0.16f, 0.16f, 0.17f), false);
            woofer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tweeter = Part(PrimitiveType.Cylinder, "Tweeter", new Vector3(0f, 0.14f, front), new Vector3(0.07f, 0.004f, 0.07f), new Color(0.2f, 0.2f, 0.21f), false);
            tweeter.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ledGo = Part(PrimitiveType.Sphere, "LED", new Vector3(size.x * 0.36f, -size.y * 0.42f, front), Vector3.one * 0.012f, new Color(0.2f, 1f, 0.35f), false);
            led = ledGo.GetComponent<Renderer>();
            SetLed(true);
        }

        GameObject Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, bool keepCollider)
        {
            var go = RuntimeMaterials.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
            return go;
        }

        void SetLed(bool on)
        {
            if (led == null) return;
            var block = new MaterialPropertyBlock();
            Color c = on ? new Color(0.2f, 1f, 0.35f) : new Color(0.05f, 0.08f, 0.05f);
            block.SetColor("_BaseColor", c); block.SetColor("_Color", c);
            block.SetColor("_EmissionColor", on ? c * 2f : Color.black);
            led.SetPropertyBlock(block);
        }
    }
}

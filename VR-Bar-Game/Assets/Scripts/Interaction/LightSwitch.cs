using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BarInteraccion
{
    /// <summary>
    /// Una llave del tablero de luces (junto a la puerta de la cocina). Apaga una zona del bar:
    /// las bombillas pierden su emisión, las luces se desactivan y la vista se oscurece un escalón
    /// (las luces están horneadas, ver DarknessOverlay). La llave se arma sola (placa, palanca y rótulo).
    /// </summary>
    public class LightSwitch : ClosingTask
    {
        [Tooltip("Rótulo debajo de la llave (SALÓN, JUEGOS, BARRA).")]
        public string zoneName = "SALÓN";
        public Renderer[] bulbs;
        public Light[] lights;
        public Material plateMaterial;

        Transform lever;

        void Awake()
        {
            BuildSwitch();
            if (string.IsNullOrEmpty(label)) label = $"Apagar {zoneName.ToLowerInvariant()}";
        }

        protected override IEnumerator Perform()
        {
            // La palanca baja con un "clac" (rápido al final).
            yield return Ease.Run(0.18f, t => lever.localRotation = Quaternion.Euler(Mathf.Lerp(-25f, 25f, Ease.InCubic(t)), 0f, 0f));
            // Las bombillas tardan un instante y titilan al apagarse.
            yield return new WaitForSeconds(0.12f);
            Emission.Set(bulbs, false);
            yield return new WaitForSeconds(0.06f);
            Emission.Set(bulbs, true);
            yield return new WaitForSeconds(0.05f);
            Emission.Set(bulbs, false);
            if (lights != null) foreach (var l in lights) if (l != null) l.enabled = false;
        }

        void BuildSwitch()
        {
            // Placa
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Placa";
            plate.transform.SetParent(transform, false);
            plate.transform.localScale = new Vector3(0.07f, 0.11f, 0.012f);
            var pr = plate.GetComponent<Renderer>();
            if (plateMaterial != null) pr.sharedMaterial = plateMaterial;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            var pc = plate.GetComponent<BoxCollider>();
            if (pc != null) { pc.isTrigger = true; pc.size = new Vector3(1.4f, 1.3f, 3f); }

            // Palanca (pivote en el centro de la placa): arriba = prendida.
            var pivot = new GameObject("Palanca").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(0f, 0f, -0.008f);
            pivot.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            var stick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stick.name = "Tecla";
            Object.Destroy(stick.GetComponent<Collider>());
            stick.transform.SetParent(pivot, false);
            stick.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            stick.transform.localScale = new Vector3(0.026f, 0.05f, 0.016f);
            var sr = stick.GetComponent<Renderer>();
            sr.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            var ivory = new Color(0.92f, 0.88f, 0.78f);
            block.SetColor("_BaseColor", ivory); block.SetColor("_Color", ivory);
            sr.SetPropertyBlock(block);
            lever = pivot;

            // Rótulo
            var textGo = new GameObject("Rotulo");
            textGo.AddComponent<NoGazeOutline>();
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = new Vector3(0f, -0.085f, -0.004f);
            var t = textGo.AddComponent<TextMeshPro>();
            t.text = zoneName;
            t.fontSize = 0.16f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.95f, 0.93f, 0.85f);
            t.rectTransform.sizeDelta = new Vector2(0.12f, 0.03f);
        }
    }
}

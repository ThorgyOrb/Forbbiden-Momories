using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector de <see cref="CardData"/> que muestra el arte de la carta aunque el campo
/// <c>artwork</c> esté vacío.
///
/// Las ~14.600 cartas importadas no tienen un Sprite asignado a propósito: su imagen vive
/// en StreamingAssets y se resuelve en tiempo de ejecución por <see cref="CardData.Artwork"/>.
/// Sin esto, en el Inspector parecía que les faltaba el arte. Aquí se pinta exactamente el
/// mismo sprite que usará el juego, así que si esta caja se ve, la carta se verá.
/// </summary>
[CustomEditor(typeof(CardData))]
[CanEditMultipleObjects]
public class CardDataEditor : Editor
{
    const float PreviewHeight = 190f;

    public override void OnInspectorGUI()
    {
        var card = (CardData)target;

        if (targets.Length == 1) DrawArtPreview(card);

        DrawDefaultInspector();
    }

    private void DrawArtPreview(CardData card)
    {
        var sprite = card.Artwork;

        EditorGUILayout.LabelField("Arte", EditorStyles.boldLabel);

        if (sprite != null && sprite.texture != null)
        {
            var rect = GUILayoutUtility.GetRect(0, PreviewHeight, GUILayout.ExpandWidth(true));
            float aspect = (float)sprite.texture.width / sprite.texture.height;
            float w = Mathf.Min(rect.width, PreviewHeight * aspect);
            var box = new Rect(rect.x + (rect.width - w) * 0.5f, rect.y, w, PreviewHeight);
            GUI.DrawTexture(box, sprite.texture, ScaleMode.ScaleToFit);

            string origin = card.artwork != null
                ? "Sprite asignado en 'artwork'"
                : $"StreamingAssets/{card.artFile}";
            EditorGUILayout.LabelField(origin, EditorStyles.miniLabel);
        }
        else if (!string.IsNullOrEmpty(card.artFile))
        {
            EditorGUILayout.HelpBox(
                $"No se pudo cargar el archivo:\nStreamingAssets/{card.artFile}",
                MessageType.Warning);
        }
        else if (card.artwork == null)
        {
            EditorGUILayout.HelpBox(
                "Esta carta no tiene arte: ni Sprite en 'artwork' ni ruta en 'artFile'.",
                MessageType.Info);
        }

        if (card.artwork == null && !string.IsNullOrEmpty(card.artFile))
            EditorGUILayout.HelpBox(
                "El campo 'artwork' está vacío A PROPÓSITO. El arte se carga bajo demanda " +
                "desde StreamingAssets; asignar los 14.000 sprites como assets metería " +
                "gigas de textura en memoria al abrir la biblioteca.",
                MessageType.None);

        EditorGUILayout.Space();
    }
}

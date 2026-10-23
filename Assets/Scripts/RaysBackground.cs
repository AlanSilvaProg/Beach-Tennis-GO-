using UnityEngine;
using UnityEngine.UI;

// Feeds the sunburst shader (BeachTennisGO/RaysBackground) with the shape of the screen, so the rays stay round on any aspect ratio.
// Put it on a RawImage whose material uses that shader and stretch the RawImage over the whole canvas.
[RequireComponent(typeof(RawImage))]
[ExecuteAlways]
public class RaysBackground : MonoBehaviour
{
    static readonly int Aspect = Shader.PropertyToID("_Aspect");

    RawImage image;

    void OnEnable() { image = GetComponent<RawImage>(); Apply(); }
    void OnRectTransformDimensionsChange() { Apply(); }

    void Apply()
    {
        if (image == null) image = GetComponent<RawImage>();
        var rect = ((RectTransform)transform).rect;
        if (image != null && image.material != null && rect.height > 0f) image.material.SetFloat(Aspect, rect.width / rect.height);
    }
}

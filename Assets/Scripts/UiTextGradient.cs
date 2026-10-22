using UnityEngine;
using UnityEngine.UI;

// Vertical colour gradient for a UI Text (top colour -> bottom colour), like the lettering of the game mode buttons.
[RequireComponent(typeof(Graphic))]
public class UiTextGradient : BaseMeshEffect
{
    public Color top = Color.white;
    public Color bottom = new Color(0.75f, 0.88f, 1f);

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        float minY = float.MaxValue, maxY = float.MinValue;
        var v = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            minY = Mathf.Min(minY, v.position.y);
            maxY = Mathf.Max(maxY, v.position.y);
        }
        float height = Mathf.Max(0.0001f, maxY - minY);
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            Color c = Color.Lerp(bottom, top, (v.position.y - minY) / height);
            v.color = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(c.a * v.color.a));
            vh.SetUIVertex(v, i);
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (graphic != null) graphic.SetVerticesDirty();
    }
#endif
}

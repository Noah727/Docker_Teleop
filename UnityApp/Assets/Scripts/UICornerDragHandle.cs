using UnityEngine;
using UnityEngine.UI;

public static class UICornerDragHandle
{
    public enum Corner
    {
        BottomLeft,
        BottomRight,
        TopLeft,
        TopRight
    }

    private static readonly Sprite[] curvedNotchSprites = new Sprite[4];

    public static RectTransform Ensure(
        Transform parent,
        string handleName,
        Color notchColor,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        Corner corner = Corner.BottomRight;
        if (anchor.x < 0.5f && anchor.y < 0.5f)
            corner = Corner.BottomLeft;
        else if (anchor.x < 0.5f && anchor.y > 0.5f)
            corner = Corner.TopLeft;
        else if (anchor.x > 0.5f && anchor.y > 0.5f)
            corner = Corner.TopRight;

        return Ensure(parent, handleName, notchColor, anchor, pivot, anchoredPosition, size, corner);
    }

    public static RectTransform Ensure(
        Transform parent,
        string handleName,
        Color notchColor,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        Corner corner)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find(handleName);
        GameObject handleObject;
        if (existing == null)
        {
            handleObject = new GameObject(handleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleObject.transform.SetParent(parent, false);
        }
        else
        {
            handleObject = existing.gameObject;
            if (handleObject.GetComponent<CanvasRenderer>() == null)
                handleObject.AddComponent<CanvasRenderer>();
            if (handleObject.GetComponent<Image>() == null)
                handleObject.AddComponent<Image>();
        }

        handleObject.layer = parent.gameObject.layer;

        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = anchor;
        handleRect.anchorMax = anchor;
        handleRect.pivot = pivot;
        handleRect.anchoredPosition = anchoredPosition;
        handleRect.sizeDelta = size;
        handleRect.localRotation = Quaternion.identity;
        handleRect.localScale = Vector3.one;
        handleRect.SetAsLastSibling();

        Image hitArea = handleObject.GetComponent<Image>();
        hitArea.color = new Color(notchColor.r, notchColor.g, notchColor.b, 0.0f);
        hitArea.raycastTarget = true;

        EnsureNotchLines(handleRect, notchColor, corner);
        SetVisible(handleRect, false, notchColor);
        return handleRect;
    }

    public static void SetVisible(RectTransform handleRect, bool visible, Color notchColor)
    {
        if (handleRect == null)
            return;

        float alpha = visible ? Mathf.Clamp01(Mathf.Max(0.32f, notchColor.a)) : 0f;
        foreach (Image image in handleRect.GetComponentsInChildren<Image>(includeInactive: true))
        {
            if (image == null || image.transform == handleRect)
                continue;
            image.color = new Color(notchColor.r, notchColor.g, notchColor.b, alpha);
        }
    }

    private static void EnsureNotchLines(RectTransform handleRect, Color notchColor, Corner corner)
    {
        DisableLegacyLine(handleRect, "NotchLine_1");
        DisableLegacyLine(handleRect, "NotchLine_2");
        DisableLegacyLine(handleRect, "NotchLine_3");
        DisableLegacyLine(handleRect, "NotchCorner");
        DisableLegacyLine(handleRect, "NotchLine_H");
        DisableLegacyLine(handleRect, "NotchLine_V");

        Color lineColor = new Color(notchColor.r, notchColor.g, notchColor.b, Mathf.Clamp01(Mathf.Max(0.35f, notchColor.a)));
        EnsureCurvedNotch(handleRect, corner, lineColor);
    }

    private static void EnsureCurvedNotch(RectTransform parent, Corner corner, Color color)
    {
        const float inset = 1f;
        Transform existing = parent.Find("NotchCurve");
        GameObject curveObject;
        if (existing == null)
        {
            curveObject = new GameObject("NotchCurve", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            curveObject.transform.SetParent(parent, false);
        }
        else
        {
            curveObject = existing.gameObject;
            if (curveObject.GetComponent<CanvasRenderer>() == null)
                curveObject.AddComponent<CanvasRenderer>();
            if (curveObject.GetComponent<Image>() == null)
                curveObject.AddComponent<Image>();
        }

        curveObject.layer = parent.gameObject.layer;
        curveObject.SetActive(true);

        RectTransform rect = curveObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        Image image = curveObject.GetComponent<Image>();
        image.sprite = GetCurvedNotchSprite(corner);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = color;
        image.raycastTarget = false;
    }

    private static void DisableLegacyLine(RectTransform parent, string name)
    {
        Transform legacy = parent.Find(name);
        if (legacy != null)
            legacy.gameObject.SetActive(false);
    }

    private static Sprite GetCurvedNotchSprite(Corner corner)
    {
        int index = (int)corner;
        if (curvedNotchSprites[index] == null)
            curvedNotchSprites[index] = CreateCurvedNotchSprite(corner);
        return curvedNotchSprites[index];
    }

    private static Sprite CreateCurvedNotchSprite(Corner corner)
    {
        const int size = 96;
        const float radius = 25f;
        const float stroke = 12f;
        const float margin = 10f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = $"UICornerDragHandle_CurvedNotch_{corner}"
        };
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = ToBottomLeftCornerSpace(corner, x + 0.5f, y + 0.5f, size);
                float alpha = CurvedNotchAlpha(point, radius, stroke, margin, size);
                texture.SetPixel(x, y, alpha <= 0f ? clear : new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Vector2 ToBottomLeftCornerSpace(Corner corner, float x, float y, float size)
    {
        switch (corner)
        {
            case Corner.BottomRight:
                return new Vector2(size - x, y);
            case Corner.TopLeft:
                return new Vector2(x, size - y);
            case Corner.TopRight:
                return new Vector2(size - x, size - y);
            default:
                return new Vector2(x, y);
        }
    }

    private static float CurvedNotchAlpha(Vector2 point, float radius, float stroke, float margin, float size)
    {
        float halfStroke = stroke * 0.5f;
        Vector2 arcCenter = new Vector2(margin + radius, margin + radius);
        float maxLine = size - margin;

        float horizontalDistance = DistanceToSegment(point, new Vector2(arcCenter.x, margin), new Vector2(maxLine, margin));
        float verticalDistance = DistanceToSegment(point, new Vector2(margin, arcCenter.y), new Vector2(margin, maxLine));
        float arcDistance = Mathf.Abs(Vector2.Distance(point, arcCenter) - radius);
        bool inArcQuadrant = point.x <= arcCenter.x && point.y <= arcCenter.y;

        float distance = Mathf.Min(horizontalDistance, verticalDistance);
        if (inArcQuadrant)
            distance = Mathf.Min(distance, arcDistance);

        return 1f - Mathf.Clamp01(distance - halfStroke + 1f);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denominator = Vector2.Dot(ab, ab);
        if (denominator <= 0.0001f)
            return Vector2.Distance(point, a);

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / denominator);
        return Vector2.Distance(point, a + ab * t);
    }
}

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

    private static Sprite roundedSprite;

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

        Color lineColor = new Color(notchColor.r, notchColor.g, notchColor.b, Mathf.Clamp01(Mathf.Max(0.35f, notchColor.a)));
        ConfigureCornerLines(handleRect, corner, lineColor);
    }

    private static void ConfigureCornerLines(RectTransform parent, Corner corner, Color color)
    {
        const float inset = 4f;
        const float thickness = 4f;
        const float length = 28f;

        switch (corner)
        {
            case Corner.BottomLeft:
                EnsureLine(parent, "NotchLine_H", new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(inset, inset), new Vector2(length, thickness), color);
                EnsureLine(parent, "NotchLine_V", new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(inset, inset), new Vector2(thickness, length), color);
                break;
            case Corner.BottomRight:
                EnsureLine(parent, "NotchLine_H", new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-inset, inset), new Vector2(length, thickness), color);
                EnsureLine(parent, "NotchLine_V", new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-inset, inset), new Vector2(thickness, length), color);
                break;
            case Corner.TopLeft:
                EnsureLine(parent, "NotchLine_H", new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(inset, -inset), new Vector2(length, thickness), color);
                EnsureLine(parent, "NotchLine_V", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(inset, -inset), new Vector2(thickness, length), color);
                break;
            case Corner.TopRight:
                EnsureLine(parent, "NotchLine_H", new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-inset, -inset), new Vector2(length, thickness), color);
                EnsureLine(parent, "NotchLine_V", new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-inset, -inset), new Vector2(thickness, length), color);
                break;
        }
    }

    private static void EnsureLine(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject lineObject;
        if (existing == null)
        {
            lineObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lineObject.transform.SetParent(parent, false);
        }
        else
        {
            lineObject = existing.gameObject;
            if (lineObject.GetComponent<CanvasRenderer>() == null)
                lineObject.AddComponent<CanvasRenderer>();
            if (lineObject.GetComponent<Image>() == null)
                lineObject.AddComponent<Image>();
        }

        lineObject.layer = parent.gameObject.layer;
        lineObject.SetActive(true);

        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        Image image = lineObject.GetComponent<Image>();
        ApplyRoundedImage(image, color);
        image.raycastTarget = false;
    }

    private static void DisableLegacyLine(RectTransform parent, string name)
    {
        Transform legacy = parent.Find(name);
        if (legacy != null)
            legacy.gameObject.SetActive(false);
    }

    private static void ApplyRoundedImage(Image image, Color color)
    {
        if (image == null)
            return;

        if (roundedSprite == null)
            roundedSprite = CreateRoundedSprite();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
    }

    private static Sprite CreateRoundedSprite()
    {
        const int size = 32;
        const int radius = 8;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "UICornerDragHandle_RoundedSprite"
        };
        texture.wrapMode = TextureWrapMode.Clamp;
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x, x - (size - radius - 1), 0);
                float dy = Mathf.Max(radius - y, y - (size - radius - 1), 0);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.Clamp01(distance - radius + 1f);
                texture.SetPixel(x, y, alpha <= 0f ? clear : new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}

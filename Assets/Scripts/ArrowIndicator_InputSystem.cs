using UnityEngine;
using UnityEngine.InputSystem; // << Input System

public class ArrowIndicator_InputSystem : MonoBehaviour
{
    [Header("Referências")]
    public Transform player;
    public Camera cam;
    public SpriteRenderer tail; // pivot: Left-Center
    public SpriteRenderer body; // pivot: Center
    public SpriteRenderer head; // pivot: Center

    [Header("Comprimento (mundo)")]
    public float minLength = 0.6f;
    public float maxLength = 6f;
    [Tooltip("Distância necessária (a partir da borda da deadzone) para ir de min->max.")]
    public float rampDistance = 3f;

    [Header("Deadzone + Suavização")]
    public float deadzone = 0.5f;
    public float deadzoneHysteresis = 0.1f;
    [Tooltip("Tempo de suavização do comprimento (SmoothDamp).")]
    public float smoothTime = 0.12f;

    [Header("Geometria")]
    [Tooltip("Compensação da ‘ponta’ visual. Útil se a cabeça tem espaço antes da ponta real.")]
    public float headOffset = 0.0f;
    [Tooltip("Usar apenas a projeção do mouse ao longo do eixo da seta (reduz jitter lateral).")]
    public bool useDirectionalProjection = true;

    [Header("Espessura (escala Y)")]
    public float thickness = 0.6f; // mesma para tail/body/head

    [Header("Cores (opcional)")]
    public Color tailColor = Color.white;
    public Color bodyColor = new Color(0.8f, 0.95f, 1f, 0.9f);
    public Color headColor = Color.white;

    // --- Internos ---
    float tailW, bodyBaseW, headW; // larguras em unidades de mundo
    float currentLength, lengthVel;
    bool insideDeadzone = true;

    private float overlap;

    private float maxOverlap = -0.0025f;
    private float minOverlap = -2.5e-05f;

    void Awake()
    {
        if (!cam) cam = Camera.main;

        // larguras locais (rect/PPU) — independentes de rotação
        tailW = tail.sprite.rect.width / tail.sprite.pixelsPerUnit;
        bodyBaseW = body.sprite.rect.width / body.sprite.pixelsPerUnit;
        headW = head.sprite.rect.width / head.sprite.pixelsPerUnit;

        // espessura
        SetLocalYScale(tail.transform, thickness);
        SetLocalYScale(body.transform, thickness);
        SetLocalYScale(head.transform, thickness);

        // escala X neutra
        SetLocalXScale(tail.transform, 1f);
        SetLocalXScale(body.transform, 1f);
        SetLocalXScale(head.transform, 1f);

        currentLength = minLength;

        // tint inicial
        if (tail) tail.color = tailColor;
        if (body) body.color = bodyColor;
        if (head) head.color = headColor;
    }

    void LateUpdate()
    {
        if (!player || !cam) return;

        // Segue player
        transform.position = player.position;
        overlap = maxOverlap;

        // --- posição do mouse via Input System ---
        // Pointer.current funciona tanto para mouse quanto pen/touch com cursor.
        Vector2 mouseScreen;
        if (Pointer.current != null)
            mouseScreen = Pointer.current.position.ReadValue();
        else if (Mouse.current != null)
            mouseScreen = Mouse.current.position.ReadValue();
        else
            mouseScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f); // fallback

        Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
        mouseWorld.z = player.position.z;

        // Direção & rotação (sprite aponta para +X)
        Vector2 raw = (mouseWorld - player.position);
        Vector2 dir = raw.sqrMagnitude > 1e-6f ? raw.normalized : Vector2.right;
        transform.right = dir;

        // Distância mundo
        float dWorld = raw.magnitude;

        // Deadzone com histerese
        float enterR = deadzone + deadzoneHysteresis * 0.5f;
        float exitR = Mathf.Max(0f, deadzone - deadzoneHysteresis * 0.5f);
        if (insideDeadzone) { if (dWorld > enterR) insideDeadzone = false; }
        else { if (dWorld < exitR) insideDeadzone = true; }

        // Distância ao longo do eixo da seta + offset da cabeça
        float along = dWorld;
        if (useDirectionalProjection)
        {
            float dot = Vector2.Dot(raw, (Vector2)transform.right);
            along = Mathf.Max(0f, dot);
        }
        along = Mathf.Max(0f, along - headOffset);

        // Mapeamento por rampa (0..1) até maxLength
        float t = Mathf.InverseLerp(0f, Mathf.Max(0.0001f, rampDistance), along);
        // easing suave (smoothstep)
        t = t * t * (3f - 2f * t);

        float targetLength = insideDeadzone ? minLength : Mathf.Lerp(minLength, maxLength, t);

        // Suaviza temporalmente
        currentLength = Mathf.SmoothDamp(currentLength, targetLength, ref lengthVel, smoothTime);
        currentLength = Mathf.Clamp(currentLength, minLength, maxLength);

        // Comprimento destinado ao corpo
        float bodyLen = Mathf.Max(0f, currentLength - (tailW + headW));

        // Escala X do corpo (pivot center)
        float bodyScaleX = (bodyBaseW > 0f) ? (bodyLen / bodyBaseW) : 1f;
        bodyScaleX = Mathf.Max(0.0001f, bodyScaleX);
        SetLocalXScale(body.transform, bodyScaleX);

        // opcional: mais forte quando muito curto
        if (bodyLen < 1.0f) overlap = minOverlap;

        // Posicionamento (tail pivot left; body/head pivot center)
        tail.transform.localPosition = Vector3.zero;
        body.transform.localPosition = new Vector3(tailW + bodyLen * 0.5f, 0f, 0f);
        head.transform.localPosition = new Vector3(tailW + bodyLen + headW * 0.5f - overlap, 0f, 0f);

        // (Opcional) ordenar para head desenhar por cima, escondendo qualquer risco
        head.sortingLayerID = body.sortingLayerID;
        head.sortingOrder = body.sortingOrder + 1;

        // (Opcional) animar tint, gradiente etc. aqui se quiser
        tail.color = tailColor;
        body.color = bodyColor;
        head.color = headColor;
    }

    static void SetLocalXScale(Transform t, float x)
    {
        var s = t.localScale; s.x = x; t.localScale = s;
    }
    static void SetLocalYScale(Transform t, float y)
    {
        var s = t.localScale; s.y = y; t.localScale = s;
    }
}

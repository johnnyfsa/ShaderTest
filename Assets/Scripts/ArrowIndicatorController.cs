using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ArrowIndicatorController : MonoBehaviour
{
    [Header("Referências")]
    public Transform player;
    public Camera cam;
    private SpriteRenderer sr;
    private Material mat;

    [Header("Comprimento visual (unidades de mundo)")]
    public float minLength = 0.6f;   // comprimento mínimo da seta
    public float maxLength = 6f;     // comprimento máximo da seta (visual)

    [Header("Faixa de medição da distância")]
    [Tooltip("Quantos metros a partir da borda da deadzone para a seta ir de minLength até maxLength.")]
    public float rampDistance = 3.0f;

    [Tooltip("Suavização temporal do comprimento.")]
    public float smoothTime = 0.12f;

    [Header("Deadzone + Histerese")]
    [Tooltip("Raio ao redor do player em que a seta fica no comprimento mínimo.")]
    public float deadzone = 0.5f;
    [Tooltip("Margem para evitar flicker ao entrar/sair da deadzone.")]
    public float deadzoneHysteresis = 0.1f;

    [Header("Geometria / Projeção")]
    [Tooltip("Desconte a distância da 'cabeça' da seta para não saturar cedo.")]
    public float headOffset = 0.25f;
    [Tooltip("Usar somente a projeção na direção da seta (reduz jitter lateral).")]
    public bool useDirectionalProjection = true;

    [Header("Curva de crescimento")]
    [Tooltip("1 = linear; >1 = cresce mais devagar no início.")]
    public float easingPower = 1.6f;
    [Tooltip("Use saturação suave exponencial em vez de clamp duro.")]
    public bool useSoftSaturate = false;
    [Tooltip("Ganho da saturação suave (quanto maior, mais rápido aproxima do máximo).")]
    public float softSatGain = 1.2f;

    [Header("Espessura visual (escala Y)")]
    public float thickness = 0.6f;

    [Header("9-Slice (px na textura)")]
    public float tailPixels = 8f;
    public float headPixels = 24f;

    [Header("Cores")]
    public Color tint = new Color(1, 1, 1, 0.95f);
    public bool useGradient = true;
    public Color gradStart = Color.white;
    public Color gradEnd = new Color(0.5f, 0.9f, 1f, 1f);
    [Range(0.1f, 3f)] public float gradPower = 1f;

    // estado interno
    float currentLength;
    float lengthVel;
    float baseScaleX = 1f;
    float texWidthPx = 128f;
    bool insideDeadzone = true;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (!cam) cam = Camera.main;

        mat = Instantiate(sr.sharedMaterial);
        sr.material = mat;

        if (sr.sprite) texWidthPx = sr.sprite.textureRect.width;
        mat.SetFloat("_TexWidthPx", texWidthPx);
        mat.SetFloat("_TailPixels", tailPixels);
        mat.SetFloat("_HeadPixels", headPixels);

        baseScaleX = transform.localScale.x;
        currentLength = Mathf.Max(minLength, 0.0001f);

        var s = transform.localScale;
        s.y = thickness;
        transform.localScale = s;
    }

    void LateUpdate()
    {
        if (!player || !cam) return;

        transform.position = player.position;

        Vector3 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        mouse.z = player.position.z;

        Vector2 rawDir = (mouse - player.position);
        Vector2 dir = rawDir.sqrMagnitude > 1e-6f ? rawDir.normalized : Vector2.right;
        transform.right = dir;

        float dWorld = rawDir.magnitude;

        // Deadzone com histerese
        float enterR = deadzone + deadzoneHysteresis * 0.5f;
        float exitR = Mathf.Max(0f, deadzone - deadzoneHysteresis * 0.5f);
        if (insideDeadzone) { if (dWorld > enterR) insideDeadzone = false; }
        else { if (dWorld < exitR) insideDeadzone = true; }

        // Distância "ao longo" da seta (opcional) e compensação da cabeça
        float along = dWorld;
        if (useDirectionalProjection)
        {
            float dot = Vector2.Dot(rawDir, (Vector2)transform.right);
            along = Mathf.Max(0f, dot);
        }
        along = Mathf.Max(0f, along - headOffset);

        // --- NOVO: mapeamento por rampa ---
        // Quanto da rampa já foi percorrido desde a borda da deadzone?
        float distFromEdge = Mathf.Max(0f, along); // já descontou headOffset
        float t; // 0..1

        if (useSoftSaturate)
        {
            // Saturação suave assintótica até 1: 1 - exp(-k*x/L)
            float L = Mathf.Max(0.0001f, rampDistance);
            t = 1f - Mathf.Exp(-softSatGain * distFromEdge / L);
        }
        else
        {
            // Rampa linear (com easing) até atingir 1 em rampDistance
            t = Mathf.InverseLerp(0f, Mathf.Max(0.0001f, rampDistance), distFromEdge);
            t = ApplyEasing(t, easingPower);
        }

        float targetLen = insideDeadzone ? minLength : Mathf.Lerp(minLength, maxLength, t);

        // Suavização temporal
        currentLength = Mathf.SmoothDamp(currentLength, targetLen, ref lengthVel, smoothTime, Mathf.Infinity, Time.deltaTime);
        currentLength = Mathf.Clamp(currentLength, minLength, maxLength);

        // Aplica escala X proporcional ao comprimento (ajuste o divisor se sua arte tiver outra largura base)
        float scaleX = Mathf.Max(0.0001f, currentLength / 1f);
        transform.localScale = new Vector3(scaleX, thickness, 1f);

        // 9-slice: informa alongamento ao shader
        float stretchX = Mathf.Max(0.0001f, scaleX / baseScaleX);
        mat.SetFloat("_StretchX", stretchX);

        // Cores
        mat.SetColor("_Tint", tint);
        mat.SetFloat("_UseGradient", useGradient ? 1f : 0f);
        mat.SetColor("_GradA", gradStart);
        mat.SetColor("_GradB", gradEnd);
        mat.SetFloat("_GradPower", gradPower);
    }

    static float ApplyEasing(float t, float power)
    {
        // smoothstep básico
        t = t * t * (3f - 2f * t);
        // power (>1 deixa ainda mais devagar no começo)
        if (power > 1.001f) t = Mathf.Pow(t, power);
        return Mathf.Clamp01(t);
    }
}

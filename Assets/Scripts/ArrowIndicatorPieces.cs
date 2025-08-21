using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowIndicatorPieces : MonoBehaviour
{
    public Transform player;
    public Camera cam;

    public SpriteRenderer tail;
    public SpriteRenderer body; // corpo com pivot no CENTRO do sprite
    public SpriteRenderer head; // cabeça com pivot no CENTRO do sprite

    [Header("Comprimento total da seta (mundo)")]
    public float minLength = 0.6f;
    public float maxLength = 6f;

    [Header("Largura (espessura visual)")]
    public float thickness = 0.6f; // ajusta via scale.y das peças

    float currentLength;

    // Larguras em UNIDADES DE MUNDO (constantes, independente de rotação)
    float tailW, headW, bodyBaseW;

    private float overlap;

    private float maxOverlap = -0.0025f;
    private float minOverlap = -2.5e-05f;

    void Awake()
    {
        if (!cam) cam = Camera.main;

        // Converter pixels → unidades
        tailW = tail.sprite.rect.width / tail.sprite.pixelsPerUnit;
        headW = head.sprite.rect.width / head.sprite.pixelsPerUnit;
        bodyBaseW = body.sprite.rect.width / body.sprite.pixelsPerUnit;

        // espessura vertical
        SetLocalYScale(tail.transform, thickness);
        SetLocalYScale(body.transform, thickness);
        SetLocalYScale(head.transform, thickness);

        // zere escalas X iniciais
        SetLocalXScale(body.transform, 1f);
        SetLocalXScale(tail.transform, 1f);
        SetLocalXScale(head.transform, 1f);

        currentLength = minLength;
    }

    void LateUpdate()
    {
        if (!player || !cam) return;

        // Segue player
        transform.position = player.position;
        overlap = maxOverlap;
        // Direção para o mouse
        Vector3 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        mouse.z = player.position.z;
        Vector2 dir = (mouse - player.position).normalized;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
        transform.right = dir;

        // Comprimento total desejado
        float dist = Vector2.Distance(player.position, mouse);
        currentLength = Mathf.Clamp(dist, minLength, maxLength);

        // Comprimento destinado ao corpo (total - tail - head)
        float bodyLen = Mathf.Max(0f, currentLength - (tailW + headW));

        // Escala do corpo ao longo do X (pivot CENTRAL)
        // Se o sprite do corpo mede bodyBaseW em unidades quando scale.x=1,
        // queremos que ele “tenha” bodyLen → scaleX = bodyLen / bodyBaseW
        float bodyScaleX = (bodyBaseW > 0f) ? (bodyLen / bodyBaseW) : 1f;
        bodyScaleX = Mathf.Max(0.0001f, bodyScaleX);
        SetLocalXScale(body.transform, bodyScaleX);

        // Agora, posicionamento:
        // - tail: começa no pivot (0)
        // - body (pivot central): a esquerda do body está em tailW; o centro fica em tailW + bodyLen/2
        // - head (pivot central): centrado no final = tailW + bodyLen + headW/2
        tail.transform.localPosition = Vector3.zero;
        // 1 pixel em unidades de mundo (exemplo: se PPU = 100 → 0.01f)

        // mantém o corpo igual
        body.transform.localPosition = new Vector3(tailW + bodyLen * 0.5f, 0f, 0f);

        if (bodyLen <= 1.0f) overlap = minOverlap;
        // puxa a cabeça um tiquinho para trás
        head.transform.localPosition = new Vector3(tailW + bodyLen + headW * 0.5f - (overlap), 0f, 0f);

    }

    static void SetLocalXScale(Transform t, float x)
    {
        var s = t.localScale;
        s.x = x;
        t.localScale = s;
    }
    static void SetLocalYScale(Transform t, float y)
    {
        var s = t.localScale;
        s.y = y;
        t.localScale = s;
    }
}

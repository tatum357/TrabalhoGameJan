using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

public class SistemaControl : MonoBehaviour
{
    public LevelLoader Loader;
    [SerializeField] private Transform Destino;
    [SerializeField] private Transform pontoSaida; // opcional: vazio ao lado do portal p/ nascer fora do trigger
    [SerializeField] private float tempoFadeOut = 0.85f; // tem que ser MENOR que o clip (seu fade tem 1s)
    [SerializeField] private float cooldown = 1.5f;

    [Header("Camera - limite do TileMap")]
    [SerializeField] private CinemachineConfiner2D confiner;
    [SerializeField] private Collider2D limitesDestino; // poligono da sala de destino (cameralimite / cameralimite1). Se vazio, mantem o atual.
    [SerializeField] private bool centralizarCameraNoDestino = true;

    private static float proximoTpLiberado = 0f;

    private void Awake()
    {
        if (confiner == null)
            confiner = FindAnyObjectByType<CinemachineConfiner2D>();
        if (confiner != null && !confiner.enabled)
            confiner.enabled = true;
    }

    public void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.time < proximoTpLiberado) return;
        if (!EhPlayer(other)) return;
        StartCoroutine(Teleportar(other));
    }

    private bool EhPlayer(Collider2D col)
    {
        if (col.CompareTag("Player")) return true;
        // Fallback caso a tag nao esteja aplicada no prefab: aceita pelo nome ou pelo script de controle
        Transform t = col.transform;
        if (t.GetComponentInParent<Controlapersonagem>() != null) return true;
        return false;
    }

    private IEnumerator Teleportar(Collider2D colPlayer)
    {
        proximoTpLiberado = Time.time + tempoFadeOut + cooldown;
        Transform playerT = colPlayer.transform;

        Rigidbody2D rb = playerT.GetComponentInParent<Rigidbody2D>();
        if (rb == null) rb = playerT.GetComponent<Rigidbody2D>();
        MonoBehaviour move = playerT.GetComponentInParent<Controlapersonagem>();
        if (move == null) move = playerT.GetComponent<Controlapersonagem>();

        if (rb != null) { rb.velocity = Vector2.zero; rb.Sleep(); } // congela ANTES do fade p/ nao andar no escuro
        if (move != null) move.enabled = false;

        if (Loader != null) Loader.TocarFade("Start");
        yield return new WaitForSeconds(tempoFadeOut); // teleporta um pouco ANTES do preto total, sem tempo morto

        Vector3 posAntiga = playerT.position;
        Vector3 posNova = posAntiga;

        if (Destino == null)
            Debug.LogError("[TP] Destino NULL em " + gameObject.name + " - arraste no Inspector");

        Vector3 alvo = (pontoSaida != null ? pontoSaida.position
                                            : (Destino != null ? Destino.position : posAntiga));
        posNova = new Vector3(alvo.x, alvo.y, playerT.position.z);

        // 1) Troca o limite da camera PRIMEIRO (senao ela nasce presa no limite antigo)
        AplicarLimitesCamera(posNova);

        // 2) So entao move o player
        if (Destino != null || pontoSaida != null)
        {
            playerT.position = posNova;
            if (rb != null) rb.position = new Vector2(posNova.x, posNova.y);
        }

        if (centralizarCameraNoDestino)
            CentralizarCamera(playerT, posNova - posAntiga);

        if (Loader != null) Loader.TocarFade("StartInicio");

        if (rb != null) rb.WakeUp();
        if (move != null) move.enabled = true;
    }

    // Troca o confiner para os limites da sala de destino.
    // Se "limites Destino" estiver vazio no Inspector, procura automaticamente
    // um colisor de limite que contenha o ponto de chegada (evita camera presa).
    private void AplicarLimitesCamera(Vector3 pontoDestino)
    {
        if (confiner == null) return;

        Collider2D limites = limitesDestino;
        if (limites == null)
            limites = EncontrarLimites(pontoDestino);

        if (limites == null)
        {
            Debug.LogWarning("[TP] 'Limites Destino' nao preenchido em " + gameObject.name
                + " e nenhum limite foi encontrado perto do ponto de chegada. "
                + "Arraste o colisor da sala de destino no Inspector, senao a camera fica presa no limite antigo.");
            return;
        }

        confiner.m_BoundingShape2D = limites;
        confiner.InvalidateCache();
    }

    private Collider2D EncontrarLimites(Vector3 ponto)
    {
        foreach (var c in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (c.gameObject == gameObject) continue;
            if (c.name.IndexOf("limite", System.StringComparison.OrdinalIgnoreCase) >= 0
                && c.OverlapPoint(ponto))
                return c;
        }
        return null;
    }

    private void CentralizarCamera(Transform playerT, Vector3 deltaPos)
    {
        var vcam = FindAnyObjectByType<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            // Pula o damping num frame para a camera ja nascer no destino, sem mostrar o vazio no caminho
            float damp = 0f;
            var transposer = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (transposer != null)
            {
                damp = transposer.m_XDamping;
                transposer.m_XDamping = 0f;
                transposer.m_YDamping = 0f;
                transposer.m_ZDamping = 0f;
            }
            vcam.OnTargetObjectWarped(playerT, deltaPos);
            if (transposer != null)
                StartCoroutine(RestaurarDamping(transposer, damp));
        }
    }

    private IEnumerator RestaurarDamping(CinemachineFramingTransposer t, float damp)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        t.m_XDamping = damp;
        t.m_YDamping = damp;
        t.m_ZDamping = damp;
    }
}

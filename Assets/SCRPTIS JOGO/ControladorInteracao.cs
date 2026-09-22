using UnityEngine;

// Controla as interacoes com objetos (ex.: fogao/panela).
// - MostrarBotao()/EsconderBotao() podem ser chamados por voce, OU acontecem
//   automaticamente quando o jogador entra/sai do collider deste objeto (trigger ou colisao).
// - Enquanto o botao estiver visivel, a tecla E abre o minigame (MinigameCozinha).
// COLOQUE ESTE SCRIPT NO OBJETO DE COZINHA (com Collider2D).
public class ControladorInteracao : MonoBehaviour
{
    [Header("Botao que acompanha o jogador")]
    public GameObject botaoInteracao;
    public Transform jogador;
    public Vector3 deslocamento = new Vector3(1f, 0.5f, 0f);

    private bool podeInteragir = false;

    void Start()
    {
        if (botaoInteracao != null)
        {
            botaoInteracao.SetActive(false);
        }

        // Procura o player automaticamente se voce nao arrastar
        if (jogador == null)
        {
            var controle = FindAnyObjectByType<Controlapersonagem>();
            if (controle != null) jogador = controle.transform;
        }
    }

    void Update()
    {
        if (podeInteragir && jogador != null && botaoInteracao != null)
        {
            // Mantem o botao ao lado do jogador
            botaoInteracao.transform.position = jogador.position + deslocamento;

            // Verifica se o jogador pressionou a tecla E -> abre o minigame
            if (Input.GetKeyDown(KeyCode.E))
            {
                Debug.Log("Tecla E pressionada! Abrindo o minigame.");
                if (MinigameCozinha.Instancia != null)
                    MinigameCozinha.Instancia.Abrir();
                else
                    Debug.LogWarning("[Interacao] MinigameCozinha nao encontrado no Canvas _ Sistema.");
            }
        }
    }

    // Deteccao automatica pelo collider do objeto (use Is Trigger, ou colisao comum)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (EhJogador(other)) MostrarBotao();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (EhJogador(other)) EsconderBotao();
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (EhJogador(col.collider)) MostrarBotao();
    }

    private void OnCollisionExit2D(Collision2D col)
    {
        if (EhJogador(col.collider)) EsconderBotao();
    }

    private bool EhJogador(Collider2D col)
    {
        if (col == null) return false;
        if (col.CompareTag("Player")) return true;
        return col.GetComponentInParent<Controlapersonagem>() != null;
    }

    public void MostrarBotao()
    {
        podeInteragir = true;

        if (botaoInteracao != null)
        {
            botaoInteracao.SetActive(true);
        }
    }

    public void EsconderBotao()
    {
        podeInteragir = false;

        if (botaoInteracao != null)
        {
            botaoInteracao.SetActive(false);
        }
    }
}
